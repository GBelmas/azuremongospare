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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Helpers.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System.Configuration;

namespace Helpers.Mongo
{
    public partial class MongoHelper
    {
        #region ReplicaSet

        /// <summary>
        /// Lance la configuration du replicaSet et de ses noeuds
        /// </summary>
        /// <param name="nodeEndpoint">A node in the replicaSet</param>
        /// <param name="replicaSetName">The replicaset Name</param>
        public static bool ConfigureReplicaSet(NodeEndPoint nodeEndpoint, string ReplicaSetRoleName, string replicaSetName, List<NodeEndPoint> nodes, bool isReconfigure)
        {
            BsonArray membersDoc = new BsonArray();


            

            // Get all the instances of the replica
            int serverId = 0;
            foreach (NodeEndPoint node in nodes)
            {

                string host = node.ToString();
                BsonDocument nodeDoc;

                ////Get role associated with this replicaSetName
                //if (MongoDBAzurePlatform.Instance.GetFunctionnalDataRole(node, ReplicaSetRoleName) == MongoDBAzurePlatform.FunctionnalDataRole.NormalMemberLocalDrive)
                //{
                //    //Normal member
                //     nodeDoc = new BsonDocument { { "_id", serverId }, { "host", host } };
                //}
                //else
                //{
                //    //Hidden member
                //    nodeDoc = new BsonDocument { { "_id", serverId }, { "host", host }, { "hidden" , true }, { "priority", 0 }};
                //}

                //V3
                nodeDoc = new BsonDocument { { "_id", serverId }, { "host", replicaSetName + ":" + node.Port } };

                membersDoc.Add(nodeDoc);

                serverId++;
            }

            var configDoc = new BsonDocument { { "_id", replicaSetName }, { "members", membersDoc } };

            // Reconfiguration of replicaSet : replSetReconfig
            // First configuration of replicaSet : replSetInitiate
            CommandDocument replicaSetCommand;
            if (isReconfigure)
                //replicaSetCommand = new CommandDocument { { "replSetReconfig", configDoc }, { "force", true } };
#warning : prendre en compte la version voir ci-dessous
//                db.adminCommand({ "replSetReconfig" : { "_id" : "replica2", "version" : 2, "members" : [{ "_id" : 0, "host" : "10.61.118.37:20001" }, { "_id" : 1, "host" : "10.61.102.163:20001" }, { "_id" : 2, "host" : "10.61.82.81:20001", "hidden" : true, "priority" : 0 }] } })

                replicaSetCommand = new CommandDocument { { "replSetReconfig", configDoc }};
            else
                replicaSetCommand = new CommandDocument { { "replSetInitiate", configDoc } };

            Trace.TraceInformation("Config MONGOD: " + replicaSetCommand.ToString());

            // Get the MongoServer to submit a command
            MongoServer server = CreateMongoServer(nodeEndpoint);

            CommandResult cmdResult = server.RunAdminCommand(replicaSetCommand);

            string response = "no response";
            try
            {
                response = cmdResult.Response.AsString;
            }
            catch (Exception)
            { }

            Trace.TraceInformation(string.Format("COMMAND RESULT FOR REPLICASET CONFIGURATION : Error message : {0} - is Ok : {1} - Response: {2}", cmdResult.ErrorMessage, cmdResult.Ok, response));

            // Check if there are errors
            if (!cmdResult.Ok)
                Trace.TraceError(string.Format("Command result error : {0}", cmdResult.ErrorMessage));
            
            return cmdResult.Ok;
        }

        public static string GetReplicaSetRoleNameFromReplicaSetName(string WantedReplicaSetName)
        {
            //TODO : Return the list of active instances... V3.

            foreach (string replicaSetWorkerName in RoleEnvironment.GetConfigurationSettingValue("ReplicaSetNames").Split(';'))
            {
                //For each ReplicaSet
                string[] names = replicaSetWorkerName.Split(':');
                string roleName = names[0];
                string replicaSetName = names[1];

                if (WantedReplicaSetName == replicaSetName)
                    return roleName;
            }

            return null;
        }

        /// <summary>
        /// Récupère le status des replicaSets
        /// </summary>
        /// <returns></returns>
        public static ReplicaSetStatus GetReplicaSetStatus(NodeEndPoint nodeEndpoint)
        {
            MongoServer server = CreateMongoServer(nodeEndpoint);
            
            try
            {
                var statusCommand = new CommandDocument { { "replSetGetStatus", 1 } };
                CommandResult result = server.RunAdminCommand(statusCommand);

                if (!result.Ok)
                    return null;

                ReplicaSetStatus status = new ReplicaSetStatus();

                foreach (var item in result.Response)
                {
                    switch (item.Name)
                    {
                        case "set":
                            status.ReplicasetName = item.Value.AsString;
                            break;
                        case "date":
                            status.Date = item.Value.AsDateTime;
                            break;
                        case "members":
                            foreach (var item2 in item.Value.AsBsonArray)
                            {
                                ReplicaSetNode node = new ReplicaSetNode();
                                foreach (var member in item2.AsBsonDocument)
                                {
                                    switch (member.Name)
                                    {
                                        case "_id":
                                            node.Id = member.Value.AsInt32;
                                            break;
                                        case "name":
                                            string[] fullAdress = member.Value.AsString.Split(':');
                                            if (fullAdress.Length > 1)
                                            {
                                                node.Adress = fullAdress[0];
                                                node.Port = fullAdress[1];
                                            }
                                            else
                                                node.Adress = member.Value.AsString;
                                            break;
                                        case "health":
                                            node.Health = member.Value.AsDouble == 0 ? "Down" : "Up";
                                            break;
                                        case "stateStr":
                                            node.StateStr = member.Value.AsString;
                                            break;
                                        case "state":
                                            node.State = ReplicaSetNode.GetStateFromInt(member.Value.AsInt32);
                                            break;
                                        case "optimeDate":
                                            node.OpTime = member.Value.AsDateTime;
                                            break;
                                        case "errmsg":
                                            node.ErrMsg = member.Value.AsString;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                status.Members.Add(node);
                            }
                            break;
                        default:
                            break;
                    }
                }

                return status;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("(EMPTYCONFIG)"))
                {
                    ReplicaSetStatus status = new ReplicaSetStatus();
                    status.IsEmptyConfig = true;
                    Trace.TraceWarning(string.Format("MongoHelper.GetStatus Exception : {0}", ex));
                    return status;
                }
                else
                {
                    Trace.TraceError(string.Format("MongoHelper.GetStatus Exception : {0}", ex));
                    return null;
                }
            }
        }

        public static bool IsPrimary(NodeEndPoint endpoint)
        {
            //V3 : self is always primary
            return true;
            //return CheckReplicaSetState(endpoint, NodeState.Primary);
        }

        public static bool IsPrimary()
        {
            //V3 : self is always primary
            return true;
            //return IsPrimary(MongoDBAzurePlatform.Instance.MyMongoDAddress);
        }

        public static bool IsSecondary(NodeEndPoint endpoint)
        {
            //V3 : self is always primary
            return false;
            //return CheckReplicaSetState(endpoint, NodeState.Secondary);
        }

        #region Private methods

        /// <summary>
        /// Vérifie l'état d'un noeud
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        private static bool CheckReplicaSetState(NodeEndPoint endpoint, NodeState state)
        {
            try
            {
                var status = GetReplicaSetStatus(endpoint);
                if (status == null)
                    return false;

                var member = status.Members.SingleOrDefault(m => endpoint == new NodeEndPoint(m.Adress,Int32.Parse(m.Port)));
                if (member != null && member.State == state)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("MongoHelper.CheckState : {0}", ex.Message));
                return false;
            }
            
        }

       

        /// <summary>
        /// Récupère la première instance qui est une DB
        /// </summary>
        /// <returns></returns>
        private static NodeEndPoint GetFirstNode(string roleName)
        {
            return MongoDBAzurePlatform.Instance.GetReplicaSetMembers(roleName).First();
        }

        public static MongoServer CreateMongoServer(NodeEndPoint endpoint)
        {
            return MongoServer.Create(GenerateConnectionString(endpoint));
        }

        private static string GenerateConnectionString(NodeEndPoint endpoint)
        {
            StringBuilder connectionStringBuilder = new StringBuilder();
            connectionStringBuilder.Append("mongodb://");

            connectionStringBuilder.Append(endpoint.ToString());

            connectionStringBuilder.Append("/?slaveOk=true");

            return connectionStringBuilder.ToString();  
        }

        #endregion

        #endregion

        

        #region Manage process

        

        public static string GetArgumentsForConfigMongo(string dbPath, string ip, string port)
        {
            return string.Format(@"--configsvr --dbpath ""{0}"" --port {1} --quiet", dbPath, port);
        }

        public static string GetArgumentsForRoutingMongo(string ip, string port, NodeEndPoint[] configDbInstances, string chunkSize = "64")
        {
            // Get all config mongo server
            StringBuilder configEndpointsBuilder = new StringBuilder();
            // sort config to have the same declaration order
            configDbInstances = configDbInstances.OrderBy(x => x.Host).ToArray();
            foreach (var configEndpoint in configDbInstances)
            {
                configEndpointsBuilder.Append(string.Format("{0},", configEndpoint.ToString()));
            }
            // Delete the last comma
            string configEndpoints = configEndpointsBuilder.ToString().Substring(0, configEndpointsBuilder.Length - 1);

            return string.Format(@"--configdb {0} --port {1} --chunkSize {2} --quiet", configEndpoints, port, chunkSize);
        }

        /// <summary>
        /// Get the command line arguments for a node in replicaSet
        /// </summary>
        /// <param name="dbPath"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="replicaSetName"></param>
        /// <param name="oplogSize">Size of the opLog in MB</param>
        /// <returns></returns>
        public static string GetArgumentsForReplicaSet(string dbPath, string ip, string port, string replicaSetName)
        {
            int opLogSize = 2048;
            // Get the opLog size (between 5 and 10% of the total disk space)
            try
            {
                opLogSize = int.Parse(RoleEnvironment.GetConfigurationSettingValue("OpLogSize"));
            }
            catch (Exception)
            { }

            //return string.Format(@"--shardsvr --dbpath ""{0}"" --port {1} --replSet {2} --oplogSize {3} --nohttpinterface --quiet", dbPath, port, replicaSetName, opLogSize.ToString());
            return string.Format(@"--dbpath ""{0}"" --port {1} --nohttpinterface --quiet", dbPath, port);
        }

        public static string GetArgumentsForMongoDStandAlone(string dbPath, string port)
        {
            return string.Format(@"--dbpath {0} --port {1} --nohttpinterface", dbPath, port);
        }

        public static string GetArgumentsForDump(string dumpPath, string ip, string port)
        {
            return string.Format(@"--host {0} -o ""{1}""", 
                                    string.Format("{0}:{1}", ip, port),
                                    dumpPath);
        }

        

        public static bool Shutdown(NodeEndPoint endpoint)
        {
            endpoint = new NodeEndPoint("127.0.0.1", endpoint.Port);
            try
            {
                MongoServer server = CreateMongoServer(endpoint);
                server.Shutdown();
                return true;
            }
            catch (EndOfStreamException)
            {
                // we expect an EndOfStreamException when the server shuts down so we ignore it   
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

       

        

        #endregion

        #region Azure Helpers

        private static CloudStorageAccount _account = null;
        private static CloudBlobContainer _container = null;
        private static string _dbConfigPath = null;
        private static TableHelper<Instance> _tableHelperInstances = null;
        private static TableHelper<ShardInfo> _tableHelperShardInfo = null;
        
        private static void InitializeTableHelper()
        {
            if (_tableHelperInstances == null)
                _tableHelperInstances =  new TableHelper<Instance>(GetSetting("TableInstanceName"), "MongoDbData");

            if (_tableHelperShardInfo == null)
                _tableHelperShardInfo = new TableHelper<ShardInfo>(GetSetting("TableShardInfoName"), "MongoDbData");
        }

        public static string GetSetting(string key)
        {
            string result = string.Empty;
            result = RoleEnvironment.GetConfigurationSettingValue(key);
            if (string.IsNullOrEmpty(result))
            {
                result = ConfigurationManager.AppSettings[key];
            }

            return result;
        }

        /// <summary>
        /// Register a new instance
        /// </summary>
        /// <param name="state"></param>
        /// <param name="mongoName"></param>
        public static void RegisterInstance(string state, string mongoName = null)
        {
            InitializeTableHelper();

            Instance instance = new Instance(RoleEnvironment.CurrentRoleInstance.Role.Name, RoleEnvironment.CurrentRoleInstance.Id)
            {
                State = state.ToString()
            };

            if (!string.IsNullOrEmpty(mongoName))
                instance.RowKey = GetRowKeyWithMongoName(mongoName, instance.RowKey);

            instance.FillEndpoints(RoleEnvironment.CurrentRoleInstance.InstanceEndpoints);

            _tableHelperInstances.AddItem(instance);
        }



        /// <summary>
        /// Get the rowKey with the mongoName (format : mongoName_instanceId)
        /// </summary>
        /// <param name="mongoName"></param>
        /// <param name="rowKey"></param>
        /// <returns>mongoName_instanceId</returns>
        private static string GetRowKeyWithMongoName(string mongoName, string rowKey)
        {
            return string.Format("{0}_{1}", mongoName, rowKey);
        }

        public static bool CreateShardInfo(string name, string vhd, string ip)
        {
            InitializeTableHelper();
            ShardInfo info = new ShardInfo(name, vhd, RoleEnvironment.CurrentRoleInstance.Id, ip);
            return _tableHelperShardInfo.AddItem(info);
        }

        public static ShardInfo GetShardInfoWithName(string shardName)
        {
            InitializeTableHelper();
            return _tableHelperShardInfo.GetItemById(shardName);
        }

        //Temporary
        public static void RegisterInstanceOrUpdate(RoutingConfigRoleMongoProcessState instanceState, string mongoName = null)
        {
            RegisterInstanceOrUpdate(instanceState.ToString(), mongoName);
        }

        /// <summary>
        /// Register or update (erase) the current instance
        /// </summary>
        /// <param name="instanceState"></param>
        /// <param name="mongoName"></param>
        public static void RegisterInstanceOrUpdate(string instanceState, string mongoName = null)
        {
            InitializeTableHelper();

            Instance instance = GetInstance(mongoName);
            if (instance == null)
                RegisterInstance(instanceState, mongoName);
            else
            {
                instance.State = instanceState.ToString();
                instance.FillEndpoints(RoleEnvironment.CurrentRoleInstance.InstanceEndpoints);
                _tableHelperInstances.UpdateItem(instance);
            }
        }

        public static bool UpdateShardRemoveIpWithName(string shardName)
        {
            InitializeTableHelper();
            ShardInfo shard = GetShardInfoWithName(shardName);
            if (shard == null)
            {
                //Shard info should always exist!
                Trace.TraceWarning("UpdateShardIpWithName : shard info is null for name " + shardName);
                return false;
            }
            else
            {
                string instanceName = string.Empty;
                string ip = string.Empty;
                shard.Ip = ip;
                shard.InstanceName = instanceName;
                int retry = 3;
                Trace.TraceInformation("RemovingIp : Removing Ip for ShardName {0}", shardName);
                while (!_tableHelperShardInfo.UpdateItem(shard) || retry > 0)
                {
                    retry--;
                    System.Threading.Thread.Sleep(500);
                }

                shard = GetShardInfoWithName(shardName);
                return shard.Ip == ip;
            }
        }

        public static bool UpdateShardIpWithName(string shardName)
        {
            InitializeTableHelper();
            ShardInfo shard = GetShardInfoWithName(shardName);
            if (shard == null)
            {
                //Shard info should always exist!
                Trace.TraceWarning("UpdateShardIpWithName : shard info is null for name "+ shardName);
                return false;
            }
            else
            {
                string instanceName = RoleEnvironment.CurrentRoleInstance.Id;
                string hostname = System.Net.Dns.GetHostName();
                string ip = System.Net.Dns.GetHostAddresses(hostname).Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).First().ToString();
                shard.Ip = ip;
                shard.InstanceName = instanceName;
                int retry = 3;
                Trace.TraceInformation("UpdateShardIpWithName : updating IP {0} for ShardName {1}", ip, shardName);
                while (!_tableHelperShardInfo.UpdateItem(shard) || retry > 0)
                {
                    retry--;
                    System.Threading.Thread.Sleep(500);
                }

                shard = GetShardInfoWithName(shardName);
                return shard.Ip == ip;
            }
        }

        public static IEnumerable<ShardInfo> GetAllShardInfo()
        {
            Console.WriteLine("Get All Shards");
            InitializeTableHelper();
            return _tableHelperShardInfo.GetAllItems();
        }

        /// <summary>
        /// Get the current instance
        /// </summary>
        /// <param name="mongoName"></param>
        /// <returns></returns>
        public static Instance GetInstance(string mongoName = null)
        {
            InitializeTableHelper();

            return _tableHelperInstances.GetItemById(RoleEnvironment.CurrentRoleInstance.Role.Name,
                                                        string.IsNullOrEmpty(mongoName) ? RoleEnvironment.CurrentRoleInstance.Id : GetRowKeyWithMongoName(mongoName, RoleEnvironment.CurrentRoleInstance.Id));
        }

        /// <summary>
        /// Get all the instances from the current role
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Instance> GetInstances()
        {
            InitializeTableHelper();

            return _tableHelperInstances.GetAllItems(RoleEnvironment.CurrentRoleInstance.Role.Name);
        }

        /// <summary>
        /// Update the state of the current instance
        /// </summary>
        /// <param name="state"></param>
        /// <param name="mongoName"></param>
        /// <returns></returns>
        public static bool UpdateInstance(RoutingConfigRoleMongoProcessState state, string mongoName = null)
        {
            try
            {
                Instance instance = GetInstance(mongoName);

                if (instance == null)
                    return false;

                instance.State = state.ToString();
                instance.FillEndpoints(RoleEnvironment.CurrentRoleInstance.InstanceEndpoints);

                return _tableHelperInstances.UpdateItem(instance);
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("MongoHelper.UpdateInstance Exception : : {0}", ex.Message));
                return false;
            }
        }

       

        /// <summary>
        /// Check if all mongoc instances are started and running
        /// </summary>
        /// <param name="count">Number of mongoc instances</param>
        /// <returns></returns>
        public static bool IsMongocConfigInstancesAllStarted()
        {
            int countNeeded = 3;
            try
            {
                // Get all instances of the current Role
                var instances = GetInstances();
                // Check if all instance (MongoC) have the Running state
                bool allStarted = (from i in instances.ToList()
                             where i.RowKey.Contains("mongoc")
                             && i.State == RoutingConfigRoleMongoProcessState.Running.ToString()
                                   select i).Count() >= countNeeded;

                return allStarted;
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("MongoHelper.IsMongocConfigInstancesAllStarted Exception : {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Check if the current instance have its mongos started
        /// </summary>
        /// <returns></returns>
        public static bool IsMongoRoutingInstanceStarted()
        {
            try
            {
                // Get the current instance
                var instance = GetInstance("mongos");
                if (instance == null)
                    return false;

                // Check if the instance is on running state or not
                return instance.State != RoutingConfigRoleMongoProcessState.Running.ToString() ? false : true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("MongoHelper.IsMongoRoutingInstanceStarted Exception : {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Copy files from the FileSystem to the Azure Storage
        /// </summary>
        /// <param name="path"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        public static bool CopyFilesToAzureStorage(string path, CloudBlobContainer container)
        {
            try
            {
                GetFilesRecursive(path, container);

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("MongoHelper.CopyFilesToAzureStorage Exception : {0}", ex.Message));
                return false;
            }
        }

        private static void GetDirectories(string path, CloudBlobContainer container)
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                GetFilesRecursive(dir, container);
            }
        }

        private static void GetFilesRecursive(string path, CloudBlobContainer container)
        {
            foreach (var item in Directory.GetFiles(path))
            {
                //the item is under the format F://folder/subfolder/filename.ext
                //Removing the drive letter first
                string blobName = item.Substring(4);
                var blob = container.GetBlobReference(blobName);
                try
                {
                    using (FileStream fileStream = new FileStream(item, FileMode.Open))
                    {
                        blob.UploadFromStream(fileStream);
                    }
                }
                catch (IOException ex)
                {
                    Trace.TraceError(string.Format("MongoHelper.GetFilesRecursive exception during the copy of the file : {0} - Exception message : {1}", item, ex != null ? ex.Message : "Unknown"));
                }
                catch (StorageClientException ex)
                {
                    Trace.TraceError(string.Format("MongoHelper.GetFilesRecursive exception during the copy of the file : {0} - Exception message : {1}", item, ex != null ? ex.Message : "Unknown"));
                }
            }

            GetDirectories(path, container);
        }

        /// <summary>
        /// Copy configuration files from Azure Storage to the config VHD
        /// </summary>
        /// <param name="dbConfigPath"></param>
        /// <param name="container"></param>
        /// <param name="account"></param>
        /// <returns></returns>
        public static bool CopyConfigFromAzureStorage(string dbConfigPath, CloudBlobContainer container, CloudStorageAccount account)
        {
            try
            {
                _account = account;
                _container = container;
                _dbConfigPath = dbConfigPath;

                // We delete the older files
                Trace.TraceInformation("Deleting the older config files");
                try
                {
                    DeleteFilesAndDirectories(dbConfigPath);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format("MongoHelper.CopyConfigFromAzureStorage Exception : {0}", ex.Message));
                }

                BlobRequestOptions options = new BlobRequestOptions();
                options.UseFlatBlobListing = true;

                GetBlobs(container.ListBlobs(options));
                
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("CopyConfigFromAzureStorage : Exception while Gettings blobs : {0} ({1}) ", ex.Message, ex.InnerException == null ? "" : ex.InnerException.Message));
                return false;
            }
        }

        /// <summary>
        /// Supprime tous les fichiers, dossiers et ss-dossiers à la racine du chemin
        /// </summary>
        /// <param name="path"></param>
        public static void DeleteFilesAndDirectories(string path)
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format("MongoHelper.DeleteFilesAndDirectories Exception on file delete: {0}", ex.Message));
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format("MongoHelper.DeleteFilesAndDirectories Exception on directory delete: {0}", ex.Message));
                }
            }
        }

        private static void GetBlobs(IEnumerable<IListBlobItem> blobs)
        {
            foreach (var blob in blobs)
            {
                try
                {
                    // Get the blob name
                    string blobName = blob.Uri.AbsolutePath.Replace(string.Format("{0}/", _account.Credentials.AccountName), string.Empty).Replace(string.Format("{0}/", _container.Name), string.Empty);
                    int slashPos = blobName.IndexOf('/') + 1;
                    int length = blobName.Length - slashPos;
                    blobName = blobName.Substring(slashPos, length);

                    // DONT copy the mongod.lock
                    if (blobName.Contains("mongod.lock")) continue;

                    string fileName = blobName.Replace("/", "\\");
                    fileName = Regex.Replace(fileName, @"[a-z]:\\\\", "");

                    // Get the fileSystem path
                    string path = Path.Combine(_dbConfigPath, fileName);

                    if (!string.IsNullOrEmpty(Path.GetExtension(path)))
                    {
                        using (FileStream stream = new FileStream(path, FileMode.Append))
                        {
                            _container.GetBlobReference(blobName).DownloadToStream(stream);
                        }
                    }
                    else
                    {
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);

                        GetBlobs(_container.GetDirectoryReference(blobName).ListBlobs());
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format("MongoHelper.GetBlobs Exception : {0}", ex.Message));
                }
            }
        }

        #endregion

       

        #region RoutingConfigRoleMongoProcessState

        public enum RoutingConfigRoleMongoProcessState
        {
            Running,
            Stopped
        }

        #endregion

        /// <summary>
        /// Compare the current RS (CurrentReplicaSetStatus) with what it should be ie the list of instances in our role (Endpoints)
        /// </summary>
        /// <param name="CurrentReplicaSetStatus"></param>
        /// <param name="Endpoints"></param>
        /// <returns></returns>
        public static bool AreReplicaSetConfigEqual(ReplicaSetStatus CurrentReplicaSetStatus, List<NodeEndPoint> Endpoints)
        {
            if (CurrentReplicaSetStatus.Members.Count != Endpoints.Count)
                return false;

            //Check  that every endpoints is in the replicatset config
            foreach (NodeEndPoint node in Endpoints)
            {
                if (CurrentReplicaSetStatus.Members.FirstOrDefault(m => node == new NodeEndPoint(m.Adress,Int32.Parse(m.Port))) == null)
                    return false;
            }

            return true;
        }
    }
}
