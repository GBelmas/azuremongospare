rem Run both the 32-bit and 64-bit InstallUtil
IF EXIST %SystemRoot%\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe %SystemRoot%\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe MongoDB.PerfCounters.dll > perfcounters-install-output.log 2> perfcounters-install-error.log
IF EXIST %SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe %SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe MongoDB.PerfCounters.dll >> perfcounters-install-output.log 2>> perfcounters-install-error.log
