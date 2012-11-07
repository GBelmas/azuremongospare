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
    public class ReplicaSetNode
    {
        public ReplicaSetNode()
        {
            this.OpTime = DateTime.MinValue;
            ErrMsg = null;
        }

        public string Health { get; set; }
        public string StateStr { get; set; }
        public NodeState State { get; set; }
        public DateTime OpTime { get; set; }
        public int Id { get; set; }
        public string Adress { get; set; }
        public string Port { get; set; }
        public string ErrMsg { get; set; }

        public static NodeState GetStateFromInt(int stateId)
        {
            switch (stateId)
	        {
                case 0:
                    return NodeState.StartingUp1;
                case 1:
                    return NodeState.Primary;
                case 2:
                    return NodeState.Secondary;
                case 3:
                    return NodeState.Recovering;
                case 4:
                    return NodeState.FatalError;
                case 5:
                    return NodeState.StartingUp2;
                case 6:
                    return NodeState.UnknownState;
                case 7:
                    return NodeState.Arbiter;
                case 8:
                    return NodeState.Down;
                case 9:
                    return NodeState.Rollback;
		        default:
                    return NodeState.UnknownState;
	        }
        }
    }

    public enum NodeState
    {
        Primary,
        Secondary,
        Recovering,
        StartingUp1,
        FatalError,
        StartingUp2,
        Rollback,
        Down,
        Arbiter,
        UnknownState
    }
}
