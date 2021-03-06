﻿using System;
using Atomex.Blockchain.Abstract;
using Atomex.Client.Wpf.Controls;
using Atomex.Client.Wpf.Properties;
using Atomex.Client.Wpf.ViewModels.Abstract;
using Atomex.Core;
using Atomex.MarketData.Abstract;

namespace Atomex.Client.Wpf.ViewModels.SendViewModels
{
    public class Fa12SendViewModel : SendViewModel
    {
        public Fa12SendViewModel()
            : base()
        {
        }

        public Fa12SendViewModel(
            IAtomexApp app,
            IDialogViewer dialogViewer,
            Currency currency)
            : base(app, dialogViewer, currency)
        {
        }

        protected override async void UpdateAmount(decimal amount)
        {
            IsAmountUpdating = true;

            var previousAmount = _amount;
            _amount = amount;

            try
            {
                if (UseDefaultFee)
                {
                    var (maxAmount, maxFeeAmount, _) = await App.Account
                        .EstimateMaxAmountToSendAsync(Currency.Name, To, BlockchainTransactionType.Output, true);

                    var availableAmount = Currency is BitcoinBasedCurrency
                        ? CurrencyViewModel.AvailableAmount
                        : maxAmount;

                    var estimatedFeeAmount = _amount != 0
                        ? (_amount < availableAmount
                            ? await App.Account.EstimateFeeAsync(Currency.Name, To, _amount, BlockchainTransactionType.Output)
                            : null)
                        : 0;

                    if (estimatedFeeAmount == null)
                    {
                        if (maxAmount > 0)
                        {
                            _amount = maxAmount;
                            estimatedFeeAmount = maxFeeAmount;
                        }
                        else
                        {
                            _amount = previousAmount;
                            Warning = Resources.CvInsufficientFunds;
                            return;
                        }
                    }

                    if (_amount > availableAmount)
                        _amount = Math.Max(availableAmount, 0);

                    if (_amount == 0)
                        estimatedFeeAmount = 0;

                    OnPropertyChanged(nameof(AmountString));

                    _fee = Currency.GetFeeFromFeeAmount(estimatedFeeAmount.Value, Currency.GetDefaultFeePrice());
                    OnPropertyChanged(nameof(FeeString));

                    Warning = string.Empty;
                }
                else
                {
                    var (maxAmount, maxFeeAmount, _) = await App.Account
                        .EstimateMaxAmountToSendAsync(Currency.Name, To, BlockchainTransactionType.Output, false);

                    var availableAmount = Currency is BitcoinBasedCurrency
                        ? CurrencyViewModel.AvailableAmount
                        : maxAmount;

                    if (_amount > availableAmount)
                        _amount = Math.Max(availableAmount , 0);

                    OnPropertyChanged(nameof(AmountString));

                    if (_fee != 0)
                        Fee = _fee;

                    Warning = string.Empty;
                }

                OnQuotesUpdatedEventHandler(App.QuotesProvider, EventArgs.Empty);
            }
            finally
            {
                IsAmountUpdating = false;
            }
        }

        protected override async void UpdateFee(decimal fee)
        {
            if (_amount == 0)
            {
                _fee = 0;
                return;
            }

            try
            {
                IsFeeUpdating = true;

                _fee = Math.Min(fee, Currency.GetMaximumFee());

                if (!UseDefaultFee)
                {
                    var estimatedFeeAmount = _amount != 0
                        ? await App.Account.EstimateFeeAsync(Currency.Name, To, _amount, BlockchainTransactionType.Output)
                        : 0;

                    var feeAmount = _fee;

                    if (feeAmount < estimatedFeeAmount.Value)
                        _fee = estimatedFeeAmount.Value;

                    if (_amount == 0)
                        _fee = 0;

                    OnPropertyChanged(nameof(AmountString));

                    OnPropertyChanged(nameof(FeeString));
                    Warning = string.Empty;
                }

                OnQuotesUpdatedEventHandler(App.QuotesProvider, EventArgs.Empty);
            }
            finally
            {
                IsFeeUpdating = false;
            }
        }

        protected override void OnQuotesUpdatedEventHandler(object sender, EventArgs args)
        {
            if (!(sender is ICurrencyQuotesProvider quotesProvider))
                return;

            var quote = quotesProvider.GetQuote(CurrencyCode, BaseCurrencyCode);
            var xtzQuote = quotesProvider.GetQuote("XTZ", BaseCurrencyCode);

            AmountInBase = Amount * (quote?.Bid ?? 0m);
            FeeInBase = Fee * (xtzQuote?.Bid ?? 0m);
        }
    }
}