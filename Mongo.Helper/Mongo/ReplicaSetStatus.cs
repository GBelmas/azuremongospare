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
    public class ReplicaSetStatus
    {
        public ReplicaSetStatus()
        {
            this.Members = new List<ReplicaSetNode>();
            this.Date = DateTime.MinValue;
            this.IsEmptyConfig = false;
        }

        public string ReplicasetName { get; set; }
        public List<ReplicaSetNode> Members { get; set; }
        public DateTime Date { get; set; }
        public bool IsEmptyConfig { get; set; }

        public override string ToString()
        {
            if (this.IsEmptyConfig)
            {
                return "EMPTY CONFIG";
            }
            else
            {
                StringBuilder sb = new StringBuilder(1000);
                sb.Append(ReplicasetName);
                sb.Append(" : ");

                sb.Append(string.Join(",", Members.Select(m => string.Format(" {0}:{1} {2} {3}", m.Adress, m.Port, m.StateStr, m.Health))));
                return sb.ToString();
            }

        }
    }
}
