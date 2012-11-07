﻿//Copyright (c) <2012>, Kobojo©, Vnext
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

namespace Helpers.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.WindowsAzure.StorageClient;

    /// <summary>
    /// Provides.
    /// </summary>
    public class ShardInfo : TableServiceEntity
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of <see cref="ShardInfo"/>.
        /// </summary>
        public ShardInfo() 
        {
            this.PartitionKey = "";
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ShardInfo"/> with name, drivepath, instanceName and ip address.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="drivePath"></param>
        /// <param name="instanceName"></param>
        /// <param name="ip"></param>
        public ShardInfo(string name, string drivePath, string instanceName, string ip = "0.0.0.0")
            : this()
        {
            this.RowKey = name;
            this.Name = name;
            this.DrivePath = drivePath;
            this.Ip = ip;
            this.InstanceName = instanceName;
        }
        #endregion Constructors

        #region Properties
        /// <summary>
        /// Gets or sets the name of the shardinfo.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the ip address.
        /// </summary>
        public string Ip { get; set; }
        
        /// <summary>
        /// Gets or sets the drive path.
        /// </summary>
        public string DrivePath { get; set; }
        
        /// <summary>
        /// Gets or sets the instance name.
        /// </summary>
        public string InstanceName { get; set; }
        #endregion Properties
    }
}
