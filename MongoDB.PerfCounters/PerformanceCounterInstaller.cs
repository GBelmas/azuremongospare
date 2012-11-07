using System;
using System.Diagnostics;

namespace MongoDB.PerformanceCounters
{
    [System.ComponentModel.RunInstaller(true)]
    public class ConnectionsCounterInstaller : System.Diagnostics.PerformanceCounterInstaller
    {
        public ConnectionsCounterInstaller()
        {
            CategoryName = PerformanceData.Categories.Connections;
            CategoryType = PerformanceCounterCategoryType.SingleInstance;
            UninstallAction = System.Configuration.Install.UninstallAction.Remove;
            PerformanceData.Current.InstallCounters(Counters, CategoryName);
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class MemoryCounterInstaller : System.Diagnostics.PerformanceCounterInstaller
    {
        public MemoryCounterInstaller()
        {
            CategoryName = PerformanceData.Categories.Memory;
            CategoryType = PerformanceCounterCategoryType.SingleInstance;
            UninstallAction = System.Configuration.Install.UninstallAction.Remove;
            PerformanceData.Current.InstallCounters(Counters, CategoryName);
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class BackgroundFlushingCounterInstaller : System.Diagnostics.PerformanceCounterInstaller
    {
        public BackgroundFlushingCounterInstaller()
        {
            CategoryName = PerformanceData.Categories.BackgroundFlushing;
            CategoryType = PerformanceCounterCategoryType.SingleInstance;
            UninstallAction = System.Configuration.Install.UninstallAction.Remove;
            PerformanceData.Current.InstallCounters(Counters, CategoryName);
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class GlobalLockCounterInstaller : System.Diagnostics.PerformanceCounterInstaller
    {
        public GlobalLockCounterInstaller()
        {
            CategoryName = PerformanceData.Categories.GlobalLock;
            CategoryType = PerformanceCounterCategoryType.SingleInstance;
            UninstallAction = System.Configuration.Install.UninstallAction.Remove;
            PerformanceData.Current.InstallCounters(Counters, CategoryName);
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class CursorsAndWritesCounterInstaller : System.Diagnostics.PerformanceCounterInstaller
    {
        public CursorsAndWritesCounterInstaller()
        {
            CategoryName = PerformanceData.Categories.CursorsAndWrites;
            CategoryType = PerformanceCounterCategoryType.SingleInstance;
            UninstallAction = System.Configuration.Install.UninstallAction.Remove;
            PerformanceData.Current.InstallCounters(Counters, CategoryName);
        }
    }
}
