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

namespace MongoDB.PerformanceCounters
{
    class PerformanceCounterData
    {
        public string Category;
        public string Name;
        public string Help;
        public PerformanceCounter Counter;
        public PerformanceCounterType Type;

        public void CreatePerformanceCounter()
        {
            // initialize performance counter
            Counter = new PerformanceCounter(Category, Name, false);
        }

        public static PerformanceCounterData CreatePerformanceCounterData(string category, string format, string name, PerformanceCounterType type)
        {
            return CreatePerformanceCounterData(category, format, name, string.Empty, type);
        }

        public static PerformanceCounterData CreatePerformanceCounterData(string category, string format, string name, string help, PerformanceCounterType type)
        {
            return new PerformanceCounterData()
            {
                Category = category,
                Name = string.Format(format, name),
                Help = help,
                Type = type,
            };
        }

        public int AddIfMatchCategory(CounterCreationDataCollection counters, string category)
        {
            // only add this counter data to collection if match Category
            if (Category == category)
            {
                return counters.Add(new CounterCreationData(Name, Help, Type));
            }

            return 0;
        }

    }
}
