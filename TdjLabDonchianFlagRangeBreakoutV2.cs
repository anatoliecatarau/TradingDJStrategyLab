// Disclaimer:
// This code is provided "AS IS" for educational purposes only. Use at your own risk.
// Futures and forex trading contains substantial risk and is not suitable for every investor.
// Past performance is not necessarily indicative of future results.

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using static TdjBaseStrategyProperties;
using NinjaTrader.NinjaScript.Indicators.TradingDJ.Bias;
using NinjaTrader.NinjaScript.Indicators.TradingDJ.Patterns;
using static NinjaTrader.NinjaScript.Strategies.TradingDJ.SourceCode.TdjFiltersSourceBaseStrategy;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.TradingDJStrategyLab
{
    public class TdjLabDonchianFlagRangeBreakoutV2 : Strategy
    {
        private TdjDonchianFlagPoleBias flagPoleBias;
        private TdjDonchianFlagPoleDetector flagPoleDetector;

        private FlagRange flagRangeUp;
        private FlagRange flagRangeDown;

        private string entryLongSignalName = @"EntryLong";
        private string entryShortSignalName = @"EntryShort";

        private string timeExitLongSignalName = @"ExitTimeLong";
        private string timeExitShortSignalName = @"ExitTimeShort";
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Enter the description for your new custom Strategy here.";
                Name = "Donchian Flag Range Breakout V2";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;

                DonchianChannelPeriod = 5;
                LoadPeriod = 2;
                BodyAveragePeriod = 3;
                PoleMinBars = 1;
                PoleMaxBars = 4;
                AverageBodySizeRatio = 1.5;
                ExtensionSizeMinATRMultiples = 1;
                AverageBarExtensionATRMultiples = 0.3;
                MinAverageCloseRatio = 0.5;

                ComparisonDCPeriod = 100;
                PoleDCRangeMinPercentage = 10;
                PoleDCRangeMaxPercentage = 50;
                MinPullbackPercentage = 30;
                MaxPullbackPercentage = 100;
                MaxBiasValidity = 5;
                InvertBiasDirection = false;

                MinPullbackBars = 1;
                MaxPullbackBars = 5;

                StartTime = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
                EndTime = DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);
                CloseAtBar = 10;
                Target = 0;
                Stop = 0;
                TrailStop = 0;
                EntryType = TdjLabBaseStrategyProperties.EntryOrderType.StopMarket;
            }
            else if (State == State.Configure)
            {
                if (Target != 0)
                {
                    SetProfitTarget(CalculationMode.Ticks, Target);
                }

                if (Stop != 0)
                {
                    SetStopLoss(CalculationMode.Ticks, Stop);
                }

                if (TrailStop != 0)
                {
                    SetTrailStop(CalculationMode.Ticks, TrailStop);
                }
            }
            else if (State == State.DataLoaded)
            {
                flagPoleDetector = TdjDonchianFlagPoleDetector(DonchianChannelPeriod, LoadPeriod, BodyAveragePeriod, PoleMinBars, PoleMaxBars, AverageBodySizeRatio, ExtensionSizeMinATRMultiples, AverageBarExtensionATRMultiples, MinAverageCloseRatio);
                flagPoleBias = TdjDonchianFlagPoleBias(DonchianChannelPeriod, LoadPeriod, BodyAveragePeriod, PoleMinBars, PoleMaxBars, AverageBodySizeRatio, ExtensionSizeMinATRMultiples, AverageBarExtensionATRMultiples, MinAverageCloseRatio, ComparisonDCPeriod, PoleDCRangeMinPercentage, PoleDCRangeMaxPercentage, MinPullbackPercentage, MaxPullbackPercentage, MaxBiasValidity, InvertBiasDirection);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;

            manageFlagRange();
            manageFlagRangeCreation();

            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (intoTimeInterval(Time[0]))
                {
                    manageEntry();
                }
            }
            else
            {
                manageExit();
            }
        }

        private void manageFlagRange()
        {
            if (flagRangeUp != null
                && High[0] <= flagRangeUp.High
                && Low[0] >= flagRangeUp.Low
                && flagRangeUp.PullbackBars <= MaxPullbackBars)
            {
                flagRangeUp.PullbackBars++;
            }
            else
            {
                flagRangeUp = null;
            }

            if (flagRangeDown != null
                && High[0] <= flagRangeDown.High
                && Low[0] >= flagRangeDown.Low
                && flagRangeDown.PullbackBars <= MaxPullbackBars)
            {
                flagRangeDown.PullbackBars++;
            }
            else
            {
                flagRangeDown = null;
            }
        }

        private void manageFlagRangeCreation()
        {
            if (flagPoleDetector.SignalUp[0])
            {
                flagRangeUp = new FlagRange
                {
                    High = flagPoleDetector.SignalUpUpperLevel[0],
                    Low = flagPoleDetector.SignalUpLowerLevel[0],
                    PullbackBars = High[0] <= High[1] ? 1 : 0
                };
            }

            if (flagPoleDetector.SignalDown[0])
            {
                flagRangeDown = new FlagRange
                {
                    High = flagPoleDetector.SignalDownUpperLevel[0],
                    Low = flagPoleDetector.SignalDownLowerLevel[0],
                    PullbackBars = Low[0] >= Low[1] ? 1 : 0
                };
            }

        }

        private bool intoTimeInterval(DateTime currentTime)
        {
            return (currentTime.TimeOfDay >= StartTime.TimeOfDay && currentTime.TimeOfDay < EndTime.TimeOfDay);
        }

        private void manageExit()
        {
            if (BarsSinceEntryExecution(0, entryLongSignalName, 0) == CloseAtBar)
            {
                ExitLong(timeExitLongSignalName, entryLongSignalName);
            }

            if (BarsSinceEntryExecution(0, entryShortSignalName, 0) == CloseAtBar)
            {
                ExitShort(timeExitShortSignalName, entryShortSignalName);
            }
        }

        private void manageEntry()
        {
            if (flagRangeUp != null
                && flagRangeUp.PullbackBars >= MinPullbackBars
                && flagRangeUp.PullbackBars <= MaxPullbackBars
                && flagPoleBias.BiasUp[0] > 0)
            {
                manageLongEntry(flagRangeUp.High + 1 * TickSize);
            }

            if (flagRangeDown != null
                && flagRangeDown.PullbackBars >= MinPullbackBars
                && flagRangeDown.PullbackBars <= MaxPullbackBars
                && flagPoleBias.BiasDown[0] > 0)
            {
                manageShortEntry(flagRangeDown.Low - 1 * TickSize);
            }
        }

        private void manageLongEntry(double entryPrice)
        {
            if (EntryType == TdjLabBaseStrategyProperties.EntryOrderType.StopMarket)
            {
                EnterLongStopMarket(Convert.ToInt32(DefaultQuantity), entryPrice, entryLongSignalName);
            }
            else
            {
                EnterLong(Convert.ToInt32(DefaultQuantity), entryLongSignalName);
            }
        }

        private void manageShortEntry(double entryPrice)
        {
            if (EntryType == TdjLabBaseStrategyProperties.EntryOrderType.StopMarket)
            {
                EnterShortStopMarket(Convert.ToInt32(DefaultQuantity), entryPrice, entryShortSignalName);
            }
            else
            {
                EnterShort(Convert.ToInt32(DefaultQuantity), entryShortSignalName);
            }
        }

        #region Helper classes
        private class FlagRange
        {
            public double High { get; set; }
            public double Low { get; set; }
            public double PullbackBars { get; set; }

        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Donchian Channel Period", Description = "Number of periods used to calculate the Donchian Channel.", Order = 1, GroupName = "001 Detector Parameters")]
        public int DonchianChannelPeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Load Period", Description = "Number of bars considered for the load phase.", Order = 2, GroupName = "001 Detector Parameters")]
        public int LoadPeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Body Average Period", Description = "Number of bars considered the average body size calculation.", Order = 3, GroupName = "001 Detector Parameters")]
        public int BodyAveragePeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole Min Bars", Description = "Minimum number of bars required in the flag pole.", Order = 4, GroupName = "001 Detector Parameters")]
        public int PoleMinBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole Max Bars", Description = "Maximum number of bars allowed in the flag pole.", Order = 5, GroupName = "001 Detector Parameters")]
        public int PoleMaxBars
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Average Body Size Ratio", Description = "Maximum number of bars allowed in the flag pole.", Order = 6, GroupName = "001 Detector Parameters")]
        public double AverageBodySizeRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Extension Size Min ATR Multiples", Description = "The minimum size of the extension in the direction of the flag pole in ATR multiples", Order = 7, GroupName = "001 Detector Parameters")]
        public double ExtensionSizeMinATRMultiples
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Average Bar Extension ATR Multiples", Description = "The average bar extension of the flag measured in ATR multiples", Order = 8, GroupName = "001 Detector Parameters")]
        public double AverageBarExtensionATRMultiples
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Min Average Close Ratio", Description = "The minimum average close ratio of the bars forming the pole in the direction of the flag pole", Order = 9, GroupName = "001 Detector Parameters")]
        public double MinAverageCloseRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Comparison DC Period", Order = 1, GroupName = "002 Parameters")]
        public int ComparisonDCPeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole DC Range Min Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 2, GroupName = "002 Parameters")]
        public int PoleDCRangeMinPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole DC Range Max Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 3, GroupName = "002 Parameters")]
        public int PoleDCRangeMaxPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Pullback Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 4, GroupName = "002 Parameters")]
        public int MinPullbackPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Pullback Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 5, GroupName = "002 Parameters")]
        public int MaxPullbackPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Bias Validity", Order = 6, GroupName = "002 Parameters")]
        public int MaxBiasValidity
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Invert Bias Direction", Order = 7, GroupName = "002 Parameters")]
        public bool InvertBiasDirection
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Min Pullback Bars", Description = "The minimum number of pullback bars. In this context we consider pullback bar a bar that does not make a new high or low", Order = 1, GroupName = "003 Strategy Parameters")]
        public int MinPullbackBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Max Pullback Bars", Order = 2, GroupName = "003 Strategy Parameters")]
        public int MaxPullbackBars
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "StartTime", Order = 3, GroupName = "003 Strategy Parameters")]
        public DateTime StartTime
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "EndTime", Order = 4, GroupName = "003 Strategy Parameters")]
        public DateTime EndTime
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Entry Type", Order = 5, GroupName = "003 Strategy Parameters")]
        public TdjLabBaseStrategyProperties.EntryOrderType EntryType { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Close At Bar", Order = 6, GroupName = "003 Strategy Parameters")]
        public int CloseAtBar
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Target", Order = 7, GroupName = "003 Strategy Parameters")]
        public int Target
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Stop", Order = 8, GroupName = "003 Strategy Parameters")]
        public int Stop
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trail Stop", Order = 9, GroupName = "003 Strategy Parameters")]
        public int TrailStop
        { get; set; }
        #endregion
    }
}
