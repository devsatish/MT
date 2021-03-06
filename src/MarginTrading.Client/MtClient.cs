﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using MarginTrading.Common.ClientContracts;
using WampSharp.V2;
using WampSharp.V2.Client;
using MarginTrading.Common.Wamp;
using MarginTrading.Core;
using Microsoft.Extensions.Configuration;

namespace MarginTrading.Client
{
    public class MtClient
    {
        private string _token;
        private string _notificationId;
        private string _serverAddress;
        private IWampRealmProxy _realmProxy;
        private IRpcMtFrontend _service;

        public void Connect(ClientEnv env)
        {
            SetEnv(env);
            var factory = new DefaultWampChannelFactory();
            IWampChannel channel = factory.CreateJsonChannel(_serverAddress, "mtcrossbar");

            while (!channel.RealmProxy.Monitor.IsConnected)
            {
                try
                {
                    Console.WriteLine($"Trying to connect to server {_serverAddress}...");
                    channel.Open().Wait();
                }
                catch
                {
                    Console.WriteLine("Retrying in 5 sec...");
                    Thread.Sleep(5000);
                }
            }
            Console.WriteLine($"Connected to server {_serverAddress}");

            _realmProxy = channel.RealmProxy;
            _service = _realmProxy.Services.GetCalleeProxy<IRpcMtFrontend>();
        }

        public void SetEnv(ClientEnv env)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{env.ToString().ToLower()}.json", true, true)
                .Build();

            _token = config["token"];
            _notificationId = config["notificationId"];
            _serverAddress = config["serverAddress"];
        }

        public async Task InitData()
        {
            var data = await _service.InitData(_token);
        }

        public async Task InitAccounts()
        {
            var data = await _service.InitAccounts(_token);
        }

        public async Task AccountInstruments()
        {
            var data = await _service.AccountInstruments(_token);
        }

        public async Task InitGraph()
        {
            var data = await _service.InitGraph();
        }

        public async Task SetActiveAccount()
        {
            var data = await _service.InitData(_token);

            IDisposable subscription = _realmProxy.Services.GetSubject<NotifyResponse>($"user.{_notificationId}")
                .Subscribe(info =>
                {
                    if (info.Account != null)
                        Console.WriteLine(info.Account.IsCurrent);
                });


            var request = new SetActiveAccountClientRequest
            {
                AccountId = data.Demo.Accounts[1].Id,
                Token = _token
            };

            Console.WriteLine("done...");
            Console.ReadLine();
            subscription.Dispose();
        }

        public async Task GetAccountHistory()
        {
            var request = new AccountHistoryClientRequest
            {
                Token = _token
            };

            var result = await _service.GetAccountHistory(request.ToJson());
        }

        public async Task GetHistory()
        {
            var request = new AccountHistoryClientRequest
            {
                Token = _token
            };

            var result = await _service.GetHistory(request.ToJson());
        }

        public async Task PlaceOrder()
        {
            IDisposable subscription = _realmProxy.Services.GetSubject<NotifyResponse>($"user.updates.{_notificationId}")
                .Subscribe(info =>
                {
                    if (info.Account != null)
                        Console.WriteLine("Account changed");

                    if (info.Order != null)
                        Console.WriteLine("Order changed");

                    if (info.AccountStopout != null)
                        Console.WriteLine("Account stopout");

                    if (info.UserUpdate != null)
                        Console.WriteLine($"User update: accountAssetPairs = {info.UserUpdate.UpdateAccountAssetPairs}, accounts = {info.UserUpdate.UpdateAccounts}");
                });

            var data = await _service.InitData(_token);

            try
            {
                var request = new OpenOrderClientRequest
                {
                    Token = _token,
                    Order = new NewOrderClientContract
                    {
                        AccountId = data.Demo.Accounts[0].Id,
                        FillType = OrderFillType.FillOrKill,
                        Instrument = "BTCUSD",
                        Volume = 1
                    }
                };

                var order = await _service.PlaceOrder(request.ToJson());

                if (order.Result.Status == 3)
                {
                    Console.WriteLine($"Order rejected: {order.Result.RejectReason} -> {order.Result.RejectReasonText}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("Press enter");
            Console.ReadLine();
            subscription.Dispose();
        }

        public async Task CloseOrder(bool closeAnyFpl)
        {
            IDisposable subscription = _realmProxy.Services.GetSubject<NotifyResponse>($"user.updates.{_notificationId}")
                .Subscribe(info =>
                {
                    if (info.Order != null)
                    {
                        Console.WriteLine($"Order pnl: {info.Order.Fpl}");
                    }
                });

            while (true)
            {
                var orders = await _service.GetOpenPositions(_token);

                if (orders.Demo.Any(item => item.Fpl > 0) || closeAnyFpl)
                {
                    var order = orders.Demo.First();

                    var request = new CloseOrderClientRequest
                    {
                        OrderId = order.Id,
                        AccountId = order.AccountId,
                        Token = _token
                    };

                    var result = await _service.CloseOrder(request.ToJson());
                    break;
                }

                Thread.Sleep(200);
            }

            Console.WriteLine("Press enter");
            Console.ReadLine();
            subscription.Dispose();
        }

        public async Task CancelOrder()
        {
            IDisposable subscription = _realmProxy.Services.GetSubject<NotifyResponse>($"user.updates.{_notificationId}")
                .Subscribe(info =>
                {
                    if (info.Order != null)
                    {
                        Console.WriteLine($"Order status: {info.Order.Status}");
                    }
                });

            while (true)
            {
                var orders = await _service.GetOpenPositions(_token);

                if (orders.Demo.Any())
                {
                    var order = orders.Demo.First();

                    var request = new CloseOrderClientRequest
                    {
                        OrderId = order.Id,
                        Token = _token
                    };

                    var result = _service.CancelOrder(request.ToJson());
                    break;
                }

                Thread.Sleep(200);
            }

            Console.WriteLine("Press enter");
            Console.ReadLine();
            subscription.Dispose();
        }

        public async Task GetOpenPositions()
        {
            var result = await _service.GetOpenPositions(_token);
        }

        public async Task GetAccountOpenPositions()
        {
            var data = await _service.InitData(_token);

            var request = new AccountTokenClientRequest
            {
                Token = _token,
                AccountId = data.Demo.Accounts[0].Id
            };

            var result = await _service.GetAccountOpenPositions(request.ToJson());
        }

        public async Task GetClientOrders()
        {
            var result = await _service.GetClientOrders(_token);
        }

        public async Task ChangeOrderLimits()
        {
            var request = new ChangeOrderLimitsClientRequest
            {
                Token = _token,
                OrderId = ""
            };
            
            var result = await _service.ChangeOrderLimits(request.ToJson());
            Console.WriteLine($"result = {result.Result}, message = {result.Message}");
        }

        public void Prices()
        {
            IDisposable subscription = _realmProxy.Services.GetSubject<InstrumentBidAskPair>("prices.update")
                .Subscribe(info =>
                {
                    Console.WriteLine($"{info.Instrument} {info.Bid}/{info.Ask}");
                });


            Console.ReadLine();
            subscription.Dispose();
        }

        public void UserUpdates()
        {
            IDisposable subscription = _realmProxy.Services.GetSubject<NotifyResponse>($"user.updates.{_notificationId}")
                .Subscribe(info =>
                {
                    if (info.UserUpdate != null)
                        Console.WriteLine($"assets = {info.UserUpdate.UpdateAccountAssetPairs}, accounts = {info.UserUpdate.UpdateAccounts}");
                });


            Console.ReadLine();
            subscription.Dispose();
        }
    }
}
