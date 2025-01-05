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
using NinjaTrader.NinjaScript.Indicators.TradingDJ.Bias;
using NinjaTrader.NinjaScript.Indicators.TradingDJ.Patterns;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.TradingDJStrategyLab
{
    public class TdjLabDonchianFlagRangeBreakout : Strategy
    {
        private TdjDonchianFlagPoleBias flagPoleBias;
        private TdjDonchianFlagPoleDetector flagPoleDetector;

        private FlagRange flagRangeUp;
        private FlagRange flagRangeDown;

        private string entryLongSignalName = @"EntryLong";
        private string entryShortSignalName = @"EntryShort";

        private Order entryLong;
        private Order entryShort;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"An example strategy using the Donchian Flag Pole Detector that you can use as a starting point";
                Name = "Donchian Flag Range Breakout";
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
                RewardRisk = 1.5;
            }
            else if (State == State.Configure)
            {
            }
            else if (State == State.DataLoaded)
            {
                flagPoleDetector = TdjDonchianFlagPoleDetector(DonchianChannelPeriod, LoadPeriod, BodyAveragePeriod, PoleMinBars, PoleMaxBars, AverageBodySizeRatio, ExtensionSizeMinATRMultiples, AverageBarExtensionATRMultiples, MinAverageCloseRatio);
                flagPoleBias = TdjDonchianFlagPoleBias(DonchianChannelPeriod, LoadPeriod, BodyAveragePeriod, PoleMinBars, PoleMaxBars, AverageBodySizeRatio, ExtensionSizeMinATRMultiples, AverageBarExtensionATRMultiples, MinAverageCloseRatio, ComparisonDCPeriod, PoleDCRangeMinPercentage, PoleDCRangeMaxPercentage, MinPullbackPercentage, MaxPullbackPercentage, MaxBiasValidity, InvertBiasDirection);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 2) return;

            manageFlagRange();
            manageFlagRangeCreation();

            if (Position.MarketPosition == MarketPosition.Flat
                && intoTimeInterval(Time[0]))
            {
                manageEntry();
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
            return currentTime.TimeOfDay >= StartTime.TimeOfDay && currentTime.TimeOfDay < EndTime.TimeOfDay;
        }

        private void manageEntry()
        {
            if (flagRangeUp != null 
                && flagRangeUp.PullbackBars >= MinPullbackBars
                && flagRangeUp.PullbackBars <= MaxPullbackBars
                && flagPoleBias.BiasUp[0] > 0)
            {
                setLongTargetAndStop();
                EnterLongStopMarket(Convert.ToInt32(DefaultQuantity), (flagRangeUp.High + 1 * TickSize), entryLongSignalName);
            }

            if (flagRangeDown != null
                && flagRangeDown.PullbackBars >= MinPullbackBars
                && flagRangeDown.PullbackBars <= MaxPullbackBars
                && flagPoleBias.BiasDown[0] > 0)
            {
                setShortTargetAndStop();
                EnterShortStopMarket(Convert.ToInt32(DefaultQuantity), (flagRangeDown.Low - 1 * TickSize), entryShortSignalName);
            }
        }

        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice,
            Cbi.OrderState orderState, DateTime time, Cbi.ErrorCode error, string comment)
        {
            if (order.Name == entryLongSignalName)
            {
                entryLong = order;
            }

            if (entryLong != null && entryLong == order)
            {
                if (entryLong.OrderState == OrderState.Cancelled)
                {
                    entryLong = null;
                }
            }

            if (order.Name == entryShortSignalName)
            {
                entryShort = order;
            }

            if (entryShort != null && entryShort == order)
            {
                if (entryShort.OrderState == OrderState.Cancelled)
                {
                    entryShort = null;
                }
            }
        }

        private void setLongTargetAndStop()
        {
            double stopLossPrice = Low[0] - 1 * TickSize;
            double risk = (flagRangeUp.High + 1 * TickSize) - (Low[0] - 1 * TickSize);
            double reward = risk * RewardRisk;
            double targetPrice = Instrument.MasterInstrument.RoundToTickSize(flagRangeUp.High + 1 * TickSize + reward);

            SetStopLoss(entryLongSignalName, CalculationMode.Price, stopLossPrice, false);
            SetProfitTarget(entryLongSignalName, CalculationMode.Price, targetPrice, false);
        }

        private void setShortTargetAndStop()
        {
            double stopLossPrice = High[0] + 1 * TickSize;
            double risk = (High[0] + 1 * TickSize) - (flagRangeDown.Low - 1 * TickSize);
            double reward = risk * RewardRisk;
            double targetPrice = Instrument.MasterInstrument.RoundToTickSize(flagRangeDown.Low - 1 * TickSize - reward);
            SetStopLoss(entryShortSignalName, CalculationMode.Price, stopLossPrice, false);
            SetProfitTarget(entryShortSignalName, CalculationMode.Price, targetPrice, false);
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
        [Display(Name = "Min PullbackBars", Description = "The minimum number of pullback bars. In this context we consider pullback bar a bar that does not make a new high or low", Order = 1, GroupName = "003 Strategy Parameters")]
        public int MinPullbackBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Max Pull backBars", Order = 2, GroupName = "003 Strategy Parameters")]
        public int MaxPullbackBars
        { get; set; }
        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time", Order = 3, GroupName = "003 Strategy Parameters")]
        public DateTime StartTime
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time", Order = 4, GroupName = "003 Strategy Parameters")]
        public DateTime EndTime
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Reward/Risk", Order = 5, GroupName = "003 Strategy Parameters")]
        public double RewardRisk
        { get; set; }
        #endregion

    }
}
