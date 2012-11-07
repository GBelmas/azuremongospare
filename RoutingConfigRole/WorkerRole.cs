//Copyright (c) <2012>, Kobojo©, Vnext
//All rights reserved.

//Redistribution and use in source and binary forms, with or without
//modification, are permitted provided that the following conditions are met:
//1. Redistributions of source code must retain the above copyright
//   notice, this list of conditions and the following disclaimer.
//2. Redistributions in binary form must reproduce the above copyright
//   notice, this list of conditions and the following disclaimer in the
//   documentation and/or other materials provided with the distribution.
//3. All advertising materials mentioning features or use of this software
//   must display the following acknowledgement:
//   This product includes software developed by the Kobojo©, VNext.
//4. Neither the name of the Kobojo©, VNext nor the
//   names of its contributors may be used to endorse or promote products
//   derived from this software without specific prior written permission.

//THIS SOFTWARE IS PROVIDED BY Kobojo©, VNext ''AS IS'' AND ANY
//EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//DISCLAIMED. IN NO EVENT SHALL Kobojo©, VNext BE LIABLE FOR ANY
//DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
//(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
//LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
//ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

namespace RoutingConfigRole
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Helpers;
    using Helpers.Azure;
    using Helpers.Mongo;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Diagnostics;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using Microsoft.WindowsAzure.StorageClient;


    public partial class WorkerRole : RoleEntryPoint
    {
        #region Fields

        private Process mongoShardProcess = null;
        private Process mongoConfigProcess = null;
        //Cloud drive for MongoC data
        private CloudDrive mongoDrive = null;
        private CloudStorageAccount storageAccount = null;
        private string dbConfigPath = null;
        //Queues' names for MongoC SOS Messages
        private const string CONFIG_REQUEST_QUEUE = "configrequestqueue";
        private const string CONFIG_RESPONSE_QUEUE = "configresponsequeue";
        //SOS message content
        private const string CONFIG_SOS_MESSAGE = "SOS";
        //private string listingPath = string.Empty;
        private volatile bool busy = false;
        private object MainSequencerLock = new object();
        private bool MongoCFirstStart = true;

        #endregion

        public bool RedirectMongoOutputToTrace
        {
            get { return RoleEnvironment.GetConfigurationSettingValue("RedirectMongoOutputToTrace") == "1"; ; }
        }

        /// <summary>
        /// RUN : Main Loop
        /// </summary>
        public override void Run()
        {
            Trace.TraceInformation("Instance run - Entering run method");
            this.busy = true;

            Trace.TraceInformation("Instance run - Entering main loop...");

            Task hostUpdaterTask = new Task(new Action(() => { HostUpdater.Run(true); }));
            
            while (true)
            {
                try
                {
                    if (hostUpdaterTask.Status != TaskStatus.Running)
                    {
                        hostUpdaterTask.Start();
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                }
                try
                {
                    //Aquire lock to serialize all Sequencer actions (otherwise, it's a mess)
                    lock (MainSequencerLock)
                    {

                        #region S+C Role
                        // ================================================================
                        //                 S + C Role
                        //=================================================================
                        if (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole == MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole)
                        {
                            //Getting current status. Warning, CurrentConfigRoutingState might change over time
                            RoutingConfigRoleState currentStatus = CurrentConfigRoutingState;
                            switch (currentStatus)
                            {
                                //Boring states
                                case RoutingConfigRoleState.Unknown:
                                    Trace.TraceError("MainLoop : Status Unknown : Wrong Status");
                                    break;
                                case RoutingConfigRoleState.Starting:
                                    Trace.TraceError("MainLoop : Status Starting : Wrong Status");
                                    break;
                                case RoutingConfigRoleState.Stopping:
                                    Trace.TraceInformation("MainLoop : Status Stopping : Doing nothing");
                                    break;
                                case RoutingConfigRoleState.Stopped:
                                    Trace.TraceInformation("MainLoop : Status Stopped : Doing nothing");
                                    break;

                                //Status MongoConfig_NotRunning
                                //=============================
                                case RoutingConfigRoleState.MongoConfig_NotRunning:
                                    this.busy = true;
                                    //Check MongoS and MongoC process health
                                    try
                                    {
                                        if (!ProcessTools.IsRunning(mongoConfigProcess))
                                        {
                                            Trace.TraceInformation("MainLoop : Status MongoConfig_NotRunning : MongoC not running - registering MongoC process to 'stopped'");
                                            MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongoc");
                                            StartMongoConfig();
                                        }
                                        else
                                        {
                                            //Mongoc Already running, going to next stage
                                            SetCurrentState(RoutingConfigRoleState.MongoShard_NotRunning);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // InvalidOperationException could be throw when There is no process associated with the object. 
                                        // http://msdn.microsoft.com/en-us/library/system.diagnostics.process.hasexited.aspx
                                        Trace.TraceError(string.Format("MainLoop : Status MongoConfig_NotRunning : MongoC Process HasExited property Exception : {0}", ex.Message));
                                    }
                                    break;

                                //Status MongoShard_NotRunning
                                //============================
                                case RoutingConfigRoleState.MongoShard_NotRunning:
                                    this.busy = true;
                                    if (MongoHelper.IsMongocConfigInstancesAllStarted())
                                    {
                                        //Start the Mongos if not started yet
                                        StartMongoShard();
                                    }
                                    else
                                    {
                                        Trace.TraceInformation("MainLoop : Status MongoShard_NotRunning : Waiting for all mongoC to start...");
                                    }

                                    if (ProcessTools.IsRunning(mongoShardProcess))
                                    {
                                        SetCurrentState(RoutingConfigRoleState.Nominal);
                                    }

                                    break;

                                //Status Nominal
                                //==============
                                case RoutingConfigRoleState.Nominal:

                                    //Clone our avaibility to mongos health
                                    this.busy = !ProcessTools.IsRunning(mongoShardProcess);

                                    //Display our Shard config
                                    List<ShardNode> shards = MongoHelper.GetShardConfigurationFromMongoC(MongoDBAzurePlatform.Instance.MyMongoCAddress);
                                    Trace.TraceInformation(string.Format("MainLoop : Status Nominal : Mongos {0}, MongoC shards : {1}", this.busy ? "DOWN" : "UP", string.Join(",", shards.OrderBy(s => s.ID).Select(s => s.Host))));

                                    break;

                                //We should not be here
                                default:
                                    Trace.TraceError("MainLoop : Wrong Status" + currentStatus.ToString());
                                    break;
                            }
                        }
                        #endregion

                        #region S only role
                        // ================================================================
                        //                 S only Role
                        //=================================================================
                        if (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole == MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingOnlyRole)
                        {
                            //Getting current status. Warning, CurrentConfigRoutingState might change over time
                            RoutingConfigRoleState currentStatus = CurrentConfigRoutingState;
                            switch (currentStatus)
                            {
                                //Boring states
                                case RoutingConfigRoleState.Unknown:
                                    Trace.TraceError("MainLoop : Status Unknown : Wrong Status");
                                    break;
                                case RoutingConfigRoleState.Starting:
                                    Trace.TraceError("MainLoop : Status Starting : Wrong Status");
                                    break;
                                case RoutingConfigRoleState.Stopping:
                                    Trace.TraceInformation("MainLoop : Status Stopping : Doing nothing");
                                    break;
                                case RoutingConfigRoleState.Stopped:
                                    Trace.TraceInformation("MainLoop : Status Stopped : Doing nothing");
                                    break;

                                //Status MongoConfig_NotRunning
                                //=============================
                                case RoutingConfigRoleState.MongoConfig_NotRunning:
                                    Trace.TraceError("MainLoop : Status Unknown : Wrong Status");
                                    break;

                                //Status MongoShard_NotRunning
                                //============================
                                case RoutingConfigRoleState.MongoShard_NotRunning:
                                    this.busy = true;
                                    if (MongoHelper.IsMongocConfigInstancesAllStarted())
                                    {
                                        //Start the Mongos if not started yet
                                        StartMongoShard();
                                    }
                                    else
                                    {
                                        Trace.TraceInformation("MainLoop : Status MongoShard_NotRunning : Waiting for all mongoC to start...");
                                    }

                                    if (ProcessTools.IsRunning(mongoShardProcess))
                                    {
                                        SetCurrentState(RoutingConfigRoleState.Nominal);
                                    }

                                    break;

                                //Status Nominal
                                //==============
                                case RoutingConfigRoleState.Nominal:

                                    //Clone our avaibility to mongos health
                                    this.busy = !ProcessTools.IsRunning(mongoShardProcess);

                                    //Display our Shard config
                                    Trace.TraceInformation(string.Format("MainLoop : Status Nominal : Mongos {0}", this.busy ? "DOWN" : "UP"));
                                    break;

                                //We should not be here
                                default:
                                    Trace.TraceError("MainLoop : Wrong Status" + currentStatus.ToString());
                                    break;
                            }
                        }
                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    //Exception during main loop
                    Trace.TraceError(string.Format("Instance run - Exception in main loop : {0}, Inner : {1} at {2}", ex.Message, ex.InnerException == null ? "" : ex.InnerException.Message, ex.StackTrace));
                }

                //Main loop cycle
                Thread.Sleep(10000);

            }
        }

        #region Sharding stuff

        

        private bool IsShardingConfigured()
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("configuration");
            container.CreateIfNotExist();

            bool isExist = container.GetBlobReference(RoleEnvironment.CurrentRoleInstance.Role.Name).Exists();

            return isExist;
        }

        #endregion

        /// <summary>
        /// ON START
        /// </summary>
        /// <returns></returns>
        public override bool OnStart()
        {
            Trace.TraceInformation("RoutingConfigRole entry point called - OnStart Method");
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 10 * 12 * Environment.ProcessorCount;

            //Disable small packet assembling, reducing network lattency
            ServicePointManager.UseNagleAlgorithm = false;

            //Enable TCP KeepAlive 2h, 1s
            ServicePointManager.SetTcpKeepAlive(true, 2 * 3600 * 1000, 1000);

            Trace.TraceInformation("Instance start - Configuring RoleEnvironment handlers");
            #region Setup CloudStorageAccount Configuration Setting Publisher

            // This code sets up a handler to update CloudStorageAccount instances when their corresponding
            // configuration settings change in the service configuration file.
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
            {
                // Provide the configSetter with the initial value
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));

                RoleEnvironment.Changed += (sender, arg) =>
                {
                    if (arg.Changes.OfType<RoleEnvironmentConfigurationSettingChange>().Any((change) => (change.ConfigurationSettingName == configName)))
                    {
                         // If the autorestart config changed, do nothing
                        if (configName != "AutoRestart")
                        {
                            // The corresponding configuration setting has changed, propagate the value
                            if (!configSetter(RoleEnvironment.GetConfigurationSettingValue(configName)))
                            {
                                Trace.TraceInformation("RoleEnvironmentConfigurationSettingChange : Requesting a recycle");

                                // In this case, the change to the storage account credentials in the
                                // service configuration is significant enough that the role needs to be
                                // recycled in order to use the latest settings. (for example, the
                                // endpoint has changed)
                                RoleEnvironment.RequestRecycle();
                            }
                        }
                    }
                };
            });

            #endregion

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += new EventHandler<RoleEnvironmentChangingEventArgs>(RoleEnvironment_Changing);
            RoleEnvironment.Stopping += new EventHandler<RoleEnvironmentStoppingEventArgs>(RoleEnvironment_Stopping);
            RoleEnvironment.StatusCheck += new EventHandler<RoleInstanceStatusCheckEventArgs>(RoleEnvironment_StatusCheck);

            Trace.TraceInformation("Instance start - Configuring Diagnostic Monitor");
            #region Diagnostic Monitor configuration

            var cfg = DiagnosticMonitor.GetDefaultInitialConfiguration();
            cfg.Logs.ScheduledTransferLogLevelFilter = LogLevel.Information;
            cfg.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);

            cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
                {
                    CounterSpecifier = @"\Processor(_Total)\% Processor Time",
                    SampleRate = TimeSpan.FromSeconds(10d)
                });
            cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
                {
                    CounterSpecifier = @"\Memory\Available Bytes",
                    SampleRate = TimeSpan.FromSeconds(10d)
                });
            cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
            {
                CounterSpecifier = @"\PhysicalDisk(_Total)\% Idle Time",
                SampleRate = TimeSpan.FromSeconds(10d)
            });
            cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
            {
                CounterSpecifier = @"\PhysicalDisk(_Total)\Avg. Disk Queue Length",
                SampleRate = TimeSpan.FromSeconds(10d)
            });

            cfg.PerformanceCounters.ScheduledTransferPeriod = TimeSpan.FromMinutes(1d);

            DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", cfg);

            #endregion

#if !DEBUG
            // Configuration of the firewall
            // FirewallManagement.ConfigureFirewall();
#else
            Trace.TraceInformation("DEBUG Mode : Firewall not configured !!");
#endif
            SetCurrentState(RoutingConfigRoleState.Starting);

            #region Path for the storage
            //Drive only for S+C role
            if (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole == MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole)
            {
                Trace.TraceInformation("Instance start - Mounting cloud drive for MongoC data");
                // Get the local cache for the cloud drive
                LocalResource localCache = RoleEnvironment.GetLocalResource("MongocCache");

                // we'll use all the cache space we can (note: InitializeCache doesn't work with trailing slash)
                CloudDrive.InitializeCache(localCache.RootPath.TrimEnd('\\'), localCache.MaximumSizeInMegabytes);

                // connect to the storage account
                storageAccount = CloudStorageAccount.FromConfigurationSetting("MongoDbData");

                // client for talking to our blob files
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                // the container that our dive is going to live in
                CloudBlobContainer drives = blobClient.GetContainerReference(RoleEnvironment.GetConfigurationSettingValue("ContainerName"));

                // create blob container (it has to exist before creating the cloud drive)
                try { drives.CreateIfNotExist(); }
                catch { }

                // get the url to the vhd page blob we'll be using
                var vhdUrl = drives.GetPageBlobReference(string.Format("mongoc-{0}.vhd", RoleEnvironment.CurrentRoleInstance.Id)).Uri.ToString();

                mongoDrive = storageAccount.CreateCloudDrive(vhdUrl);
                try
                {
                    mongoDrive.Create(localCache.MaximumSizeInMegabytes);
                }
                catch (CloudDriveException)
                {
                    // exception is thrown if all is well but the drive already exists
                }
                dbConfigPath = mongoDrive.Mount(localCache.MaximumSizeInMegabytes, DriveMountOptions.Force) + @"\";
                Trace.TraceInformation("Instance start - Cloud drive mounted");
            }
            #endregion

            switch (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole)
            {
                case MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole:
                    Trace.TraceInformation(string.Format("Instance Start (S+C) - Registering mongoc and mongos instance as 'stopped', MongoConfig IP is {0}, MongoS IP is {1}", MongoDBAzurePlatform.Instance.MyMongoCAddress.ToString(),MongoDBAzurePlatform.Instance.MyMongoSAddress.ToString()));
                    break;
                case MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingOnlyRole:
                    Trace.TraceInformation(string.Format("Instance Start (S only) - Registering mongoc and mongos instance as 'stopped', MongoS IP is {0}", MongoDBAzurePlatform.Instance.MyMongoSAddress.ToString()));
                    break;
                default:
                    Trace.TraceError("On Start : Wrong Functionnal Role : " + (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole.ToString()));
                    break;
            }

            MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongoc");
            MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongos");

            //Start MongoDB performance counters collection for MongoC process
            Trace.TraceInformation("Instance start - Starting custom performance counters");
            if (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole == MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole)
                MongoDB.PerformanceCounters.PerformanceMonitor.Start(MongoDBAzurePlatform.Instance.MyMongoCAddress.Host, MongoDBAzurePlatform.Instance.MyMongoCAddress.Port, int.Parse(RoleEnvironment.GetConfigurationSettingValue("PerfCountersSamplingIntervalInMs")));


            Trace.TraceInformation("Instance start - end of instance start");
            switch (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole)
            {
                case MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole:
                    SetCurrentState(RoutingConfigRoleState.MongoConfig_NotRunning);
                    break;
                case MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingOnlyRole:
                    SetCurrentState(RoutingConfigRoleState.MongoShard_NotRunning);
                    break;
                default:
                    Trace.TraceError("On Start : Wrong Functionnal Role : "+(MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole.ToString()));
                    break;
            }

            return base.OnStart();
        }

        /// <summary>
        /// StartMongoConfig : start mongoC process
        /// </summary>
        private void StartMongoConfig()
        {
            bool start;
            if (MongoCFirstStart)
            {
                start = true;
            }
            else
            {
                //Start only if AutoRestart option is true
                bool autoRestart = false;
                bool parseResult = bool.TryParse(RoleEnvironment.GetConfigurationSettingValue("AutoRestart"), out autoRestart);

                if (parseResult)
                {
                    //If the Autorestart flag is set to 'true' then we recycle the process
                    if (autoRestart)
                    {
                        Trace.TraceInformation("StartMongoConfig - The Autorestart flag is active, restarting MongoC");
                        start = true;
                    }
                    else
                    {
                        // The MongoC process has exited but the autorestart denies restart
                        Trace.TraceInformation("StartMongoConfig - The Autorestart flag is not active, MongoC will NOT be restarted");

                        start = false;
                    }
                }
                else
                {
                    Trace.TraceError("MongoC_Exit - Unable to get the AutoRestart flag value. Doing nothing.");
                    start = false;
                }
            }

            if (start)
            {
                NodeEndPoint myAddress = MongoDBAzurePlatform.Instance.MyMongoCAddress;

                Trace.TraceInformation("StartMongoC - Trying to launch the process...");
                // Start the config process
                this.mongoConfigProcess = null;
                this.mongoConfigProcess = ProcessTools.StartProcess(MongoDBAzurePlatform.Instance.MongoExePath,
                                                                "mongod.exe",
                                                                MongoHelper.GetArgumentsForConfigMongo(dbConfigPath,
                                                                                                        myAddress.ToString(),
                                                                                                        myAddress.Port.ToString()),
                                                                RedirectMongoOutputToTrace,
                                                                false,
                                                                MongoConfiguration_Exited
                                                            );
                Thread.Sleep(1000); //sleep to allow the process to start up correctly before checking its state
                try
                {
                    if (!this.mongoConfigProcess.HasExited)
                    {
                        MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Running, "mongoc");
                        Trace.TraceInformation("StartMongoC - Process has started successfully");
                    }
                    else
                    {
                        MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongoc");
                        Trace.TraceError("StartMongoC - Process has failed to start");
                    }
                }
                catch (Exception)
                {
                    MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongoc");
                    Trace.TraceError("StartMongoC - Process has failed to start");
                }
            }
        }

        /// <summary>
        /// StartMongoShard : start mongoS process
        /// </summary>
        private void StartMongoShard()
        {
            // Check if all MongoC instances are started
            if (MongoHelper.IsMongocConfigInstancesAllStarted())
            {
                // Check if MongoS is already started on the current instance
                if (!MongoHelper.IsMongoRoutingInstanceStarted())
                {

                    Trace.TraceInformation("StartMongoS - Trying to launch the process...");

                    NodeEndPoint routingEndpoint = MongoDBAzurePlatform.Instance.MyMongoSAddress;

                    // Start the routing process
                    mongoShardProcess = ProcessTools.StartProcess(MongoDBAzurePlatform.Instance.MongoExePath,
                                                                "mongos.exe",
                                                                MongoHelper.GetArgumentsForRoutingMongo(routingEndpoint.Host,
                                                                                                        routingEndpoint.Port.ToString(),
                                                                                                        MongoDBAzurePlatform.Instance.MongoCInstances.ToArray()),
                                                                RedirectMongoOutputToTrace,
                                                                false,
                                                                MongoShard_Exited
                                                            );
                    Thread.Sleep(1000); //sleep to allow the process to start up correctly before checking its state

                    try
                    {
                        if (!this.mongoShardProcess.HasExited)
                        {
                            MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Running, "mongos");
                            Trace.TraceInformation("StartMongoS - Process has started successfully");
                        }
                        else
                        {
                            MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongos");
                            Trace.TraceError("StartMongoS - Process has failed to start");
                        }
                    }
                    catch (Exception)
                    {
                        MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongos");
                        Trace.TraceError("StartMongoS - Process has failed to start");
                    }
                }
                else
                {
                    Trace.TraceInformation("StartMongoS - The process is already started");
                }

                // Subscribe to the load balancer if not
                if (this.busy)
                {
                    this.busy = false;
                    Trace.TraceWarning("StartMongoS - Subscribe to the load balancer. Process should be reachable in less than 10 sec.");
                }

            }
            else
            {
                // If the MongoS isn't started, unsubscribe from the load balancer
                if (!MongoHelper.IsMongoRoutingInstanceStarted())
                {
                    this.busy = true;
                    Trace.TraceWarning("StartMongoS - Unsubscribing from the load balancer. Process won't be reachable until we re-subscribe");
                }

                Trace.TraceWarning("StartMongoS - One or more MongoC instances aren't started - MongoS instance couldn't be started. Retry will happen in a few sec.");
            }
        }

        /// <summary>
        /// MongoShard_Exited : Manage MongoS process end of life
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MongoShard_Exited(object sender, EventArgs e)
        {
            lock (MainSequencerLock)
            {
                this.busy = true;
                Trace.TraceWarning("MongoS_Exit - Unsubscribing the process from the load balancer");

                MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongos");
                Trace.TraceWarning("MongoS_Exit - MongoS process has been registred as 'stopped'");

                //This is valid for both S+C and S only role
                switch (CurrentConfigRoutingState)
                {
                    case RoutingConfigRoleState.Unknown:
                        break;
                    case RoutingConfigRoleState.Starting:
                        break;
                    case RoutingConfigRoleState.Stopping:
                        break;
                    case RoutingConfigRoleState.Stopped:
                        break;
                    case RoutingConfigRoleState.MongoConfig_NotRunning:
                        break;
                    case RoutingConfigRoleState.MongoShard_NotRunning:
                        break;
                    case RoutingConfigRoleState.Nominal:
                        SetCurrentState(RoutingConfigRoleState.MongoShard_NotRunning);
                        break;
                    default:
                        break;
                }

            }
            
            //No relaunch of mongos, the main loop will do it
        }

        /// <summary>
        /// Manage MongoC process end of life
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void MongoConfiguration_Exited(object o, EventArgs args)
        {
            MongoHelper.RegisterInstanceOrUpdate(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongoc");
            Trace.TraceWarning("MongoC_Exit - MongoC process has been registred as 'stopped'");

            lock(MainSequencerLock)
            {
                if (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole == MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole)
                {
                    //S+C Role
                    switch (CurrentConfigRoutingState)
                    {
                        case RoutingConfigRoleState.Unknown:
                            break;
                        case RoutingConfigRoleState.Starting:
                            break;
                        case RoutingConfigRoleState.Stopping:
                            Trace.TraceInformation("MongoC_Exit : Since we are stopping, MongoC will not be restarted");
                            break;
                        case RoutingConfigRoleState.Stopped:
                            Trace.TraceInformation("MongoC_Exit : Since we are stopping, MongoC will not be restarted");
                            break;
                        case RoutingConfigRoleState.MongoConfig_NotRunning:
                            //doing nothing, Main loop will restart mongoc
                            break;
                        case RoutingConfigRoleState.MongoShard_NotRunning:
                            SetCurrentState(RoutingConfigRoleState.MongoConfig_NotRunning);
                            break;
                        case RoutingConfigRoleState.Nominal:
                            SetCurrentState(RoutingConfigRoleState.MongoConfig_NotRunning);
                            break;
                        default:
                            Trace.TraceError("MongoC_Exit : Unknwon state : doing nothing");
                            break;
                    }
                }
                else
                {
                    //S only Role
                    //We are not supposed to be here
                }
            }
            

            
        }

        /// <summary>
        /// ON STOPPING
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RoleEnvironment_Stopping(object sender, RoleEnvironmentStoppingEventArgs e)
        {
            lock (MainSequencerLock)
            {
                // Flag the instance as stopping
                SetCurrentState(RoutingConfigRoleState.Stopping);

                Trace.TraceWarning("Role environment stopping notification received - Instance will stop in a few minutes");

                Trace.TraceWarning("The instance is stopping : shutting down MongoS process");
                MongoHelper.Shutdown(MongoDBAzurePlatform.Instance.MyMongoSAddress);

                Trace.TraceInformation("The instance is stopping : updating MongoS process status to 'stopped'");
                MongoHelper.UpdateInstance(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongos");

                if (MongoDBAzurePlatform.Instance.MyFunctionnalRoutingRole == MongoDBAzurePlatform.FunctionnalRoutingRole.RoutingAndConfigRole)
                {
                    Trace.TraceWarning("The instance is stopping : shutting down MongoC process");
                    MongoHelper.Shutdown(MongoDBAzurePlatform.Instance.MyMongoCAddress);
                    try
                    {
                        while (!this.mongoConfigProcess.HasExited)
                            Thread.Sleep(50);
                    }
                    catch (Exception)
                    {
                        // InvalidOperationException could be throw when There is no process associated with the object. 
                        // http://msdn.microsoft.com/en-us/library/system.diagnostics.process.hasexited.aspx
                    }
                    Trace.TraceInformation("The instance is stopping : updating MongoC instance status to 'stopped'");
                    MongoHelper.UpdateInstance(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped, "mongoc");

                    Trace.TraceInformation("The instance is stopping : Unmounting MongoC data drive");
                    try
                    {
                        mongoDrive.Unmount();
                    }
                    catch (Exception) { }
                }
                Trace.TraceInformation("The instance is stopping : end of operations");
            }
        }

        /// <summary>
        /// ROLE CONFIGURATION CHANGING
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RoleEnvironment_Changing(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If a configuration setting is changing
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                if (e.Changes.OfType<RoleEnvironmentConfigurationSettingChange>().Any(s => s.ConfigurationSettingName != "AutoRestart"))
                {
                    Trace.TraceWarning("RoleEnvironment_Changing - Restarting instance due to settings changes");
                    // Set the Cancel property of RoleEnvironmentChangingEventArgs to true to take the instance offline, apply the configuration change, and then bring the instance back online.
                    e.Cancel = true;
                }
                else
                {
                    Trace.TraceInformation("RoleEnvironment_Changing - Canceling the instance restart due to settings changes because only 'AutoRestart' setting has been modified");
                    e.Cancel = false;
                }
            }
        }

        /// <summary>
        /// STATUS CHECK : Control the avaibility of this instance for the WindowsAzure load balancer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RoleEnvironment_StatusCheck(object sender, RoleInstanceStatusCheckEventArgs e)
        {
            if (this.busy)
            {                
                e.SetBusy();
                Trace.TraceWarning("StatusCheck - The status of the role instance has been set to busy - Instance is unsubscribed from the Azure load balancer");
            }
        }

        /// <summary>
        /// ON STOP
        /// </summary>
        public override void OnStop()
        {
            lock (MainSequencerLock)
            {
                SetCurrentState(RoutingConfigRoleState.Stopped);
            }
            base.OnStop();
        }
    }
}
