﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Memstate.Models;
using Memstate.Models.KeyValue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Memstate.Host.Commands
{
    public class DemoCommand : ICommand
    {
        public event EventHandler Done = (sender, args) => { };
        
        private volatile bool _running;
        
        private MemstateSettings _settings;

        private Engine<KeyValueStore<int>> _engine;

        private IWebHost _host;
        
        private Thread _producerThread;
        
        private Thread _consumerThread;

        public async Task Start(string[] arguments)
        {
            _running = true;

            _settings = new MemstateSettings(arguments);

            _settings.WithInmemoryStorage();
            _settings.LoggerFactory.AddConsole((category, level) => true);

            _engine = new EngineBuilder(_settings).Build<KeyValueStore<int>>();

            _host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Web.Startup>()
                .ConfigureServices(services => services.AddSingleton(_settings))
                .Build();

            _producerThread = new Thread(Producer);
            _consumerThread = new Thread(Consumer);

            _producerThread.Start();
            _consumerThread.Start();

            await _host.StartAsync();
        }

        public async Task Stop()
        {
            _running = false;

            _producerThread.Join();
            _consumerThread.Join();

            await _host.StopAsync();

            await _engine.DisposeAsync();
        }

        private async void Producer(object state)
        {
            var random = new Random();

            await _engine.ExecuteAsync(new Set<int>("key-0", 0));

            while (_running)
            {
                for (var i = 0; i < random.Next(1, 100); i++)
                {
                    await _engine.ExecuteAsync(new Set<int>($"key-{i}", i));
                }

                Thread.Sleep(random.Next(100, 5000));
            }
        }

        private async void Consumer(object state)
        {
            var random = new Random();

            while (_running)
            {
                for (var i = 0; i < random.Next(1, 100); i++)
                {
                    await _engine.ExecuteAsync(new Get<int>("key-0"));
                }

                Thread.Sleep(random.Next(100, 5000));
            }
        }
    }
}