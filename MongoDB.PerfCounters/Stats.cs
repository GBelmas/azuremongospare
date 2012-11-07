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
using MongoDB.Bson;

namespace MongoDB.PerformanceCounters
{
    internal class Stats
    {
        //To add a perf counter, follow the steps
        
        //STEP 1 : add the path to the required stat in the db.serverStatus bson document. The hierarchy is separated by the | character
        public const string Gl_CurrentQueue_Readers = "globalLock|currentQueue|readers";
        public const string Gl_CurrentQueue_Writers = "globalLock|currentQueue|writers";
        public const string Gl_ActiveClients_readers = "globalLock|activeClients|readers";
        public const string Gl_ActiveClients_writers = "globalLock|activeClients|writers";
        public const string Mem_Resident = "mem|resident";
        public const string Mem_Virtual = "mem|virtual";
        public const string Connections_Current = "connections|current";
        public const string Connections_Available = "connections|available";
        public const string BGFL_Flushes = "backgroundFlushing|flushes";
        public const string BGFL_TotalMs = "backgroundFlushing|total_ms";
        public const string BGFL_LastMS = "backgroundFlushing|last_ms";
        public const string Cursors_TotalOpen = "cursors|totalOpen";
        public const string Cursors_Timedout = "cursors|timedOut";
        public const string Dur_CommitsInWriteLock = "dur|commitsInWriteLock";

        private static string[] statsToCollect;

        static Stats()
        {
            //STEP 2 : add the stat in the following string table in order to take it into account in the parse method
            statsToCollect = new string[] {
                Gl_CurrentQueue_Readers,
                Gl_CurrentQueue_Writers,
                Gl_ActiveClients_readers,
                Gl_ActiveClients_writers,
                Mem_Resident,
                Mem_Virtual,
                Connections_Current,
                Connections_Available,
                BGFL_Flushes,
                BGFL_TotalMs,
                BGFL_LastMS,
                Cursors_TotalOpen,
                Cursors_Timedout,
                Dur_CommitsInWriteLock
            };
        }


        public static PerformanceData Parse(BsonDocument stats)
        {
            if (null == stats)
                return null;

            PerformanceData perf = PerformanceData.Current;
            
            foreach (string statName in statsToCollect)
            {
                string statVal = ExtractDataFromBson(stats, statName);

                if (!string.IsNullOrEmpty(statVal))
                {
                    long value = 0;
                    if (long.TryParse(statVal, out value))
                    {
                        perf[statName] = value;
                        Console.WriteLine(string.Format("{0} : {1}", statName, value));
                    }
                }
                else
                    Console.WriteLine(string.Format("Skipping {0} because cant find the value", statName));
            }
            
            return perf;
        }

        private static string ExtractDataFromBson(BsonDocument stats, string dataName)
        {
            try
            {
                string[] splitDataName = dataName.Split('|');
                if (splitDataName.Length == 1) //We have the name of the value to retrieve
                    return stats.GetValue(dataName).ToString();
                else //Navigate to the first element of the list
                {
                    var elmToNavigate = stats.Contains(splitDataName[0]);
                    if (elmToNavigate)
                        return ExtractDataFromBson(stats.GetElement(splitDataName[0]).Value.ToBsonDocument(), dataName.Substring(dataName.IndexOf('|') + 1));
                    return null; //Can't find the element to navigate in (stat not available on this mongo process)
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("An error occured while parsing {0} value, message {1}", dataName, e.Message);
                return null;
            }
        }
    }
}
