// Copyright Structured Trading LLC. © 2025. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace DailySimpleMovingAverage
{
    /// <summary>
    /// Simple Moving Average calculated on daily bars
    /// </summary>
    public class DailySimpleMovingAverage : Indicator
    {
        private const string LOG_PREFIX = "DSMA";

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
        //private Indicator _dailySma;

        // Track initialization state
        private bool _initialized = false;

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

            try
            {
                if (Symbol == null)
                {
                    Log("Init failed: Symbol is null", LoggingLevel.Error);
                    return;
                }

                if (!LoadDailyHistory())
                {
                    return;
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                Log($"Init failed with exception: {ex.Message}\n{ex.StackTrace}", LoggingLevel.Error);
            }
        }

        private bool LoadDailyHistory()
        {
            int calendarDaysBack = LookbackPeriod;
            int maxAttempts = 5;
            int attempt = 0;

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                DateTime startDate = Core.Instance.TimeUtils.DateTimeUtcNow.AddDays(-calendarDaysBack);

                try
                {
                    _dailyHistory?.Dispose();
                    _dailyHistory = Symbol.GetHistory(
                        Period.DAY1,
                        Symbol.HistoryType,
                        startDate
                    );
                }
                catch (Exception ex)
                {
                    Log($"GetHistory failed: {ex.Message}", LoggingLevel.Error);
                    return false;
                }

                if (_dailyHistory == null)
                {
                    Log("GetHistory returned null", LoggingLevel.Error);
                    return false;
                }

                if (_dailyHistory.Count >= LookbackPeriod)
                {
                    return true;
                }

                // Calculate exactly how many more trading days we need
                int barsNeeded = LookbackPeriod - _dailyHistory.Count;
                calendarDaysBack += barsNeeded;
            }

            Log($"Init failed: Insufficient data after {maxAttempts} attempts. Got {_dailyHistory?.Count ?? 0}, need {LookbackPeriod}", LoggingLevel.Error);
            return false;
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                if (_dailyHistory.Count < LookbackPeriod)
                {
                    Log($"OnUpdate: Insufficient daily bars. Have {_dailyHistory.Count}, need {LookbackPeriod}", LoggingLevel.Error);
                    return;
                }

                double dailyValueSum = GetPrice(HistoryPriceType);

                if (double.IsNaN(dailyValueSum))
                {
                    Log($"OnUpdate: GetPrice returned NaN for {HistoryPriceType}", LoggingLevel.Error);
                    return;
                }

                for (int i = 1; i < LookbackPeriod; i++)
                {
                    var dailyBar = _dailyHistory[i];

                    if (dailyBar == null)
                    {
                        Log($"OnUpdate: Daily bar at index {i} is null", LoggingLevel.Error);
                        return;
                    }

                    double barValue = dailyBar[HistoryPriceType];

                    if (double.IsNaN(barValue))
                    {
                        Log($"OnUpdate: Daily bar[{i}] {HistoryPriceType} is NaN", LoggingLevel.Error);
                        return;
                    }

                    dailyValueSum += barValue;
                }

                double dailySmaValue = dailyValueSum / LookbackPeriod;

                if (double.IsNaN(dailySmaValue) || double.IsInfinity(dailySmaValue))
                {
                    Log($"OnUpdate: Calculated SMA is invalid: {dailySmaValue}", LoggingLevel.Error);
                    return;
                }

                SetValue(dailySmaValue);
            }
            catch (Exception ex)
            {
                Log($"OnUpdate exception: {ex.Message}\n{ex.StackTrace}", LoggingLevel.Error);
            }
        }

        protected override void OnClear()
        {
            try
            {
                if (_dailyHistory != null)
                {
                    _dailyHistory.Dispose();
                    _dailyHistory = null;
                }

                _initialized = false;
            }
            catch (Exception ex)
            {
                Log($"OnClear exception: {ex.Message}", LoggingLevel.Error);
            }

            base.OnClear();
        }

        private void Log(string message, LoggingLevel level = LoggingLevel.Trading)
        {
            Core.Loggers.Log($"[{LOG_PREFIX}] {Symbol?.Name ?? "?"}: {message}", level);
        }
    }
}