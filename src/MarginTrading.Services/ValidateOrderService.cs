﻿using System;
using MarginTrading.Core;
using MarginTrading.Core.Exceptions;
using MarginTrading.Core.Messages;
using MarginTrading.Services.Helpers;

namespace MarginTrading.Services
{
    public class ValidateOrderService : IValidateOrderService
    {
        private readonly IQuoteCacheService _quoteCashService;
        private readonly IAccountUpdateService _accountUpdateService;
        private readonly IAccountsCacheService _accountsCacheService;
        private readonly IAccountAssetsCacheService _accountAssetsCacheService;
        private readonly IInstrumentsCache _instrumentsCache;

        public ValidateOrderService(
            IQuoteCacheService quoteCashService,
            IAccountUpdateService accountUpdateService,
            IAccountsCacheService accountsCacheService,
            IAccountAssetsCacheService accountAssetsCacheService,
            IInstrumentsCache instrumentsCache)
        {
            _quoteCashService = quoteCashService;
            _accountUpdateService = accountUpdateService;
            _accountsCacheService = accountsCacheService;
            _accountAssetsCacheService = accountAssetsCacheService;
            _instrumentsCache = instrumentsCache;
        }

        //has to be beyond global lock
        public void Validate(Order order)
        {
            if (order.Volume == 0)
            {
                throw new ValidateOrderException(OrderRejectReason.InvalidVolume, "Volume cannot be 0");
            }

            var asset = _instrumentsCache.GetInstrumentById(order.Instrument);
            order.AssetAccuracy = asset.Accuracy;

            var account = _accountsCacheService.Get(order.ClientId, order.AccountId);

            order.AccountAssetId = account.BaseAssetId;
            order.TradingConditionId = account.TradingConditionId;

            var quote = _quoteCashService.GetQuote(order.Instrument);

            //check ExpectedOpenPrice for pending order
            if (order.ExpectedOpenPrice.HasValue)
            {
                if (order.ExpectedOpenPrice <= 0)
                {
                    throw new ValidateOrderException(OrderRejectReason.InvalidExpectedOpenPrice, "Incorrect expected open price");
                }

                order.ExpectedOpenPrice = Math.Round(order.ExpectedOpenPrice ?? 0, order.AssetAccuracy);

                if (order.GetOrderType() == OrderDirection.Buy && order.ExpectedOpenPrice > quote.Ask ||
                    order.GetOrderType() == OrderDirection.Sell && order.ExpectedOpenPrice < quote.Bid)
                {
                    var reasonText = order.GetOrderType() == OrderDirection.Buy
                        ? string.Format(MtMessages.Validation_PriceAboveAsk, order.ExpectedOpenPrice, quote.Ask)
                        : string.Format(MtMessages.Validation_PriceBelowBid, order.ExpectedOpenPrice, quote.Bid);

                    throw new ValidateOrderException(OrderRejectReason.InvalidExpectedOpenPrice, reasonText, $"{order.Instrument} quote (bid/ask): {quote.Bid}/{quote.Ask}");
                }
            }

            var accountAsset = _accountAssetsCacheService.GetAccountAsset(order.TradingConditionId, order.AccountAssetId, order.Instrument);

            //check TP/SL
            if (order.TakeProfit.HasValue)
            {
                order.TakeProfit = Math.Round(order.TakeProfit.Value, order.AssetAccuracy);
            }

            if (order.StopLoss.HasValue)
            {
                order.StopLoss = Math.Round(order.StopLoss.Value, order.AssetAccuracy);
            }

            ValidateOrderStops(order.GetOrderType(), quote, accountAsset.DeltaBid, accountAsset.DeltaAsk, order.TakeProfit, order.StopLoss, order.ExpectedOpenPrice, order.AssetAccuracy);

            if (!_accountUpdateService.IsEnoughBalance(order))
            {
                throw new ValidateOrderException(OrderRejectReason.NotEnoughBalance, MtMessages.Validation_NotEnoughBalance, $"Account available balance is {account.GetTotalCapital()}");
            }
        }

        public void ValidateOrderStops(OrderDirection type, BidAskPair quote, double deltaBid, double deltaAsk, double? takeProfit,
            double? stopLoss, double? expectedOpenPrice, int assetAccuracy)
        {
            double deltaBidValue = MarginTradingCalculations.GetVolumeFromPoints(deltaBid, assetAccuracy);
            double deltaAskValue = MarginTradingCalculations.GetVolumeFromPoints(deltaAsk, assetAccuracy);

            if (expectedOpenPrice.HasValue)
            {
                double minGray;
                double maxGray;

                //check TP/SL for pending order
                if (type == OrderDirection.Buy)
                {
                    minGray = Math.Round(expectedOpenPrice.Value - 2 * deltaBidValue, assetAccuracy);
                    maxGray = Math.Round(expectedOpenPrice.Value + deltaAskValue, assetAccuracy);

                    if (takeProfit.HasValue && takeProfit > 0 && takeProfit < maxGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidTakeProfit,
                            string.Format(MtMessages.Validation_TakeProfitMustBeMore, Math.Round((double) takeProfit, assetAccuracy), maxGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }

                    if (stopLoss.HasValue && stopLoss > 0 && stopLoss > minGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidStoploss,
                            string.Format(MtMessages.Validation_StopLossMustBeLess, Math.Round((double) stopLoss, assetAccuracy), minGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }
                }
                else
                {
                    minGray = Math.Round(expectedOpenPrice.Value - deltaBidValue, assetAccuracy);
                    maxGray = Math.Round(expectedOpenPrice.Value + 2 * deltaAskValue, assetAccuracy);

                    if (takeProfit.HasValue && takeProfit > 0 && takeProfit > minGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidTakeProfit,
                            string.Format(MtMessages.Validation_TakeProfitMustBeLess, Math.Round((double) takeProfit, assetAccuracy), minGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }

                    if (stopLoss.HasValue && stopLoss > 0 && stopLoss < maxGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidStoploss,
                            string.Format(MtMessages.Validation_StopLossMustBeMore, Math.Round((double) stopLoss, assetAccuracy), maxGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }
                }
            }
            else
            {
                //check TP/SL for market order
                double minGray = Math.Round(quote.Bid - deltaBidValue, assetAccuracy);
                double maxGray = Math.Round(quote.Ask + deltaAskValue, assetAccuracy);

                if (type == OrderDirection.Buy)
                {
                    if (takeProfit.HasValue && takeProfit > 0 && takeProfit < maxGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidTakeProfit,
                            string.Format(MtMessages.Validation_TakeProfitMustBeMore, Math.Round((double) takeProfit, assetAccuracy), maxGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }

                    if (stopLoss.HasValue && stopLoss > 0 && stopLoss > minGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidStoploss,
                            string.Format(MtMessages.Validation_StopLossMustBeLess, Math.Round((double) stopLoss, assetAccuracy), minGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }
                }
                else
                {
                    if (takeProfit.HasValue && takeProfit > 0 && takeProfit > minGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidTakeProfit,
                            string.Format(MtMessages.Validation_TakeProfitMustBeLess, Math.Round((double) takeProfit, assetAccuracy), minGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }

                    if (stopLoss.HasValue && stopLoss > 0 && stopLoss < maxGray)
                    {
                        throw new ValidateOrderException(OrderRejectReason.InvalidStoploss,
                            string.Format(MtMessages.Validation_StopLossMustBeMore,
                                Math.Round((double) stopLoss, assetAccuracy), maxGray),
                            $"quote (bid/ask): {quote.Bid}/{quote.Ask}");
                    }
                }
            }
        }
    }
}
