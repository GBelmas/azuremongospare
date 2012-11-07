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
using System.Linq;
using System.Text;

namespace Helpers.Mongo
{
    //On utilise pas cette fonction, c'est trop dangereux. L'admin doit le faire a la main
    ///// <summary>
    ///// Active tous les noeuds d'un replicaSet en mode sharding
    ///// </summary>
    ///// <param name="routingEndpoint"></param>
    ///// <param name="replicaSetName"></param>
    ///// <param name="nodesEndpoint"></param>
    //public static bool ConfigureShard(NodeEndPoint routingEndpoint, string replicaSetName, List<EndPoint> nodesEndpoint)
    //{
    //    // On se connecte sur un mongos (Routing)
    //    MongoServer server = CreateMongoServer(routingEndpoint);

    //    StringBuilder nodes = new StringBuilder();
    //    // pour chaque noeud, on va l'ajouter dans le Shard
    //    foreach (var nodeEndpoint in nodesEndpoint)
    //    {
    //        nodes.Append(string.Format("{0}:{1},", nodeEndpoint.Address, nodeEndpoint.Port));
    //    }
    //    // delete the last comma
    //    nodes = nodes.Remove(nodes.Length - 1, 1);

    //    var shardCommand = new CommandDocument { { "addshard", string.Format("{0}/{1}", replicaSetName, nodes.ToString()) } };

    //    var cmdResult = server.RunAdminCommand(shardCommand);

    //    // Check if there are errors
    //    if (!cmdResult.Ok)
    //        Trace.TraceError(string.Format("Configure Shard command result error message : {0}", cmdResult.ErrorMessage));

    //    return cmdResult.Ok;
    //}

    ///// <summary>
    ///// Active le sharding sur la base de données
    ///// </summary>
    ///// <param name="routingEndpoint"></param>
    ///// <param name="databaseName"></param>
    //public static void EnableShardingOnDatabase(IPEndPoint routingEndpoint, string databaseName)
    //{
    //    // On se connecte sur un mongos
    //    MongoServer server = CreateMongoServer(routingEndpoint);

    //    var enableShardCommand = new CommandDocument { { "enablesharding", databaseName } };
    //    server.RunAdminCommand(enableShardCommand);
    //}

    ///// <summary>
    //    /// Shutdown the localhost mongo process
    //    /// </summary>
    //    /// <param name="port">Port of the mongo process</param>
    //    /// <param name="seconds">Seconds to wait for the secondaries to catch up (Default is 60 seconds)</param>
    //    /// <param name="force">Force to quit when no secondary has been elected</param>
    //    /// <returns></returns>
    //    public static bool Shutdown(int port, int seconds = 60, bool force = true)
    //    {
    //        try
    //        {
    //            var shutdownCommand = new CommandDocument { { "shutdown", 1 }, { "timeoutSecs", seconds}, { "force" , force } };

    //            MongoServer server = CreateMongoServer(new IPEndPoint(IPAddress.Parse("127.0.0.1"), port));

    //            var result = server.RunAdminCommand(shutdownCommand);

    //            // Check if there are errors
    //            if (!result.Ok)
    //                Trace.TraceError(string.Format("ShutdownCommand result error : {0}", result.ErrorMessage));

    //            return result.Ok;
    //        }
    //        catch (EndOfStreamException) 
    //        {                    
    //            // we expect an EndOfStreamException when the server shuts down so we ignore it   
    //            return true;
    //        }
    //        catch (Exception ex)
    //        {
    //            Trace.TraceError(string.Format("MongoHelper.Shutdown Exception : : {0}", ex.Message));
    //            return false;
    //        }
    //    }

    /// <summary>
    /// "Hard" reconfig will update replicaSetIPs reference in mongoC process
    /// Do not use. !!! With version > 2.x, mongoc should detect the problem and update itself
    /// </summary>
    //public static void MongoConfigurationHardReconfig()
    //{
    //    Trace.TraceInformation("MongoConfigurationHardReconfig : Starting hard reconfig of MongoC shards...");

    //    List<ShardNode> InternalConfig = GetShardConfigurationFromMongoC(MongoDBAzurePlatform.Instance.MyMongoCAddress);
    //    //What should the correct ReplicaSetAddresses should be ?
    //    List<ShardNode> ExpectedConfig = GetShardConfigurationExpected();

    //    //Now check if every already existing shard in mongoc is with the correct IP. If Expected have more, no problem, they will be added later normally
    //    foreach (ShardNode Cshard in InternalConfig)
    //    {
    //        //Find the corresponding shard in Expected
    //        ShardNode Realshard = ExpectedConfig.FirstOrDefault(s => s.ID == Cshard.ID);
    //        if (Realshard == null)
    //        {
    //        }
    //        else
    //        {
    //            if (AreShardConfigEqual(Cshard.Host, Realshard.Host))
    //            {
    //                //Same IPs, cool, nothing to update
    //            }
    //            else
    //            {
    //                Trace.TraceWarning(string.Format("MongoConfigurationHardReconfig : Updating MongoC shard {0} to {1}", Cshard.Host, Realshard.Host));

    //                try
    //                {
    //                    // We connect to a mongoC
    //                    MongoServer server = CreateMongoServer(MongoDBAzurePlatform.Instance.MyMongoCAddress);
    //                    MongoDatabase db = server["config"];
    //                    MongoCollection shards = db["shards"];

    //                    var query = Query.EQ("_id", Realshard.ID);
    //                    var sortBy = SortBy.Null;
    //                    var update = Update.Set("host", Realshard.Host);
    //                    var result = shards.FindAndModify(query, sortBy, update, false);

    //                }
    //                catch (Exception ee)
    //                {
    //                    Trace.TraceError(string.Format("MongoConfigurationHardReconfig : Error while updating shard {0} => {1}  : " + ee.Message + " " + ee.InnerException == null ? "" : ee.InnerException.Message, Cshard.Host, Realshard.Host));
    //                    throw;
    //                }
    //            }
    //        }
    //    }

    //}


    //private void ShardingConfigurationIsSuccess(bool success)
    //    {
    //        try
    //        {
    //            if (success)
    //            {
    //                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    //                var container = blobClient.GetContainerReference("configuration");
    //                container.CreateIfNotExist();
    //                container.GetBlobReference(RoleEnvironment.CurrentRoleInstance.Role.Name).UploadText("");
    //            }
    //            else
    //            {
    //                // An error ocurred during the configuration of the shard
    //                // We delete the blob it exist
    //                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    //                var container = blobClient.GetContainerReference("configuration");
    //                container.CreateIfNotExist();

    //                container.GetBlobReference(RoleEnvironment.CurrentRoleInstance.Role.Name).DeleteIfExists();
    //            }
    //        }
    //        catch (Exception)
    //        {
    //            Trace.TraceError("An error occured while trying to flag the sharding configuration status");
    //        }
    //    }


    ///// <summary>
    ///// Return the primary endpoint of the replicaSet
    ///// </summary>
    ///// <param name="roleName">The name of the WorkerRole (ReplicaSet)</param>
    ///// <returns></returns>
    //public static RoleInstanceEndpoint GetPrimaryEndpoint(string roleName)
    //{
    //    return MongoDBAzurePlatform.Instance.ReplicaSetMembers.Where(m => m.Key == roleName).FirstOrDefault(r => MongoHelper.IsRightInstance(r.Id, "0")).InstanceEndpoints["MongoDbEndpoint"];
    //}

    /// <summary>
    /// Check if the current instance have new endpoint(s)
    /// </summary>
    /// <param name="endpointsToMonitor"></param>
    /// <returns></returns>
    //        public static List<string> HasChanges(params string[] endpointsToMonitor)
    //        {
    //            Instance instance = GetInstance();
    //            if (instance == null)
    //                return null;

    //            List<string> endpoints = new List<string>();
    //            foreach (var endpoint in instance.Endpoints.Split(';'))
    //            {
    //                string[] e = endpoint.Split('-');
    //                string endpointName = e[0];
    //                string[] a = e[1].Split(':');
    //                string ip = a[0];
    //                string port = a[1];

    //                string endpointToMonitor = endpointsToMonitor.SingleOrDefault(ee => ee == endpointName);
    //                // On vérifie que l'endpoint a toujours la meme ip et le meme port
    //                if (!string.IsNullOrEmpty(endpointToMonitor))
    //                {
    //#warning passage avec le mongoDBAzurePlatform ici ?
    //                    var instanceEndpoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[endpointToMonitor];
    //                    if (instanceEndpoint.IPEndpoint.Port.ToString() != port || instanceEndpoint.IPEndpoint.Address.ToString() != ip)
    //                        endpoints.Add(endpointToMonitor);
    //                }
    //            }

    //            return endpoints;
    //        }
}
