﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarginTrading.Core.Monitoring
{
    public static class ServiceNames
    {
        public const string EthereumJobService = "EthereumJobService";
        public const string MatchingEngine = "MatchingEngine";
    }

    public interface IMonitoringRecord
    {
        string ServiceName { get; }
        DateTime DateTime { get; }
        string Version { get; }
    }

    public class MonitoringRecord : IMonitoringRecord
    {


        public string ServiceName { get; set; }
        public DateTime DateTime { get; set; }
        public string Version { get; set; }

        public static MonitoringRecord Create(string serviceName, DateTime dateTime, string version)
        {
            return new MonitoringRecord
            {
                ServiceName = serviceName,
                DateTime = dateTime,
                Version = version
            };
        }
    }

    public interface IServiceMonitoringRepository
    {

        Task<IEnumerable<IMonitoringRecord>> GetAllAsync();
        Task ScanAllAsync(Func<IEnumerable<IMonitoringRecord>, Task> chunk);
        Task UpdateOrCreate(IMonitoringRecord record);
    }
}
