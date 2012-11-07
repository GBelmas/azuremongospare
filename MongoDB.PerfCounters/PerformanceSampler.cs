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
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoDB.PerformanceCounters
{
    /// <summary>
    /// Collect performance data (db.serverStatus) from MongoDB
    /// This implementation is not thread-safe.
    /// </summary>
    internal class PerformanceSampler : IDisposable
    {
        #region Fields
        private MongoServer server = null;
        #endregion Fields

        #region Constructors
        /// <summary>
        /// Initializes a new instance of <see cref="PerformanceSampler"/>.
        /// </summary>
        public PerformanceSampler()
        {
        }

        ~PerformanceSampler()
        {
            Dispose();
        }
        #endregion Constructors

        #region Private Methods
        /// <summary>
        /// Retrieves the stats from the serverStatus Mongo command;
        /// </summary>
        /// <returns>the server server response as <see cref="BsonDocumen"/> if succeed, otherwise return null.</returns>
        private BsonDocument GetStats()
        {
            try
            {
                var statusCommand = new CommandDocument { { "serverStatus", 1 } };
                CommandResult result = server.RunAdminCommand(statusCommand);

                if (!result.Ok)
                    return null;
                else
                    return result.Response;
            }
            catch (Exception)
            { return null; }
        }
        #endregion Private Methods

        #region Public Methods
        /// <summary>
        /// Connects the sample to the <see cref="host"/> on the <see cref="port"/> port.
        /// </summary>
        /// <param name="host">Host to connect to.</param>
        /// <param name="port">Port to connect to.</param>
        /// <returns>True if connection succeed, otherwise return false.</returns>
        internal bool Connect(string host, int port)
        {
            // already connected ?
            if ((null != server))
            {
                try
                {
                    this.server.Connect();
                    return true;
                }
                catch(Exception) 
                {
                    return false;
                }
            }

            try
            {
                // create performance counters
                PerformanceData.Current.CreateCounters();
                // create new client
                MongoServerSettings settings = new MongoServerSettings();
                settings.Server = new MongoServerAddress(host, port);
                server = MongoServer.Create(settings);
                server.Connect();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Collects the data from the mongo.
        /// </summary>
        /// <returns>A new instance of <see cref="PerformanceData"/> if succeed, otherwise return false.</returns>
        internal PerformanceData Collect()
        {
            // get stats from local mongo process
            BsonDocument stats = GetStats();
            if (null == stats)
                return null;

            // parse and return stats
            return Stats.Parse(stats);
        }

        /// <summary>
        /// Close the current sampler.
        /// </summary>
        internal void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                this.server.Disconnect();
            }
            catch (Exception) { }
            finally
            {
                this.server = null;
            }
            GC.SuppressFinalize(this);
        }
        #endregion Public Methods
    }
}
