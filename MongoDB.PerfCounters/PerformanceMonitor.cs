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
using System.Diagnostics;
using System.Threading;

namespace MongoDB.PerformanceCounters
{
    /// <summary>
    /// Provide methods to start and stop a performance monitoring of Mongo.
    /// </summary>
    public static class PerformanceMonitor
    {
        public static event ThreadExceptionEventHandler ThreadException;

        #region Fields
        private static Thread _sampler;
        private static string _host;
        private static int _port;
        private static int _interval;
        #endregion Fields

        #region Public Methods
        /// <summary>
        /// Starts the performance monitor.
        /// </summary>
        /// <param name="host">Host to monitor.</param>
        /// <param name="port">Access port.</param>
        /// <param name="interval">Intervals between check points.</param>
        public static void Start(string host, int port, int interval)
        {
            Trace.TraceInformation("PerformanceMonitor.Start - Enter");
            Trace.TraceInformation("Performance counters collection begins for host:<{0}> port:<{1}> with <{2}> ms sampling", host, port, interval);

            _host = host;
            _port = port;
            _interval = interval;

            // stop if already created
            Stop();

            // sampler thread
            Thread _sampler = new Thread(SamplerThread);
            _sampler.Start();

            Trace.TraceInformation("PerformanceMonitor.Start - Leave");
        }

        /// <summary>
        /// Stops the performance monitoring.
        /// </summary>
        public static void Stop()
        {
            Trace.TraceInformation("PerformanceMonitor.Stop - Enter");

            if (null != _sampler)
                _sampler.Abort();

            Trace.TraceInformation("PerformanceMonitor.Stop - Leave");
        }
        #endregion Public Methods

        #region Private Methods
        private static void SamplerThread()
        {
            // retries
            while (true)
            {
                try
                {
                    // sampler loop
                    using (PerformanceSampler sampler = new PerformanceSampler())
                    {
                        bool connected = sampler.Connect(_host, _port);
                        while (!connected)
                        {
                            connected = sampler.Connect(_host, _port);
                            Thread.Sleep(_interval);
                        } 
                        while (true)
                        {
                            Thread.Sleep(_interval);
                            sampler.Collect();
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    // thread aborted
                    // just exit
                    _sampler = null;
                    return;
                }
                catch (Exception e)
                {
                    // unknown error
                    Trace.TraceError("PerformanceMonitor.SamplerThread - Exception during perf counters collection : {0}", e.Message);
                    if (null != ThreadException) ThreadException(null, new ThreadExceptionEventArgs(e));

                    // back to loop
                    Thread.Sleep(_interval);
                }
            };
        }
        #endregion Private Methods
    }
}
