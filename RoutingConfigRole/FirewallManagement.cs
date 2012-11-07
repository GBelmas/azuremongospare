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
using NetFwTypeLib;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Diagnostics;

namespace RoutingConfigRole
{
    public static class FirewallManagement
    {

        /// <summary>
        /// Configure le firewall Windows pour autoriser seulement les IPs definies dansle Whitelist
        /// </summary>
        /// <param name="WhileListe">Voir http://msdn.microsoft.com/en-us/library/aa365366(v=vs.85).aspx pour le format</param>
        static internal void ConfigureFirewall()
        {
            try
            {
                INetFwRule denyRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
                denyRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                denyRule.Description = "Used to block all access to tcp endpoint for the mongos.";
                denyRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                denyRule.Enabled = true;
                denyRule.Protocol = 6; //6 = TCP  http://www.iana.org/assignments/protocol-numbers/protocol-numbers.xml
                denyRule.LocalPorts = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["InputMongo"].IPEndpoint.Port.ToString();
                denyRule.InterfaceTypes = "All";
                denyRule.Name = "MongoDB : Block access";

                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                firewallPolicy.Rules.Add(denyRule);

                INetFwRule allowRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
                allowRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                allowRule.Description = "Used to allow several ip to the tcp endpoint for the mongos.";
                allowRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                allowRule.Enabled = true;
                allowRule.RemoteAddresses = GetWhiteList();
                allowRule.Protocol = 6; //6 = TCP  http://www.iana.org/assignments/protocol-numbers/protocol-numbers.xml
                allowRule.LocalPorts = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["InputMongo"].IPEndpoint.Port.ToString();
                allowRule.InterfaceTypes = "All";
                allowRule.Name = "MongoDB : Allow limitted access";

                firewallPolicy.Rules.Add(allowRule);

                Trace.TraceInformation("The firewall configuration is finished");
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex != null ? ex.Message : "An error occured during the configuration of the firewall, there is no error message");
            }
        }

        static string GetWhiteList()
        {
            string s = "";

            //En premier ajoute les IPs des autres instances mongos
            //TODO : Ajouter les IPs des autres instances mongos

            //Ensuite les ACL declarée dans la config
            s = RoleEnvironment.GetConfigurationSettingValue("MongoFirewallWhiteList");

            return s;
        }

    }
}
