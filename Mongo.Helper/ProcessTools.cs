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
using System.Diagnostics;
using System.IO;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace Helpers
{
    public static class ProcessTools
    {
        public static bool IsRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }
            else
                return !process.HasExited;
        }

        public static Process StartProcessWindow(string exeDir, string exeFilename, string arguments, bool redirectOutputToDiagnostics, bool recycleOnExit, Action<object, EventArgs> customProcessExited = null)
        {
            Process process = new Process();

            // Path of the mongo exe
            string exeDirPath = Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\", exeDir);

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.StartInfo.FileName = Path.Combine(exeDirPath, exeFilename);
            process.StartInfo.RedirectStandardError = redirectOutputToDiagnostics;
            process.StartInfo.RedirectStandardOutput = redirectOutputToDiagnostics;
            process.StartInfo.Arguments = arguments;


            Trace.TraceInformation("Launch mongod with arguments : " + arguments);

            if (redirectOutputToDiagnostics)
            {
                process.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
                process.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
            }

            process.EnableRaisingEvents = true;
            if (recycleOnExit)
                process.Exited += new EventHandler(process_Exited);
            else if (customProcessExited != null)
                process.Exited += new EventHandler(customProcessExited);

            process.Start();

            if (redirectOutputToDiagnostics)
            {
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
            }
            return process;
        }

        public static Process StartProcess(string exeDir, string exeFilename, string arguments, bool redirectOutputToDiagnostics, bool recycleOnExit, Action<object, EventArgs> customProcessExited = null)
        {
            Process process = new Process();

            // Path of the mongo exe
            string exeDirPath = Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\", exeDir);

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.StartInfo.FileName = Path.Combine(exeDirPath, exeFilename);
            process.StartInfo.RedirectStandardError = redirectOutputToDiagnostics;
            process.StartInfo.RedirectStandardOutput = redirectOutputToDiagnostics;
            process.StartInfo.Arguments = arguments;

            if (redirectOutputToDiagnostics)
            {
                process.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
                process.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
            }

            process.EnableRaisingEvents = true;
            if (recycleOnExit)
                process.Exited += new EventHandler(process_Exited);
            else if (customProcessExited != null)
                process.Exited += new EventHandler(customProcessExited);

            process.Start();

            if (redirectOutputToDiagnostics)
            {
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
            }

            return process;
        }

        private static void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                string s = "ProcessTools.process_OutputDataReceived";
                if (sender is Process)
                    s = (sender as Process).ProcessName;
                Trace.TraceInformation(s + " : "+e.Data);
                try
                {
                    File.AppendAllText(string.Format(@"c:\mongo_output{0}.log", DateTime.Now.ToString("yyyyMMdd")), e.Data + "\r\n");
                }
                catch (Exception ex)
                {
                    Trace.TraceInformation(ex.Message);
                }
            }
        }

        private static void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                string s = "ProcessTools.process_ErrorDataReceived";
                if (sender is Process)
                    s = (sender as Process).ProcessName;
                Trace.TraceError(s + " : " + e.Data);
                try
                {
                    File.AppendAllText(string.Format(@"c:\mongo_output{0}.log",DateTime.Now.ToString("yyyyMMdd")), e.Data + "\r\n");
                }
                catch (Exception ex)
                {
                    Trace.TraceInformation(ex.Message);
                }
            }
        }

        private static void process_Exited(object sender, EventArgs e)
        {
            Process process = sender as Process;
            Trace.TraceError(string.Format("Mongo exit ! Process name : ", process != null ? process.ProcessName : "Process unknown"));
            RoleEnvironment.RequestRecycle();
        }

    }
}
