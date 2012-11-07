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
using Microsoft.WindowsAzure.ServiceRuntime;
using Helpers.Mongo;
using Helpers.Azure;

namespace vNext.AzureTools.UpdateHosts
{
    class Program
    {
        private static readonly string hostsFilePath;

        static void Main(string[] args)
        {
            string[] words = new string[] { "toto", "titi", string.Empty, "lala", string.Empty };
            var orderedWords = words.OrderBy(x => x);
            foreach (string w in orderedWords)
            {
                Console.WriteLine(w);
            }

            Console.ReadLine();
            //if (RoleEnvironment.IsAvailable)
            //{
            //    if (args.Length >= 1 && args[0] == "forever")
            //    {
            //        Trace.TraceInformation("Starting endless roles/instances endpoints synchronization to local hosts file");
            //        while (true)
            //        {
            //            UpdateHostsFile(GetEndpointsInfo());
            //            System.Threading.Thread.Sleep(2000);
            //        }
            //    }
            //    else
            //        UpdateHostsFile(GetEndpointsInfo());
            //}
            //else
            //{
            //    Trace.TraceError("Role Environment is not available ! Unable to update hosts file - exiting now");
            //    Environment.Exit(1);
            //}
        }

        static Program()
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

        public static void UpdateHostsFile(Dictionary<string, string> deploymentEndpoints)
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

                    //si c'est un commentaire (#) on prend la ligne
                    //si le hostname est vide on prend la ligne
                    //si notre config actuelle ne contient pas le hostname alors on l'ajoute, 
                    //si le hostname est déjà là on l'écrasera, donc on le prend pas en compte
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

        public static Dictionary<string, string> GetEndpointsInfo()
        {
            Dictionary<string, string> endpointInfos = new Dictionary<string, string>();
            try
            {
                Console.WriteLine("Retrieve Routing Role Ips");
                foreach (var role in RoleEnvironment.Roles.Where(x => x.Key.Contains("Routing")).Select(i => i.Value))
                {
                    foreach (var instance in role.Instances)
                    {
                        // i.Id : identifiant de la bécane
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
                Console.WriteLine("Retrieve Shard Role Ips");
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
    }
}


#region DEBUG VERSION FOR TESTING PURPOSES
//namespace vNext.AzureTools.UpdateHosts
//{
//    class Program
//    {
//        private static readonly string hostsFilePath;

//        static void Main(string[] args)
//        {
//#if !DEBUG
//            if (RoleEnvironment.IsAvailable)
//            {
//#endif
//                if (args.Length >= 1 && args[0] == "forever")
//                {
//                    Trace.TraceInformation("Starting endless roles/instances endpoints synchronization to local hosts file");
//                    while (true)
//                    {
//                        Debug.WriteLine("Checking roles/instances endpoints...");
//                        UpdateHostsFile(GetEndpointsInfo());
//                        System.Threading.Thread.Sleep(59000);
//                    }
//                }
//                else
//                {
//                    UpdateHostsFile(GetEndpointsInfo());
//                }
//#if !DEBUG
//            }

//            else
//            {
//                Trace.TraceError("Role Environment is not available ! Unable to update hosts file - exiting now");
//                Environment.Exit(1);
//            }
//#endif
//        }

//        static Program()
//        {
//            try
//            {
//#if DEBUG
//                hostsFilePath = @"d:\hosts";
//#else
//                hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.None), @"drivers\etc\hosts");
//#endif
//            }
//            catch (Exception)
//            {
//                hostsFilePath = @"d:\windows\System32\drivers\etc\hosts";
//            }
//            Debug.WriteLine("Hosts file path: " + hostsFilePath);
//        }

//        public static void UpdateHostsFile(Dictionary<string, string> deploymentEndpoints)
//        {
//            try
//            {
//                var hostsFileEntries = File.ReadAllLines(hostsFilePath);
//                List<string> newHostsEntries = new List<string>();

//                foreach (var entry in hostsFileEntries)
//                {
//                    string hostname;
//                    if (entry.Split(' ', '\t').Length > 1)
//                        hostname = entry.Split(' ', '\t')[1].ToLower();
//                    else
//                        hostname = "";

//                    //si c'est un commentaire (#) on prend la ligne
//                    //si le hostname est vide on prend la ligne
//                    //si notre config actuelle ne contient pas le hostname alors on l'ajoute, 
//                    //si le hostname est déjà là on l'écrasera, donc on le prend pas en compte
//                    if (entry.StartsWith("#") || string.IsNullOrEmpty(hostname) || !deploymentEndpoints.ContainsKey(hostname))
//                    {
//                        newHostsEntries.Add(entry);
//                    }
//                }
//                foreach (var endpoint in deploymentEndpoints)
//                {
//                    newHostsEntries.Add(string.Format("{1} {0}", endpoint.Key, endpoint.Value));
//                }

//                bool writeNeeded = false;
//                if (hostsFileEntries.Length != newHostsEntries.Count)
//                    writeNeeded = true;
//                else
//                {
//                    for (int i = 0; i < hostsFileEntries.Length; i++)
//                    {
//                        if (hostsFileEntries[i] != newHostsEntries[i])
//                        {
//                            writeNeeded = true;
//                            break;
//                        }
//                    }
//                }


//                if (writeNeeded)
//                {
//                    Trace.TraceWarning("Change detected in role/instances endpoints - updating local hosts file");
//                    Debug.WriteLine("Change detected in role/instances endpoints - updating local hosts file");
//                    File.WriteAllLines(hostsFilePath, newHostsEntries.ToArray<string>());
//                }
//                else
//                    Debug.WriteLine("OK - no changes to apply.");
//            }
//            catch (Exception e)
//            {
//                Trace.TraceError("UpdateHosts - an error occured : {0} - inner exception : {1}", e.Message, (e.InnerException == null ? string.Empty : e.InnerException.Message));
//                Debug.WriteLine("UpdateHosts - an error occured : {0} - inner exception : {1}", e.Message, (e.InnerException == null ? string.Empty : e.InnerException.Message));
//            }
//        }

//        public static Dictionary<string, string> GetEndpointsInfo()
//        {
//            Dictionary<string, string> endpointInfos = new Dictionary<string, string>();
//            try
//            {
//#if DEBUG
//                endpointInfos.Add("myhost1", "10.16.63.45");
//                endpointInfos.Add("myhost2", "10.16.63.46");
//                endpointInfos.Add("myhost3", "10.16.63.41");
//                endpointInfos.Add("myhost4", "10.16.63.48");
//                endpointInfos.Add("myhost5", "10.16.63.91");
//                endpointInfos.Add("myhost6", "10.16.63.40");
//                endpointInfos.Add("myhost7", "10.16.63.23");
//                endpointInfos.Add("myhost8", "10.16.63.78");
//                endpointInfos.Add("myhost9", "10.16.63.12");
//#else
//                foreach (var role in RoleEnvironment.Roles.Select(i => i.Value))
//                {
//                    foreach (var instance in role.Instances)
//                    {
//                        // i.Id : identifiant de la bécane
//                        foreach (var endpoint in instance.InstanceEndpoints.Values)
//                        {
//                            var ipAddress = endpoint.IPEndpoint.Address.ToString();
//                            if (ipAddress.StartsWith("10")) //We only take internal ips
//                            {
//                                if (!endpointInfos.ContainsKey(instance.Id.ToLower()))
//                                    endpointInfos.Add(instance.Id.ToLower(), ipAddress);
//                            }
//                        }
//                    }
//                }
//#endif
//            }
//            catch (Exception e)
//            {
//                Trace.TraceError("UpdateHosts - Unable to get Roles/Instances endpoints information : {0} - inner exception : {1}", e.Message, (e.InnerException == null ? string.Empty : e.InnerException.Message));
//                Debug.WriteLine("UpdateHosts - Unable to get Roles/Instances endpoints information : {0} - inner exception : {1}", e.Message, (e.InnerException == null ? string.Empty : e.InnerException.Message));
//            }
//            return endpointInfos;
//        }
//    }
//}
#endregion