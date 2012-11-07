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
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Diagnostics;

namespace Helpers
{
    /// <summary>
    /// Manage IPs for all mongodb process. All reference to IP address should be taken from here and not directly from WA Endpoints. In Debug mode, this will provide
    /// hardcoded IP and ports to have all mongo process on different ports
    /// </summary>
    public class MongoDBAzurePlatform
    {
        public enum FunctionnalRoutingRole
        {
            RoutingAndConfigRole,
            RoutingOnlyRole
        }

        public enum FunctionnalDataRole
        {
            NormalMemberLocalDrive,
            HiddenMemberAzureDrive
        }

        public List<NodeEndPoint> MongoCInstances { get { return GetMongoCInstances(); } }
        public List<NodeEndPoint> MongoSInstances { get { return GetMongoSInstances(); } }

        //Singleton
        private static readonly MongoDBAzurePlatform instance = new MongoDBAzurePlatform();
        public static MongoDBAzurePlatform Instance
        {
            get
            {
                return instance;
            }
        }

        /// <summary>
        /// MONGO CONFIG
        /// </summary>
        /// <returns></returns>
        private List<NodeEndPoint> GetMongoCInstances()
        {
#if DEBUG
            List<NodeEndPoint> r = new List<NodeEndPoint>();
            int port = 30020;
            foreach (NodeEndPoint n in RoleEnvironment.Roles["RoutingConfigRole"].Instances.OrderBy(c => c.Id).Select(c => new NodeEndPoint(FilterInstanceName(c.Id), port)))
            {
                r.Add(n);
                port++;
            }
            return r;
#else
            //return RoleEnvironment.Roles["MainRoutingConfigRole"].Instances.OrderBy(c => c.Id).Select(i => new NodeEndPoint(i.Id,i.InstanceEndpoints["MongoConfigEndpoint"].IPEndpoint.Port)).ToList();
            return RoleEnvironment.Roles.Where(x => x.Key.Contains("RoutingConfigRole")).First().Value.Instances.OrderBy(c => c.Id).Select(i => new NodeEndPoint(i.Id, i.InstanceEndpoints["MongoConfigEndpoint"].IPEndpoint.Port)).ToList();
#endif
        }

        /// <summary>
        /// MONGO Shard
        /// </summary>
        /// <returns></returns>
        private List<NodeEndPoint> GetMongoSInstances()
        {
#if DEBUG
            List<NodeEndPoint> r = new List<NodeEndPoint>();
            int port = 30030;
            foreach (NodeEndPoint n in RoleEnvironment.Roles["RoutingConfigRole"].Instances.OrderBy(c => c.Id).Select(c => new NodeEndPoint(FilterInstanceName(c.Id), port)))
            {
                r.Add(n);
                port++;
            }

            for (int i = 4; i < RoleEnvironment.CurrentRoleInstance.Role.Instances.Count; i++)
            {
                r.Add(new NodeEndPoint(FilterInstanceName(RoleEnvironment.CurrentRoleInstance.Id), port++));
            }

            return r;
#else
            return RoleEnvironment.Roles["MongosEndpoint"].Instances.Select(i => new NodeEndPoint(i.Id, i.InstanceEndpoints["MongoConfigEndpoint"].IPEndpoint.Port)).ToList();
#endif
        }


        /// <summary>
        /// MONGO DATA
        /// </summary>
        /// <returns></returns>
        public List<NodeEndPoint> GetReplicaSetMembers(string RoleName)
        {
            List<NodeEndPoint> r = new List<NodeEndPoint>();
#if DEBUG
            int port = 30040;
            if (RoleName == "ReplicaSetRole1")
            {
                port = 30040;
            }

            if (RoleName == "ReplicaSetRole2")
            {
                port = 30140;
            }

            //Get all endpoints for this role
            foreach (NodeEndPoint n in RoleEnvironment.Roles[RoleName].Instances.OrderBy(c => c.Id).Select(c => new NodeEndPoint(FilterInstanceName(c.Id), port)))
            {
                r.Add(n);
                port++;
            }
#else

            //Get all endpoints for this role
            //foreach (NodeEndPoint n in RoleEnvironment.Roles[RoleName].Instances.Select(c => new NodeEndPoint(c.Id, c. InstanceEndpoints["MongoDbEndpoint"].IPEndpoint.Port)))
            //{
            //    r.Add(n);
            //}
#endif
            //return r.OrderBy( o=> o.Host).ToList();
            r.Add(new NodeEndPoint(RoleEnvironment.CurrentRoleInstance.Id, RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["MongoDbEndpoint"].IPEndpoint.Port));
            return r;
        }

#if DEBUG
        private string FilterInstanceName(string s)
        {
            string[] parts = s.Split('.');
            return parts[parts.Length - 1];
        }
#endif
        public NodeEndPoint MyMongoCAddress
        {
            get
            {
                if (!RoleEnvironment.CurrentRoleInstance.Role.Name.Contains("RoutingConfigRole"))
                    throw new InvalidOperationException("GetMyMongoCAddress called from a " + RoleEnvironment.CurrentRoleInstance.Role.Name + " role");

                if (Instance.MyFunctionnalRoutingRole == FunctionnalRoutingRole.RoutingAndConfigRole)
                {
                    return new NodeEndPoint(RoleEnvironment.CurrentRoleInstance.Id, RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["MongoConfigEndpoint"].IPEndpoint.Port);
                }
                else
                    throw new InvalidOperationException("GetMyMongoCAddress invalid call for S only role");
            }
        }

        public NodeEndPoint MyMongoSAddress
        {
            get
            {
                if (!RoleEnvironment.CurrentRoleInstance.Role.Name.Contains("RoutingConfigRole"))
                    throw new InvalidOperationException("GetMyMongoSAddress called from a " + RoleEnvironment.CurrentRoleInstance.Role.Name + " role");

                return new NodeEndPoint(RoleEnvironment.CurrentRoleInstance.Id, RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["MongosEndpoint"].IPEndpoint.Port);
            }
        }

        public NodeEndPoint MyMongoDAddress
        {
            get
            {
                if (!RoleEnvironment.CurrentRoleInstance.Role.Name.StartsWith("MainShardRole"))
                    throw new InvalidOperationException("GetMyMongoDAddress called from a " + RoleEnvironment.CurrentRoleInstance.Role.Name + " role");

                return new NodeEndPoint(RoleEnvironment.CurrentRoleInstance.Id, RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["MongoDbEndpoint"].IPEndpoint.Port);
            }
        }


        public FunctionnalRoutingRole MyFunctionnalRoutingRole
        {
            get
            {
                if (!RoleEnvironment.CurrentRoleInstance.Role.Name.Contains("RoutingConfigRole"))
                    throw new InvalidOperationException("MyFunctionnalRoutingRole called from a " + RoleEnvironment.CurrentRoleInstance.Role.Name + " role");

                if (RoleEnvironment.CurrentRoleInstance.Id.EndsWith("IN_0")) return FunctionnalRoutingRole.RoutingAndConfigRole;
                if (RoleEnvironment.CurrentRoleInstance.Id.EndsWith("IN_1")) return FunctionnalRoutingRole.RoutingAndConfigRole;
                if (RoleEnvironment.CurrentRoleInstance.Id.EndsWith("IN_2")) return FunctionnalRoutingRole.RoutingAndConfigRole;
                return FunctionnalRoutingRole.RoutingOnlyRole;
            }
        }

        public FunctionnalDataRole GetFunctionnalDataRole(NodeEndPoint endpoint, string RoleName)
        {

            //Check instance number
            //if (endpoint.Host.EndsWith("IN_2", StringComparison.InvariantCultureIgnoreCase))
            //    return FunctionnalDataRole.HiddenMemberAzureDrive;
            //else
            return FunctionnalDataRole.NormalMemberLocalDrive;

        }

        public FunctionnalDataRole MyFunctionnalDataRole
        {
            get
            {
                if (RoleEnvironment.CurrentRoleInstance.Role.Name.Contains("RoutingConfigRole"))
                    throw new InvalidOperationException("MyFunctionnalDataRole called from a " + RoleEnvironment.CurrentRoleInstance.Role.Name + " role");
                //V3 : no hidden role anymore
                return FunctionnalDataRole.NormalMemberLocalDrive;
            }
        }


        public string GetMyLocalDBPath
        {
            get
            {
                if (RoleEnvironment.CurrentRoleInstance.Role.Name.Contains("RoutingConfigRole"))
                    throw new InvalidOperationException("MyFunctionnalDataRole called from a " + RoleEnvironment.CurrentRoleInstance.Role.Name + " role");

                string driveRoot = System.IO.Path.GetPathRoot(RoleEnvironment.GetLocalResource("MongoDBCache").RootPath);
                return System.IO.Path.Combine(driveRoot, "MongoData");
            }
        }

        public string MongoExePath
        {
            get
            {
#if DEBUG
                return @"D:\Logiciels\MongoDB\mongodb-win32-x86_64-2.0.1\mongodb-win32-x86_64-2.0.1\bin";
#else
                return "MongoDB";
#endif


            }
        }

    }



}
