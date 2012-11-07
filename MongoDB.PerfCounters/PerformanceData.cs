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
using System.Collections.Generic;
using System.Diagnostics;

namespace MongoDB.PerformanceCounters
{
    public class PerformanceData
    {
        #region Singleton
        private static PerformanceData _singleton = new PerformanceData();

        static PerformanceData()
        {
        }

        public static PerformanceData Current
        {
            get { return _singleton; }
        }
        #endregion

        private static class Formats
        {
            public const string Number = "# {0}";
            public const string Ratio = "{0}";
            public const string MegaBytes = "# {0} MB";
            public const string Millisec = "{0} in millisec";

        }

        private static class Names
        {
            //STEP 3 add the stat name in a variable below. You shoud reuse the same variable name as in the Stats class.
            public const string Gl_CurrentQueue_Readers = "Awaiting read lock operations";
            public const string Gl_CurrentQueue_Writers = "Awaiting write lock operations";
            public const string Gl_ActiveClients_readers = "Active clients performing read";
            public const string Gl_ActiveClients_writers = "Active clients performing write";
            public const string Mem_Resident = "Resident memory";
            public const string Mem_Virtual = "Virtual memory";
            public const string Connections_Current = "Current connections";
            public const string Connections_Available = "Available connections";
            public const string BGFL_Flushes = "Flush writes to disk";
            public const string BGFL_TotalMs = "Total flush time";
            public const string BGFL_LastMS = "Last flush time";
            public const string Cursors_TotalOpen = "Total opened cursors";
            public const string Cursors_Timedout = "Timed out cursors";
            public const string Dur_CommitsInWriteLock = "Commits in write lock";
        }

        internal static class Categories
        {
            //STEP 3 bis Create a new perfcounter category if needed
            public const string GlobalLock = "MongoDB Global Lock";
            public const string BackgroundFlushing = "MongoDB Background Flushing";
            public const string Memory = "MongoDB Memory";
            public const string Connections = "MongoDB Connections";
            public const string CursorsAndWrites = "MongoDB Cursors & Writes";
        }

        private PerformanceData()
        {
            CreatePerformanceCounterData();
            InitializeSetters();
        }

        #region Define Counters
        //STEP 4 Declare a perfcounter for your new stat
        private static PerformanceCounterData _Gl_CurrentQueue_Readers;
        private static PerformanceCounterData _Gl_CurrentQueue_Writers;
        private static PerformanceCounterData _Gl_ActiveClients_readers;
        private static PerformanceCounterData _Gl_ActiveClients_writers;
        private static PerformanceCounterData _Mem_Resident;
        private static PerformanceCounterData _Mem_Virtual;
        private static PerformanceCounterData _Connections_Current;
        private static PerformanceCounterData _Connections_Available;
        private static PerformanceCounterData _BGFL_Flushes;
        private static PerformanceCounterData _BGFL_TotalMs;
        private static PerformanceCounterData _BGFL_LastMS;
        private static PerformanceCounterData _Cursors_TotalOpen;
        private static PerformanceCounterData _Cursors_Timedout;
        private static PerformanceCounterData _Dur_CommitsInWriteLock;
        #endregion

        #region CreatePerformanceCounterData
        private void CreatePerformanceCounterData()
        {
            //STEP 5 create the perfcounter based on the model of the following lines
            _Gl_CurrentQueue_Readers = PerformanceCounterData.CreatePerformanceCounterData(Categories.GlobalLock, Formats.Number, Names.Gl_CurrentQueue_Readers, PerformanceCounterType.NumberOfItems64);
            _Gl_CurrentQueue_Writers = PerformanceCounterData.CreatePerformanceCounterData(Categories.GlobalLock, Formats.Number, Names.Gl_CurrentQueue_Writers, PerformanceCounterType.NumberOfItems64);
            _Gl_ActiveClients_readers = PerformanceCounterData.CreatePerformanceCounterData(Categories.GlobalLock, Formats.Number, Names.Gl_ActiveClients_readers, PerformanceCounterType.NumberOfItems64);
            _Gl_ActiveClients_writers = PerformanceCounterData.CreatePerformanceCounterData(Categories.GlobalLock, Formats.Number, Names.Gl_ActiveClients_writers, PerformanceCounterType.NumberOfItems64);
            _Mem_Resident = PerformanceCounterData.CreatePerformanceCounterData(Categories.Memory, Formats.MegaBytes, Names.Mem_Resident, PerformanceCounterType.NumberOfItems32);
            _Mem_Virtual = PerformanceCounterData.CreatePerformanceCounterData(Categories.Memory, Formats.MegaBytes, Names.Mem_Virtual, PerformanceCounterType.NumberOfItems32);
            _Connections_Current = PerformanceCounterData.CreatePerformanceCounterData(Categories.Connections, Formats.Number, Names.Connections_Current, PerformanceCounterType.NumberOfItems64);
            _Connections_Available = PerformanceCounterData.CreatePerformanceCounterData(Categories.Connections, Formats.Number, Names.Connections_Available, PerformanceCounterType.NumberOfItems64);
            _BGFL_Flushes = PerformanceCounterData.CreatePerformanceCounterData(Categories.BackgroundFlushing, Formats.Number, Names.BGFL_Flushes, PerformanceCounterType.NumberOfItems64);
            _BGFL_TotalMs = PerformanceCounterData.CreatePerformanceCounterData(Categories.BackgroundFlushing, Formats.Millisec, Names.BGFL_TotalMs, PerformanceCounterType.NumberOfItems64);
            _BGFL_LastMS = PerformanceCounterData.CreatePerformanceCounterData(Categories.BackgroundFlushing, Formats.Millisec, Names.BGFL_LastMS, PerformanceCounterType.NumberOfItems64);
            _Cursors_TotalOpen = PerformanceCounterData.CreatePerformanceCounterData(Categories.CursorsAndWrites, Formats.Number, Names.Cursors_TotalOpen, PerformanceCounterType.NumberOfItems64);
            _Cursors_Timedout = PerformanceCounterData.CreatePerformanceCounterData(Categories.CursorsAndWrites, Formats.Number, Names.Cursors_Timedout, PerformanceCounterType.NumberOfItems64);
            _Dur_CommitsInWriteLock = PerformanceCounterData.CreatePerformanceCounterData(Categories.CursorsAndWrites, Formats.Number, Names.Dur_CommitsInWriteLock, PerformanceCounterType.NumberOfItems64);
        }
        #endregion

        #region CreateCounters
        internal void CreateCounters()
        {
            //STEP 6 add a line to instance the counter
            _Gl_CurrentQueue_Readers.CreatePerformanceCounter();
            _Gl_CurrentQueue_Writers.CreatePerformanceCounter();
            _Gl_ActiveClients_readers.CreatePerformanceCounter();
            _Gl_ActiveClients_writers.CreatePerformanceCounter();
            _Mem_Resident.CreatePerformanceCounter();
            _Mem_Virtual.CreatePerformanceCounter();
            _Connections_Current.CreatePerformanceCounter();
            _Connections_Available.CreatePerformanceCounter();
            _BGFL_Flushes.CreatePerformanceCounter();
            _BGFL_TotalMs.CreatePerformanceCounter();
            _BGFL_LastMS.CreatePerformanceCounter();
            _Cursors_TotalOpen.CreatePerformanceCounter();
            _Cursors_Timedout.CreatePerformanceCounter();
            _Dur_CommitsInWriteLock.CreatePerformanceCounter();
        }
        #endregion

        #region InstallCounters
        internal void InstallCounters(CounterCreationDataCollection counters, string category)
        {
            //STEP 7 Add a line to enable the counter installation in the OS perfmon
            _Gl_CurrentQueue_Readers.AddIfMatchCategory(counters, category);
            _Gl_CurrentQueue_Writers.AddIfMatchCategory(counters, category);
            _Gl_ActiveClients_readers.AddIfMatchCategory(counters, category);
            _Gl_ActiveClients_writers.AddIfMatchCategory(counters, category);
            _Mem_Resident.AddIfMatchCategory(counters, category);
            _Mem_Virtual.AddIfMatchCategory(counters, category);
            _Connections_Current.AddIfMatchCategory(counters, category);
            _Connections_Available.AddIfMatchCategory(counters, category);
            _BGFL_Flushes.AddIfMatchCategory(counters, category);
            _BGFL_TotalMs.AddIfMatchCategory(counters, category);
            _BGFL_LastMS.AddIfMatchCategory(counters, category);
            _Cursors_TotalOpen.AddIfMatchCategory(counters, category);
            _Cursors_Timedout.AddIfMatchCategory(counters, category);
            _Dur_CommitsInWriteLock.AddIfMatchCategory(counters, category);
        }
        #endregion

        #region Setters
        private delegate void PerformanceDataSetter(long value);
        private Dictionary<string, PerformanceDataSetter> _setters;

        private void InitializeSetters()
        {
            //STEP 10 associate the stat name and the counter setter (in order to be able to loop on the stat list)
            // build dictionary of strings/setters to be used in indexer
            _setters = new Dictionary<string, PerformanceDataSetter>();
            _setters.Add(Stats.Gl_CurrentQueue_Readers, Set_Gl_CurrentQueue_Readers);
            _setters.Add(Stats.Gl_CurrentQueue_Writers, Set_Gl_CurrentQueue_Writers);
            _setters.Add(Stats.Gl_ActiveClients_readers, Set_Gl_ActiveClients_readers);
            _setters.Add(Stats.Gl_ActiveClients_writers, Set_Gl_ActiveClients_writers);
            _setters.Add(Stats.Mem_Resident, Set_Mem_Resident);
            _setters.Add(Stats.Mem_Virtual, Set_Mem_Virtual);
            _setters.Add(Stats.Connections_Current, Set_Connections_Current);
            _setters.Add(Stats.Connections_Available, Set_Connections_Available);
            _setters.Add(Stats.BGFL_Flushes, Set_BGFL_Flushes);
            _setters.Add(Stats.BGFL_TotalMs, Set_BGFL_TotalMs);
            _setters.Add(Stats.BGFL_LastMS, Set_BGFL_LastMS);
            _setters.Add(Stats.Cursors_TotalOpen, Set_Cursors_TotalOpen);
            _setters.Add(Stats.Cursors_Timedout, Set_Cursors_Timedout);
            _setters.Add(Stats.Dur_CommitsInWriteLock, Set_Dur_CommitsInWriteLock);
        }

        internal long this[string command]
        {
            set
            {
                PerformanceDataSetter setter = null;
                if (_setters.TryGetValue(command, out setter))
                    setter(value);
            }
        }
        #endregion

        #region Properties
        //STEP 8 create a property for the perfcounter value. Name it the same way
        
        public long Gl_CurrentQueue_Readers
        {
            get { return _Gl_CurrentQueue_Readers.Counter.RawValue; }
            set { Set_Gl_CurrentQueue_Readers(value); }
        }

        public long Gl_CurrentQueue_Writers
        {
            get { return _Gl_CurrentQueue_Writers.Counter.RawValue; }
            set { Set_Gl_CurrentQueue_Writers(value); }
        }

        public long Gl_ActiveClients_readers
        {
            get { return _Gl_ActiveClients_readers.Counter.RawValue; }
            set { Set_Gl_ActiveClients_readers(value); }
        }

        public long Gl_ActiveClients_writers
        {
            get { return _Gl_ActiveClients_writers.Counter.RawValue; }
            set { Set_Gl_ActiveClients_writers(value); }
        }

        public long Mem_Resident
        {
            get { return _Mem_Resident.Counter.RawValue; }
            set { Set_Mem_Resident(value); }
        }

        public long Mem_Virtual
        {
            get { return _Mem_Virtual.Counter.RawValue; }
            set { Set_Mem_Virtual(value); }
        }

        public long Connections_Current
        {
            get { return _Connections_Current.Counter.RawValue; }
            set { Set_Connections_Current(value); }
        }

        public long Connections_Available
        {
            get { return _Connections_Available.Counter.RawValue; }
            set { Set_Connections_Available(value); }
        }

        public long BGFL_Flushes
        {
            get { return _BGFL_Flushes.Counter.RawValue; }
            set { Set_BGFL_Flushes(value); }
        }

        public long BGFL_TotalMs
        {
            get { return _BGFL_TotalMs.Counter.RawValue; }
            set { Set_BGFL_TotalMs(value); }
        }

        public long BGFL_LastMS
        {
            get { return _BGFL_LastMS.Counter.RawValue; }
            set { Set_BGFL_LastMS(value); }
        }

        public long Cursors_TotalOpen
        {
            get { return _Cursors_TotalOpen.Counter.RawValue; }
            set { Set_Cursors_TotalOpen(value); }
        }

        public long Cursors_Timedout
        {
            get { return _Cursors_Timedout.Counter.RawValue; }
            set { Set_Cursors_Timedout(value); }
        }

        public long Dur_CommitsInWriteLock
        {
            get { return _Dur_CommitsInWriteLock.Counter.RawValue; }
            set { Set_Dur_CommitsInWriteLock(value); }
        }


        #endregion

        #region Helpers
        //STEP 9 Create a helper to set the value of the perfcounter
       
        private void Set_Gl_CurrentQueue_Readers(long value)
        {
            _Gl_CurrentQueue_Readers.Counter.RawValue = value;
        }

        private void Set_Gl_CurrentQueue_Writers(long value)
        {
            _Gl_CurrentQueue_Writers.Counter.RawValue = value;
        }

        private void Set_Gl_ActiveClients_readers(long value)
        {
            _Gl_ActiveClients_readers.Counter.RawValue = value;
        }

        private void Set_Gl_ActiveClients_writers(long value)
        {
            _Gl_ActiveClients_writers.Counter.RawValue = value;
        }

        private void Set_Mem_Resident(long value)
        {
            _Mem_Resident.Counter.RawValue = value;
        }

        private void Set_Mem_Virtual(long value)
        {
            _Mem_Virtual.Counter.RawValue = value;
        }

        private void Set_Connections_Current(long value)
        {
            _Connections_Current.Counter.RawValue = value;
        }

        private void Set_Connections_Available(long value)
        {
            _Connections_Available.Counter.RawValue = value;
        }

        private void Set_BGFL_Flushes(long value)
        {
            _BGFL_Flushes.Counter.RawValue = value;
        }

        private void Set_BGFL_TotalMs(long value)
        {
            _BGFL_TotalMs.Counter.RawValue = value;
        }

        private void Set_BGFL_LastMS(long value)
        {
            _BGFL_LastMS.Counter.RawValue = value;
        }

        private void Set_Cursors_TotalOpen(long value)
        {
            _Cursors_TotalOpen.Counter.RawValue = value;
        }

        private void Set_Cursors_Timedout(long value)
        {
            _Cursors_Timedout.Counter.RawValue = value;
        }

        private void Set_Dur_CommitsInWriteLock(long value)
        {
            _Dur_CommitsInWriteLock.Counter.RawValue = value;
        }
        #endregion
    }
}
