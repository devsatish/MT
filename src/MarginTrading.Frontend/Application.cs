﻿using System;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using MarginTrading.Common.Wamp;
using WampSharp.V2.Realm;

namespace MarginTrading.Frontend
{
    public class Application
    {
        private readonly IComponentContext _componentContext;
        private readonly IConsole _consoleWriter;
        private readonly ILog _logger;
        private const string ServiceName = "MarginTrading.Frontend";

        public Application(
            IComponentContext componentContext,
            IConsole consoleWriter,
            ILog logger)
        {
            _componentContext = componentContext;
            _consoleWriter = consoleWriter;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _consoleWriter.WriteLine($"Staring {ServiceName}");
            await _logger.WriteInfoAsync(ServiceName, null, null, "Starting broker");
            try
            {
                var rpcMethods = _componentContext.Resolve<IRpcMtFrontend>();
                var realm = _componentContext.Resolve<IWampHostedRealm>();
                await realm.Services.RegisterCallee(rpcMethods);
            }
            catch (Exception ex)
            {
                _consoleWriter.WriteLine($"{ServiceName} error: {ex.Message}");
                await _logger.WriteErrorAsync(ServiceName, "Application.RunAsync", null, ex);
            }
        }

        public void Stop()
        {
            _consoleWriter.WriteLine($"Closing {ServiceName}");
        }
    }
}
