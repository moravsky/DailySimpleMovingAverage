// Copyright Structured Trading LLC. © 2025. All rights reserved.

using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DailySimpleMovingAverage
{
    /// <summary>
    /// Simple Moving Average calculated on daily bars
    /// </summary>
    public class DailySimpleMovingAverage : Indicator
    {
        [InputParameter("Period", 0, 1, 999, 1, 0)]
        public int LookbackPeriod = 5;

        [InputParameter("Price Type", 1, variants: new object[]
        {
            "Close", PriceType.Close,
            "Open", PriceType.Open,
            "High", PriceType.High,
            "Low", PriceType.Low,
            "Typical", PriceType.Typical,
            "Median", PriceType.Median,
            "Weighted", PriceType.Weighted
        })]
        public PriceType HistoryPriceType = PriceType.Close;

        // Holds the daily history - auto-updates with new bars
        private HistoricalData _dailyHistory;

        // Built-in SMA indicator applied to daily history
        private Indicator _dailySma;

        public DailySimpleMovingAverage()
            : base()
        {
            Name = "Daily SMA";
            Description = "Simple Moving Average calculated on daily bars";

            AddLineSeries("Daily SMA", Color.CadetBlue, 2, LineStyle.Solid);

            SeparateWindow = false;
            UpdateType = IndicatorUpdateType.OnBarClose;
        }

        protected override void OnInit()
        {
            base.OnInit();

            Name = $"Daily SMA ({LookbackPeriod})";

            // Calculate start date accounting for weekends and holidays
            DateTime startDate = CalculateLookbackDate(Core.Instance.TimeUtils.DateTimeUtcNow, LookbackPeriod);

            // Get daily history - auto-updates because no end date specified
            _dailyHistory = Symbol.GetHistory(
                Period.DAY1,  // Daily bars
                Symbol.HistoryType,
                startDate
            );

            // Create built-in SMA indicator on the daily history
            _dailySma = Core.Indicators.BuiltIn.SMA(LookbackPeriod, PriceType.Close);
            _dailyHistory.AddIndicator(_dailySma);

        }

        private static DateTime CalculateLookbackDate(DateTime endDate, int requiredTradingDays)
        {
            DateTime currentDate = endDate;
            int tradingDaysFound = 0;

            while (tradingDaysFound < requiredTradingDays)
            {
                if (currentDate.DayOfWeek >= DayOfWeek.Monday &&
                    currentDate.DayOfWeek <= DayOfWeek.Friday)
                {
                    tradingDaysFound++;
                }
                currentDate = currentDate.AddDays(-1);
            }

            return currentDate;
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            // Need enough daily bars and valid SMA
            if (_dailyHistory == null || _dailyHistory.Count < LookbackPeriod || _dailySma == null)
            {
                return;
            }

            double dailyValueSum = GetPrice(HistoryPriceType);

            for (int i = 1; i < LookbackPeriod; i++)
            {
                var dailyBar = _dailyHistory[i];
                dailyValueSum += dailyBar[HistoryPriceType];
            }

            // Get the current daily SMA value
            double dailySmaValue = dailyValueSum / LookbackPeriod;

            // Check for valid value
            if (double.IsNaN(dailySmaValue))
            {
                return;
            }

            // Plot the value
            SetValue(dailySmaValue);
        }

        protected override void OnClear()
        {
            if (_dailyHistory != null)
            {
                _dailyHistory.Dispose();
                _dailyHistory = null;
            }

            base.OnClear();
        }
    }
}