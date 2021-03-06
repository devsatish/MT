﻿using System;
using System.Collections.Generic;
using System.Linq;
using MarginTrading.Core;

namespace MarginTrading.Services
{
    public class FplService : IFplService
    {
        private readonly ICfdCalculatorService _cfdCalculatorService;
        private readonly IInstrumentsCache _instrumentsCache;
        private readonly IAccountsCacheService _accountsCacheService;
        private readonly IAccountAssetsCacheService _accountAssetsCacheService;

        public FplService(
            ICfdCalculatorService cfdCalculatorService,
            IInstrumentsCache instrumentsCache,
            IAccountsCacheService accountsCacheService,
            IAccountAssetsCacheService accountAssetsCacheService)
        {
            _cfdCalculatorService = cfdCalculatorService;
            _instrumentsCache = instrumentsCache;
            _accountsCacheService = accountsCacheService;
            _accountAssetsCacheService = accountAssetsCacheService;
        }

        public void UpdateOrderFpl(IOrder order, FplData fplData)
        {
            fplData.QuoteRate = _cfdCalculatorService.GetQuoteRateForQuoteAsset(order.AccountAssetId, order.Instrument);

            fplData.Fpl = order.GetOrderType() == OrderDirection.Buy
                ? (order.ClosePrice - order.OpenPrice) * fplData.QuoteRate * order.GetMatchedVolume()
                : (order.OpenPrice - order.ClosePrice) * fplData.QuoteRate * order.GetMatchedVolume();

            var accountAsset = _accountAssetsCacheService.GetAccountAsset(order.TradingConditionId, order.AccountAssetId, order.Instrument);

            fplData.MarginInit = Math.Round(order.ClosePrice * order.GetMatchedVolume() * fplData.QuoteRate / accountAsset.LeverageInit, order.AssetAccuracy);
            fplData.MarginMaintenance = Math.Round(order.ClosePrice * order.GetMatchedVolume() * fplData.QuoteRate / accountAsset.LeverageMaintenance, order.AssetAccuracy);

            fplData.OpenCrossPrice = Math.Round(order.OpenPrice * fplData.QuoteRate, order.AssetAccuracy);
            fplData.CloseCrossPrice = Math.Round(order.ClosePrice * fplData.QuoteRate, order.AssetAccuracy);

            fplData.OpenPrice = order.OpenPrice;
            fplData.ClosePrice = order.ClosePrice;

            var account = _accountsCacheService.Get(order.ClientId, order.AccountId);
            account.CacheNeedsToBeUpdated();
        }

        public double GetMatchedOrdersPrice(List<MatchedOrder> matchedOrders, string instrument)
        {
            if (matchedOrders.Count == 0)
            {
                return 0;
            }

            int accuracy = _instrumentsCache.GetInstrumentById(instrument).Accuracy;

            return Math.Round(matchedOrders.Sum(item => item.Price * item.Volume) /
                              matchedOrders.Sum(item => item.Volume), accuracy);
        }
    }
}
