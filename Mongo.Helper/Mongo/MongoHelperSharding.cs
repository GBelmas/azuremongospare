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
using System.Net;
using MongoDB.Driver;
using System.Diagnostics;
using MongoDB.Bson;
using Microsoft.WindowsAzure.ServiceRuntime;
using MongoDB.Driver.Builders;

namespace Helpers.Mongo
{
    public partial class MongoHelper
    {

        public static List<ShardNode> GetShardConfigurationFromMongoC(NodeEndPoint nodeEndpoint)
        {
            List<ShardNode> r = new List<ShardNode>();

            try
            {
                // We connect to a mongoC
                MongoServer server = CreateMongoServer(nodeEndpoint);
                MongoDatabase db = server["config"];
                MongoCollection shards = db["shards"];

                var cursor = shards.FindAllAs(typeof(BsonDocument));
                foreach (var shard in cursor)
                {
                    ShardNode cfg = new ShardNode();
                    cfg.ID = (((BsonDocument)(shard)))["_id"].AsString;
                    cfg.Host = (((BsonDocument)(shard)))["host"].AsString;
                    r.Add(cfg);
                }
            }
            catch (Exception ee)
            {
                Trace.TraceError("GetShardConfigurationFromMongoC : Error while getting inner shard configuration : " + ee.Message + " " + ee.InnerException == null ? "" : ee.InnerException.Message);
                throw;
            }


            return r;
        }


        /// <summary>
        /// Returns what should be the mongoc configuration
        /// </summary>
        /// <returns></returns>
        public static List<ShardNode> GetShardConfigurationExpected()
        {
            List<ShardNode> ExpectedConfig = new List<ShardNode>();
            
            //TODO : return the list of active instances

            foreach (var instance in RoleEnvironment.Roles["MainShardRole"].Instances)
            {
                ShardNode node = new ShardNode()
                {
                    ID = instance.Id,
                    Host = instance.Id + "/" + instance.Id + ":" + instance.InstanceEndpoints["MongoDbEndpoint"].IPEndpoint.Port
                };

                ExpectedConfig.Add(node);
            }
            //ExpectedConfig.Add(new ShardNode() { ID = RoleEnvironment.CurrentRoleInstance.Id, Host = RoleEnvironment.CurrentRoleInstance.Id + ":" + RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["MongoDbEndpoint"].IPEndpoint.Port });

            return ExpectedConfig;

            //foreach (string replicaSetWorkerName in RoleEnvironment.GetConfigurationSettingValue("ReplicaSetNames").Split(';'))
            //{
            //    //For each ReplicaSet
            //    string[] names = replicaSetWorkerName.Split(':');
            //    string roleName = names[0];
            //    string replicaSetName = names[1];

            //    //Make a mongo-like host format : something like replica1/10.26.150.63:20003,10.26.156.91:20003,10.26.158.51:20003
            //    //Note : we have to ignore hidden members
            //    string Hostdesc = string.Format("{0}/{1}", replicaSetName, string.Join(",",
            //        MongoDBAzurePlatform.Instance.GetReplicaSetMembers(roleName).Where(i => MongoDBAzurePlatform.Instance.GetFunctionnalDataRole(i, roleName) != MongoDBAzurePlatform.FunctionnalDataRole.HiddenMemberAzureDrive).Select(i => i.ToString())));
            //    ExpectedConfig.Add(new ShardNode() { ID = replicaSetName, Host = Hostdesc });
            //}

            //return ExpectedConfig;
        }

        /// <summary>
        /// Returns true is MongoC need reconfig
        /// </summary>
        /// <param name="InternalConfig"></param>
        /// <param name="ExpectedConfig"></param>
        /// <returns></returns>
        private static bool CompareShardConfig(List<ShardNode> InternalConfig, List<ShardNode> ExpectedConfig)
        {
            //Now check if every already existing shard in mongoc is with the correct IP. If Expected have more, no problem, they will be added later normally
            foreach (ShardNode Cshard in InternalConfig)
            {
                //Find the corresponding shard in Expected
                ShardNode Realshard = ExpectedConfig.FirstOrDefault(s => s.ID == Cshard.ID);
                if (Realshard == null)
                {
                    //MongoC has reference to a replica that does not even exist in reality. How is that possible ? Maybe after few add and remove shard then crashed. 
                    //Let's assume that this mongoc has to be cleaned anyway
                    Trace.TraceWarning(string.Format("CompareShardConfig : MongoC shard {0}  does not even exist in reality. Manual check is recommended !", Cshard.Host));
                }
                else
                {
                    if (AreShardConfigEqual(Cshard.Host, Realshard.Host))
                    {
                        //Same IPs, cool
                    }
                    else
                    {
                        Trace.TraceWarning(string.Format("CompareShardConfig : Mismatch detected : MongoC shard {0} is different from what it should be : {1}", Cshard.Host, Realshard.Host));
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if mongoc shard config are equal. host1 and host2 must be in mongoc like format (replica1/10.26.150.63:20003,10.26.156.91:20003,10.26.158.51:20003)
        /// </summary>
        /// <param name="host1"></param>
        /// <param name="host2"></param>
        /// <returns></returns>
        public static bool AreShardConfigEqual(string host1, string host2)
        {
            try
            {
                //Detect IPs in each argument
                string[] host1IPs = host1.Split('/')[1].Split(',');
                string[] host2IPs = host2.Split('/')[1].Split(',');

                if (host1IPs.Length != host2IPs.Length)
                    return false;

                //Test if each IP in host1 is in host2
                foreach (string ip in host1IPs)
                {
                    if (host2IPs.FirstOrDefault(i => i == ip) == null)
                        return false;
                }
            }
            catch (Exception ee)
            {
                Trace.TraceError(string.Format("AreShardConfigEqual : Exception while comparing shard config {0} and {1} (wrong format ?) : {2}", host1, host2, ee.Message));
                throw;
            }

            return true;
        }

        

        /// <summary>
        /// Checks if mongoc data about shards are correct, ie ReplicaSet decribed have correct IPs. If no shard configuration, its ok, its gonna be configured later. If there is entries that 
        /// do not correspond to the replicaset IPs, then it means we have to alter mongoc config directly.
        /// </summary>
        /// <returns>True if a HardReconfig is needed</returns>
        public static bool IsMongoCNeedHardReconfig()
        {
            // Configuration is handled manually
            return true;
        }

    }
}
