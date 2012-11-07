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
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Diagnostics;
using Helpers.Mongo;

namespace ReplicaSetRole1
{
    public class ReplicaSetRoleManager
    {

        public enum ReplicaSetRoleState
        {
            // INSTANCE
            InstanceStarting,
            InstanceStopping,
            InstanceStopped,
            InstanceRunning,

            MongoDNotRunning,

            // REPLICASET
            ReplicaSetConfigurationInProgress,

            Unknown,
            PreparingData
        }        

        private static ReplicaSetRoleState OldState = ReplicaSetRoleState.Unknown;

        /// <summary>
        /// Set the state of the instance
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="state"></param>
        public static void SetState(ReplicaSetRoleState state)
        {
            if (OldState != state)
            {
                Trace.TraceWarning(string.Format("*** Changing state from : {1} to : {2}",
                                                        RoleEnvironment.CurrentRoleInstance.Id,
                                                        OldState,
                                                        state));
                MongoHelper.RegisterInstanceOrUpdate(state.ToString());
                OldState = state;
            }
        }

        public static ReplicaSetRoleState GetState()
        {
            return OldState;
        }
    }

}
