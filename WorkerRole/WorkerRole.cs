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
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Helpers.Azure;
using Helpers.Mongo;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.Text;
using System.Collections.Generic;
using Helpers;
using System.IO;
using System.Threading.Tasks;

namespace ReplicaSetRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        #region Fields

        //Timeout allowed for mongoD to stop
        private readonly TimeSpan MongoDShutdownTimeout = new TimeSpan(0, 5, 0);   //1 min

        private Process mongoProcess = null;
        private CloudDrive mongoDrive = null;
        private CloudStorageAccount storageAccount = null;
        private CloudBlobClient blobClient = null;
        private object MainSequencerLock = new object();
        private string CurrentDBPath;
        private bool MongoDFirstStart;
        private ShardInfo shard = null;

        #endregion

        public WorkerRole()
        {
        }

        public bool IsVerbose { get { return RoleEnvironment.GetConfigurationSettingValue("Verbose") == "1"; } }

        public bool RedirectMongoOutputToTrace
        {
            get { return RoleEnvironment.GetConfigurationSettingValue("RedirectMongoOutputToTrace") == "1"; }
        }

        private ShardInfo PoolForDisk()
        {
            IEnumerable<ShardInfo> shards = MongoHelper.GetAllShardInfo();
            Stopwatch watch = new Stopwatch();
            foreach (ShardInfo shard in shards.OrderBy(x => x.Ip))
            {
                watch.Restart();
                #region Path for the storage
                try
                {
                    CurrentDBPath = this.MountDrive("MongoDbCache", shard.DrivePath);
                    if (!string.IsNullOrEmpty(CurrentDBPath))
                    {
                        Trace.TraceInformation("Instance start - Cloud drive {0} mounted successfully", shard.DrivePath);
                        return shard;
                    }
                }
                catch (Exception ex) 
                {
                    if (IsVerbose) Trace.TraceInformation(ex.ToString());
                }
                #endregion
                long ms = watch.ElapsedMilliseconds;
                Trace.TraceInformation("[PERFCOUNT] Trying to mount a shard took {0} ms", ms);
                if (MongoHelper.GetAllShardInfo().Any(x => string.IsNullOrEmpty(x.Ip)))
                {
                    Trace.TraceInformation("Detected an unmounted disk, will break current operation!");
                    break;
                }
            }

            return null;
        }


        public override void Run()
        {
            Task hostUpdaterTask = null;
            Trace.TraceInformation("Instance run - Entering main loop");
            while (true)
            {

                try
                {
                    bool isMainSequencerFreeze = bool.Parse(RoleEnvironment.GetConfigurationSettingValue("MainSequencerFreeze"));
                    bool hasDriveMounted = false;

                    // 1.We try to mount each disk declared in azure table
                    while (!hasDriveMounted)
                    {
                        shard = PoolForDisk();
                        hasDriveMounted = shard != null;

                        if (hasDriveMounted)
                            break;

                        Trace.TraceInformation("Instance run - Waiting for disk to mount");
                        Thread.Sleep(2000);
                    }

                    // 2. Update Entry in Azure Table
                    Task updateIp = new Task(new Action(() =>
                    {
                        bool ipHasBeenUpdated = false;
                        // We have mounted a drive, we must register this IP address
                        Trace.TraceInformation("Instance start - Registering IP");
                        while (!ipHasBeenUpdated)
                        {
                            //
                            ipHasBeenUpdated = MongoHelper.UpdateShardIpWithName(shard.RowKey);
                            if (ipHasBeenUpdated)
                            {
                                Trace.TraceInformation("Instance start - Registering successful");
                            }
                            else
                            {
                                Trace.TraceError("Instance start - Registering IP failed! will retry in 5s");
                            }

                            Thread.Sleep(5000);
                        }
                    }));

                    updateIp.Start();

                    hostUpdaterTask = new Task(new Action(() => { HostUpdater.Run(true); }));

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
                            //Serialize operations to avoid conflicting instructions with MongoD process
                            lock (MainSequencerLock)
                            {
                                if (isMainSequencerFreeze)
                                {
                                    Trace.TraceWarning("Instance run Main Loop - MainSequencerFreeze = True : Doing nothing");
                                }
                                else
                                {
                                    // Time for action, depending of our status
                                    //=========================================

                                    ReplicaSetRoleManager.ReplicaSetRoleState CurrentStatus = ReplicaSetRoleManager.GetState();
                                    if (ReplicaSetRoleManager.ReplicaSetRoleState.InstanceRunning != CurrentStatus)
                                    {
                                        bool autorestart = false;
                                        bool.TryParse(RoleEnvironment.GetConfigurationSettingValue("AutoRestart"), out autorestart);
                                        if (autorestart)
                                        {
                                            Trace.TraceInformation("MongoD is not running, will launch a process");
                                            LaunchMongoDProcess();
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError(string.Format("Instance run - Exception in Main loop : {0} ({1}) at {2}", ex.Message, ex.InnerException == null ? "" : ex.InnerException.Message, ex.StackTrace));
                        }
                        catch
                        {
                            Trace.TraceError("Uncaught exception in Main Sequencer");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error in main loop : " + ex.ToString());
                }
                catch
                {
                    Trace.TraceError("Uncaught exception in Main Loop");
                }
            }
        }

      
        /// <summary>
        /// LaunchMongoDProcess if it needs to
        /// </summary>
        private void CheckMongoDProcess()
        {
            //We should only relaunch it if we are not already running
            ReplicaSetRoleManager.ReplicaSetRoleState CurrentStatus = ReplicaSetRoleManager.GetState();
            if (CurrentStatus == ReplicaSetRoleManager.ReplicaSetRoleState.MongoDNotRunning)
            {
                //First Start
                if (MongoDFirstStart)
                {
                    Trace.TraceWarning("CheckMongoDProcess - The MongoD process will be started as a first time");
                    LaunchMongoDProcess();
                    MongoDFirstStart = false;
                }
                else
                {
                    //Its not the first start, see if we need to restart it

                    bool autoRestart = false;
                    bool parseResult = bool.TryParse(RoleEnvironment.GetConfigurationSettingValue("AutoRestart"), out autoRestart);

                    if (parseResult)
                    {
                        if (autoRestart)
                        {
                            Trace.TraceInformation("MongoD_Exit - The Autorestart flag is active");
                            Trace.TraceWarning("MongoD_Exit - The MongoD process will be (re)started");
                            LaunchMongoDProcess();
                        }
                        else
                        {
                            Trace.TraceWarning("MongoD_Exit - The MongoD process is NOT running AND autorestart is inactive => MongoD will NOT be restarted");
                        }
                    }
                    else
                    {
                        Trace.TraceError("MongoD_Exit - Unable to get the AutoRestart flag value. Doing nothing.");
                    }
                }


            }
        }

        /// <summary>
        /// Start the MongoDProcess
        /// </summary>
        private void LaunchMongoDProcess()
        {
            Trace.TraceInformation("Instance run - Attempting to start MongoD process");
            // Get endpoint
            NodeEndPoint replicaEndpoint = MongoDBAzurePlatform.Instance.MyMongoDAddress;

            mongoProcess = ProcessTools.StartProcessWindow(MongoDBAzurePlatform.Instance.MongoExePath,
                                                        "mongod.exe",
                                                        MongoHelper.GetArgumentsForMongoDStandAlone(
                //TODO : CurrentDbPath must be set to Azure drive letter M:
                                                        @"M:\",
                                                        replicaEndpoint.Port.ToString()),
                                                        true,
                                                        false,
                                                        Mongod_Exited
                                                    );

            Thread.Sleep(1000); //sleep to allow the process to start up correctly before checking its state

            try
            {
                if (!this.mongoProcess.HasExited)
                {
                    Trace.TraceInformation("Instance start - MongoD process has started successfully");
                    //ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.ReplicaSetConfigurationInProgress);
                    ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceRunning);
                }
                else
                {
                    Trace.TraceError("Instance start - MongoD process has failed to start");
                }
            }
            catch (Exception)
            {
                Trace.TraceError("Instance start - MongoD process has failed to start");
            }
        }


        #region ReplicaSet

        /// <summary>
        /// Returns current ReplicaSet infos
        /// </summary>
        /// <returns></returns>
        ReplicaSetStatus GetReplicaSetStatus()
        {
            return MongoHelper.GetReplicaSetStatus(MongoDBAzurePlatform.Instance.MyMongoDAddress);
        }

        private void CheckReplicaSetConfig()
        {
            //Check first if config is not empty
            ReplicaSetStatus status = GetReplicaSetStatus();
            if (status == null)
                return;


            var container = blobClient.GetContainerReference("configuration");
            container.CreateIfNotExist();

            if (status.IsEmptyConfig)
            {
                if (MongoDBAzurePlatform.Instance.MyFunctionnalDataRole == MongoDBAzurePlatform.FunctionnalDataRole.HiddenMemberAzureDrive)
                {
                    Trace.TraceInformation("CheckReplicaSetConfig : a Initialisation is needed but we are a hidden member. Doing nothing");
                }
                else
                {
                    //Replicaset is not initialized.
                    using (var BlobLease = new smarx.WazStorageExtensions.AutoRenewLease(container.GetBlobReference(string.Format("{0}-reconfigInProgress", RoleEnvironment.CurrentRoleInstance.Id))))
                    {
                        if (BlobLease.HasLease)
                        {
                            Trace.TraceInformation("CheckReplicaSetConfig : Got Reconfiguration Lease, configuring");
                            // inside here, this instance has exclusive access

                            this.ConfigureReplicaSet(false);

                            //Let mongod handle the change
                            Thread.Sleep(5000);
                        }
                        else
                        {
                            Trace.TraceInformation("CheckReplicaSetConfig : could not get Reconfiguration Lease, another instance is using it");
                        }
                    }
                }
            }
            else
            {
                Trace.TraceInformation("CheckReplicaSetConfig - Checking for network configuration changes...");

                // Check if the endpoint have new ip address or port number
                if (!IsCurrentConfigurationCorrect(status))
                {
                    if (MongoDBAzurePlatform.Instance.MyFunctionnalDataRole == MongoDBAzurePlatform.FunctionnalDataRole.HiddenMemberAzureDrive)
                    {
                        Trace.TraceWarning("CheckReplicaSetConfig - Network configuration has changed, but we are a hidden member, doing nothing");
                    }
                    else
                    {
                        Trace.TraceWarning("CheckReplicaSetConfig - Network configuration has changed, replicaSet will reconfigure");

                        //We have network modifications, we need to reconfigure the ReplicaSet
                        using (var BlobLease = new smarx.WazStorageExtensions.AutoRenewLease(container.GetBlobReference(string.Format("{0}-reconfigInProgress", RoleEnvironment.CurrentRoleInstance.Id))))
                        {
                            if (BlobLease.HasLease)
                            {
                                Trace.TraceInformation("CheckReplicaSetConfig : Got Reconfiguration Lease, configuring");
                                // inside here, this instance has exclusive access

                                this.ConfigureReplicaSet(true);

                                //Let mongod handle the change
                                Thread.Sleep(5000);
                            }
                            else
                            {
                                Trace.TraceInformation("CheckReplicaSetConfig : could not get Reconfiguration Lease, another instance is using it");
                            }
                        } // lease is released here
                    }
                }
                else
                {
                    Trace.TraceInformation("CheckReplicaSetConfig - No network configuration changes found");
                    ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceRunning);
                }
            }

        }

        private bool IsCurrentConfigurationCorrect(ReplicaSetStatus CurrentReplicaSetStatus)
        {
            List<NodeEndPoint> ThisRSendpoints = MongoDBAzurePlatform.Instance.GetReplicaSetMembers(RoleEnvironment.CurrentRoleInstance.Id);
            return MongoHelper.AreReplicaSetConfigEqual(CurrentReplicaSetStatus, ThisRSendpoints);
        }

        void ConfigureReplicaSet(bool isReconfiguration)
        {
            try
            {

                ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.ReplicaSetConfigurationInProgress);
                Trace.TraceInformation(string.Format("Initialization of replicaSet configuration"));

                //Trying to find primary if any
                ReplicaSetNode Primary = null;
                try
                {
                    ReplicaSetStatus s = GetReplicaSetStatus();
                    Primary = s.Members.FirstOrDefault(mbox => mbox.State == NodeState.Primary);
                }
                catch (Exception) { }
                NodeEndPoint target;

                //if (Primary != null)
                //{
                //    //Choosing primary
                //    target = new NodeEndPoint(Primary.Adress, Int32.Parse(Primary.Port));
                //}
                //else
                //{
                //    //Primary not found, trying with local
                //    target = MongoDBAzurePlatform.Instance.MyMongoDAddress;
                //}

                //V3 : only the current instance is primary
                target = MongoDBAzurePlatform.Instance.MyMongoDAddress;

                Trace.TraceInformation("DEBUG - Calling MongoHelper.ConfigureReplicaSet - nodeEndpoint : {0} - ReplicaName : {1} - Nb of members :  {2} - isReconfig : {3}",
                                       target.ToString(),
                                        RoleEnvironment.CurrentRoleInstance.Id,
                                        MongoDBAzurePlatform.Instance.GetReplicaSetMembers(RoleEnvironment.CurrentRoleInstance.Role.Name).Count,
                                        isReconfiguration);

                //bool isConfigured = MongoHelper.ConfigureReplicaSet(target, RoleEnvironment.CurrentRoleInstance.Role.Name,
                //                                    RoleEnvironment.GetConfigurationSettingValue("ReplicaSetName"), 
                //                                    MongoDBAzurePlatform.Instance.GetReplicaSetMembers(RoleEnvironment.CurrentRoleInstance.Role.Name),
                //                                    isReconfiguration);

                //V3 : sets the replica with self only
                bool isConfigured = MongoHelper.ConfigureReplicaSet(target, RoleEnvironment.CurrentRoleInstance.Role.Name,
                                                    RoleEnvironment.CurrentRoleInstance.Id,
                                                    new List<NodeEndPoint>() { target },
                                                    isReconfiguration);

                if (isConfigured) //Reconfiguration is OK
                {
                    Trace.TraceInformation(string.Format("The replicaSet {0} has been configured successfully", RoleEnvironment.CurrentRoleInstance.Id));
                    ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceRunning);
                }
                else //Reconfiguration is wrong, we release the lock in order to let another role instance reconfigure the ReplicaSet
                {
                    Trace.TraceError(string.Format("An error occured during the replicaSet configuration, the replicaSet isn't configured."));
                }
            }
            catch (Exception ex)
            {
                // Si la commande a déjà été lancé une première fois, une exception est levé
                if (ex != null && ex.Message.Contains("already initialized"))
                {
                    Trace.TraceWarning(string.Format("The replicaSet configuration has already been initialized by another role instance."));
                    ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceRunning);
                }
                else
                {
                    Trace.TraceError(string.Format("An error occured during the replicaSet configuration : {0} - stacktrace : {1}", ex != null ? ex.Message : "(Exception message was empty)", ex.StackTrace));
                }
            }
        }

        #region vieuw trucs
        //private void SetReplicaSetConfigurationStatus(bool success)
        //{
        //    if (blobClient == null)
        //        blobClient = storageAccount.CreateCloudBlobClient();

        //    var container = blobClient.GetContainerReference("configuration");
        //    container.CreateIfNotExist();
        //    var blob = container.GetBlobReference(RoleEnvironment.GetConfigurationSettingValue("ReplicaSetName"));

        //    if (success)
        //    {
        //        // Replicaset is configured
        //        blob.UploadText("");
        //    }
        //    else
        //    {
        //        // An error occurred while trying to configure of the replicaSet
        //        // We delete the existing blob
        //        blob.DeleteIfExists();
        //    }
        //}

        //private bool IsReplicaSetConfigured()
        //{
        //    if (blobClient == null)
        //        blobClient = storageAccount.CreateCloudBlobClient();

        //    var container = blobClient.GetContainerReference("configuration");
        //    container.CreateIfNotExist();

        //    bool isExist = container.GetBlobReference(RoleEnvironment.GetConfigurationSettingValue("ReplicaSetName")).Exists();

        //    return isExist;
        //}

        //private bool IsGoodInstanceToLaunchReconfiguration()
        //{
        //    if (blobClient == null)
        //        blobClient = storageAccount.CreateCloudBlobClient();

        //    var container = blobClient.GetContainerReference("configuration");
        //    container.CreateIfNotExist();

        //    bool isExist = container.GetBlobReference(string.Format("{0}-reconfigInProgress", RoleEnvironment.GetConfigurationSettingValue("ReplicaSetName"))).Exists();
        //    if (isExist)
        //    {
        //        var blob = container.GetBlobReference(string.Format("{0}-reconfigInProgress", RoleEnvironment.GetConfigurationSettingValue("ReplicaSetName")));
        //        if (blob.DownloadText() == RoleEnvironment.CurrentRoleInstance.Id)
        //            return true;
        //        else
        //            return false;
        //    }
        //    return false;
        //}

        //private bool IsReconfigurationInProgress()
        //{
        //    if (blobClient == null)
        //        blobClient = storageAccount.CreateCloudBlobClient();

        //    var container = blobClient.GetContainerReference("configuration");
        //    container.CreateIfNotExist();

        //    bool isExist = container.GetBlobReference(string.Format("{0}-reconfigInProgress", RoleEnvironment.GetConfigurationSettingValue("ReplicaSetName"))).Exists();

        //    return isExist;
        //}
        #endregion

        #endregion

        public override bool OnStart()
        {
            Trace.TraceInformation("ReplicatSetRole entry point called - OnStart Method");
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 10 * 12 * Environment.ProcessorCount;

            //Disable small packet assembling, reducing network lattency
            ServicePointManager.UseNagleAlgorithm = false;

            //Enable TCP KeepAlive 2h, 1s
            ServicePointManager.SetTcpKeepAlive(true, 2 * 3600 * 1000, 1000);

            #region Diagnostic Monitor configuration

            Trace.TraceInformation("Instance start - Configuring Diagnostic Monitor");
            try
            {
                var cfg = DiagnosticMonitor.GetDefaultInitialConfiguration();
                //cfg.Logs.ScheduledTransferLogLevelFilter = LogLevel.Information;
                //cfg.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);

                //cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
                //{
                //    CounterSpecifier = @"\Processor(_Total)\% Processor Time",
                //    SampleRate = TimeSpan.FromSeconds(10d)
                //});
                //cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
                //{
                //    CounterSpecifier = @"\Memory\Available Bytes",
                //    SampleRate = TimeSpan.FromSeconds(10d)
                //});
                //cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
                //{
                //    CounterSpecifier = @"\PhysicalDisk(_Total)\% Idle Time",
                //    SampleRate = TimeSpan.FromSeconds(10d)
                //});
                //cfg.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration
                //{
                //    CounterSpecifier = @"\PhysicalDisk(_Total)\Avg. Disk Queue Length",
                //    SampleRate = TimeSpan.FromSeconds(10d)
                //});

                //cfg.PerformanceCounters.ScheduledTransferPeriod = TimeSpan.FromMinutes(1d);

                DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", cfg);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            #endregion


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
                        string[] configNotImpacting = new string[] { "AutoRestart", "Verbose", "MainSequencerFreeze" };
                        if (!configNotImpacting.Contains(configName))
                        {
                            // The corresponding configuration setting has changed, propagate the value
                            if (!configSetter(RoleEnvironment.GetConfigurationSettingValue(configName)))
                            {
                                // In this case, the change to the storage account credentials in the
                                // service configuration is significant enough that the role needs to be
                                // recycled in order to use the latest settings. (for example, the
                                // endpoint has changed)
                                Trace.TraceInformation("Request recycle due to configuration changes");
                                RoleEnvironment.RequestRecycle();
                            }
                        }
                    }
                };
            });

            #endregion

            //Init Storage
            storageAccount = CloudStorageAccount.FromConfigurationSetting("MongoDbData");
            blobClient = storageAccount.CreateCloudBlobClient();


            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += new EventHandler<RoleEnvironmentChangingEventArgs>(RoleEnvironment_Changing);
            RoleEnvironment.Stopping += new EventHandler<RoleEnvironmentStoppingEventArgs>(RoleEnvironment_Stopping);

            Trace.TraceInformation(string.Format("Instance Start - Registering the MongoD instance as 'Starting', my IP is {0}", MongoDBAzurePlatform.Instance.MyMongoDAddress.ToString()));
            ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceStarting);

            //Start MongoDB performance counters collection
            Trace.TraceInformation("Instance start - Starting custom performance counters");
            MongoDB.PerformanceCounters.PerformanceMonitor.Start(MongoDBAzurePlatform.Instance.MyMongoDAddress.Host, MongoDBAzurePlatform.Instance.MyMongoDAddress.Port, int.Parse(RoleEnvironment.GetConfigurationSettingValue("PerfCountersSamplingIntervalInMs")));

            ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.PreparingData);
            MongoDFirstStart = true;
            return base.OnStart();
        }

        private void RemoveIpMapping()
        {
            Trace.TraceInformation("Instance stop - The role instance has stopped, Remove Ip from reference table");
            if (shard != null)
            {
                int retry = 3;
                while (retry > 0 && !MongoHelper.UpdateShardRemoveIpWithName(shard.RowKey))
                {
                    Thread.Sleep(100);
                }
            }
            else
            {
                Trace.TraceInformation("ShardInfo was null, could not update Ip from reference table");
            }
        }
        public override void OnStop()
        {
            lock (MainSequencerLock)
            {

                MongoHelper.UpdateInstance(MongoHelper.RoutingConfigRoleMongoProcessState.Stopped);
                ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceStopped);
                RemoveIpMapping();
            }
            base.OnStop();
        }

        #region Private Methods

        private void Mongod_Exited(object o, EventArgs args)
        {
            ReplicaSetRoleManager.ReplicaSetRoleState CurrentStatus = ReplicaSetRoleManager.GetState();
            if (CurrentStatus == ReplicaSetRoleManager.ReplicaSetRoleState.InstanceStopping || CurrentStatus == ReplicaSetRoleManager.ReplicaSetRoleState.InstanceStopped)
            {
                //If we are going down, do not go back to MongoDNotRunning    
                Trace.TraceInformation("MongoD_Exit - MongoD process has exit (going down)");
            }
            else
            {
                Trace.TraceWarning("MongoD_Exit - MongoD process has been registred as 'stopped'");
                ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.MongoDNotRunning);
            }

            //The mail loop will restart the process if necessary
        }

        private string MountDrive(string localCacheName, string blobName)
        {
            //Trace.TraceInformation("MountDrive - Creating drive " + blobName);
            string path = "";
            try
            {
                // Get the local cache for the cloud drive
                LocalResource localCache = RoleEnvironment.GetLocalResource(localCacheName);

                // we'll use all the cache space we can (note: InitializeCache doesn't work with trailing slash)
                CloudDrive.InitializeCache(localCache.RootPath.TrimEnd('\\'), localCache.MaximumSizeInMegabytes);


                // the container that our dive is going to live in
                CloudBlobContainer drives = blobClient.GetContainerReference(RoleEnvironment.GetConfigurationSettingValue("ContainerName"));

                // create blob container (it has to exist before creating the cloud drive)
                try { drives.CreateIfNotExist(); }
                catch (Exception driveEx) { if (IsVerbose) Trace.TraceInformation(driveEx.ToString()); }

                // get the url to the vhd page blob we'll be using
                var blob = blobClient.GetContainerReference(RoleEnvironment.GetConfigurationSettingValue("ContainerName")).GetPageBlobReference(blobName);
                var vhdUrl = blob.Uri.ToString();

                mongoDrive = storageAccount.CreateCloudDrive(vhdUrl);

                try
                {
                    if (mongoDrive.CreateIfNotExist(localCache.MaximumSizeInMegabytes))
                    {
                        //mongoDrive.Create(localCache.MaximumSizeInMegabytes);
                        Trace.TraceInformation("MountDrive - Drive created");
                    }
                }
                catch (CloudDriveException cloudEx)
                {
                    // this exception can be thrown if the drive already exists
                    if (IsVerbose) Trace.TraceInformation(cloudEx.Message);
                }

                path = mongoDrive.Mount(0, DriveMountOptions.None) + @"\";
                try
                {
                    if (!path.ToLower().Contains("m:"))
                    {
                        char letter = path.Split(':')[0].First();
                        Trace.TraceInformation("Azure Drive is mounted on {0}, will run diskpart to mount it on M:", letter);
                        MongoDriverHelper.RunDiskPart('M', letter);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error in diskpart failed for {0}\r\n{1}" + path, ex);
                    return string.Empty;
                }
            }
            catch (Exception exception)
            {
                if (IsVerbose)
                {
                    Trace.TraceError("MountDrive Exception : " + exception.ToString());
                }
                return string.Empty;
            }

            return path;
        }

        private void RoleEnvironment_Stopping(object sender, RoleEnvironmentStoppingEventArgs e)
        {
            lock (MainSequencerLock)
            {
                Trace.TraceWarning("Role environment stopping notification received - Instance will stop in a few minutes");
                ReplicaSetRoleManager.SetState(ReplicaSetRoleManager.ReplicaSetRoleState.InstanceStopping);

                // On sait qu'on va s'arreter, désinscription auprès de la config pour sortir du replicaset et shard
                do
                {
                    Trace.TraceInformation("Instance stopping - Stopping MongoD process...");
                    MongoHelper.Shutdown(MongoDBAzurePlatform.Instance.MyMongoDAddress);
                    try
                    {
                        Stopwatch stopWatch = new Stopwatch();
                        stopWatch.Start();

                        //Trying to stop mongoD. Sometimes mongoD is deaf so if it did not stop after the defined timeout, we try again
                        while (!this.mongoProcess.HasExited && (stopWatch.Elapsed < MongoDShutdownTimeout))
                            Thread.Sleep(50);

                        if (!this.mongoProcess.HasExited)
                        {
                            Trace.TraceWarning("Instance stopping - MongoD did not want to stop. Retrying...");
                        }
                    }
                    catch (Exception ee)
                    {
                        // InvalidOperationException could be throw when There is no process associated with the object. 
                        // http://msdn.microsoft.com/en-us/library/system.diagnostics.process.hasexited.aspx
                        Trace.TraceError("Instance stopping - Exception while stopping MongoD : " + ee.Message);
                    }
                } while (!this.mongoProcess.HasExited);
                Trace.TraceInformation("Instance stopping - MongoD process exited successfully");

                Trace.TraceInformation("Instance stopping - Unmounting drive...");
                try
                {
                    if (mongoDrive != null)
                    {
                        mongoDrive.Unmount();

                    }
                    //TODO: We must broadcast a message to alert a pool instance to mount the disk.

                    Trace.TraceInformation("Instance stopping - Drive unmounted successfully, unregistering instance...");
                    RemoveIpMapping();
                }
                catch (Exception ee)
                {
                    Trace.TraceError(string.Format("Instance stopping - Error while umounting drive {0} : {1}", mongoDrive == null ? "(unknown)" : mongoDrive.LocalPath, ee.Message));
                }


                Trace.TraceInformation("Instance stopping - Instance unregistered successfully - end of operation");
            }
        }

        private void RoleEnvironment_Changing(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If a configuration setting is changing
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                string[] settingsNotImpacting = new string[] { "AutoRestart", "MainSequencerFreeze", "Verbose" };
                if (e.Changes.OfType<RoleEnvironmentConfigurationSettingChange>().Any(s => !settingsNotImpacting.Contains(s.ConfigurationSettingName)))
                {
                    Trace.TraceWarning("RoleEnvironment_Changing - Restarting instance due to settings changes");
                    // Set the Cancel property of RoleEnvironmentChangingEventArgs to true to take the instance offline, apply the configuration change, and then bring the instance back online.
                    e.Cancel = true;
                }
                else
                {
                    Trace.TraceInformation("RoleEnvironment_Changing - Canceling the instance restart due to settings changes because only 'AutoRestart' or 'MainSequencerFreeze' setting has been modified");
                    e.Cancel = false;
                }
            }
        }

        #endregion
    }
}
