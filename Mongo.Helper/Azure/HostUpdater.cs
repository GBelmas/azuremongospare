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
using System.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.IO;
using Helpers.Mongo;

namespace Helpers.Azure
{
    /// <summary>
    /// Provides methods to update the host.
    /// </summary>
    public static class HostUpdater
    {
        #region Fields
        private static readonly string hostsFilePath;
        #endregion Fields

        #region Constructors
        /// <summary>
        /// Initializes the hostsFilePath from the system variables. If a failure occurs, use the default path : d:\windows\System32\drivers\etc\hosts.
        /// </summary>
        static HostUpdater()
        {
            try
            {
                hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            }
            catch(Exception)
            {
                //Default path in Windows Azure
                hostsFilePath = @"d:\windows\System32\drivers\etc\hosts";
            }
        }
        #endregion Constructors

        #region Private Methods
        /// <summary>
        /// Updates the hosts file with the given endpoints.
        /// </summary>
        /// <param name="deploymentEndpoints">Endpoints to update in the file.</param>
        private static void UpdateHostsFile(Dictionary<string, string> deploymentEndpoints)
        {
            try
            {
                var hostsFileEntries = File.ReadAllLines(hostsFilePath);
                List<string> newHostsEntries = new List<string>();

                foreach (var entry in hostsFileEntries)
                {
                    string hostname;

                    if (entry.Split(' ', '\t').Length > 1)
                        hostname = entry.Split(' ', '\t')[1].ToLower();
                    else
                        hostname = "";

                    // if line is a comment, we take it.
                    // if the hostname is empty, we take it.
                    // if the current config does not contain the hostname, we add it, otherwise it will be overloaded and we don't take it into account.
                    if (entry.StartsWith("#") || string.IsNullOrEmpty(hostname) || !deploymentEndpoints.ContainsKey(hostname))
                    {
                        newHostsEntries.Add(entry);
                    }
                }

                foreach (var endpoint in deploymentEndpoints)
                {
                    newHostsEntries.Add(string.Format("{1} {0}", endpoint.Key, endpoint.Value));
                }

                bool writeNeeded = false;

                if (hostsFileEntries.Length != newHostsEntries.Count)
                    writeNeeded = true;
                else
                {
                    for (int i = 0; i < hostsFileEntries.Length; i++)
                    {
                        if (hostsFileEntries[i] != newHostsEntries[i])
                        {
                            writeNeeded = true;
                            break;
                        }
                    }
                }

                if (writeNeeded)
                {
                    Trace.TraceWarning("Change detected in role/instances endpoints - updating local hosts file");
                    File.WriteAllLines(hostsFilePath, newHostsEntries.ToArray<string>());
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("UpdateHosts - an error occured : {0} - inner exception : {1}", e.Message, (e.InnerException == null ? string.Empty : e.InnerException.Message));
            }
        }

        /// <summary>
        /// Retrieves the EndpointsInfo from roles, takes only internal ip addresses.
        /// </summary>
        /// <returns>A <see cref="Dictionnary"/> with all the endpoints.</returns>
        private static Dictionary<string, string> GetEndpointsInfo()
        {
            Dictionary<string, string> endpointInfos = new Dictionary<string, string>();
            try
            {
                Console.WriteLine("Retrieve Routing Role Ips");
                foreach (var role in RoleEnvironment.Roles.Where(x => x.Key.Contains("Routing")).Select(i => i.Value))
                {
                    foreach (var instance in role.Instances)
                    {
                        // i.Id : machine id.
                        foreach (var endpoint in instance.InstanceEndpoints.Values)
                        {
                            var ipAddress = endpoint.IPEndpoint.Address.ToString();
                            if (ipAddress.StartsWith("10")) //We only take internal ips
                            {
                                if (!endpointInfos.ContainsKey(instance.Id.ToLower()))
                                    endpointInfos.Add(instance.Id.ToLower(), ipAddress);
                            }
                        }
                    }
                }

                IEnumerable<ShardInfo> shards = MongoHelper.GetAllShardInfo();
                foreach (ShardInfo shard in shards)
                {
                    if (!string.IsNullOrEmpty(shard.Ip) && !string.IsNullOrEmpty(shard.RowKey))
                    {
                        endpointInfos.Add(shard.RowKey.ToLower(), shard.Ip);
                    }
                }

                // read in azure table
            }
            catch (Exception e)
            {
                Trace.TraceError("UpdateHosts - Unable to get Roles/Instances endpoints information : {0} - inner exception : {1}", e.Message, (e.InnerException == null ? string.Empty : e.InnerException.Message));
            }
            return endpointInfos;
        }
        #endregion Private Methods

        #region Public Methods
        /// <summary>
        /// Update every 2000 ms the host file.
        /// </summary>
        /// <param name="isForever"></param>
        public static void Run(bool isForever)
        {
            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    if (isForever)
                    {
                        Trace.TraceInformation("Starting endless roles/instances endpoints synchronization to local hosts file");
                        while (true)
                        {
                            UpdateHostsFile(GetEndpointsInfo());
                            System.Threading.Thread.Sleep(2000);
                        }
                    }
                    else
                        UpdateHostsFile(GetEndpointsInfo());
                }
                else
                {
                    Trace.TraceError("Role Environment is not available ! Unable to update hosts file - exiting now");
                    //Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Host Updater failed : " + ex.ToString());
            }
        }
        #endregion Public Methods
    }
}
