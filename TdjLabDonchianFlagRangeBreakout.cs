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
        private TdjDonchianFlagPoleSetup flagPoleSetup;

        private string entryLongSignalName = @"EntryLong";
        private string entryShortSignalName = @"EntryShort";

        private Order entryLong;
        private Order entryShort;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"An example strategy using the Donchian Flag Pole Setup that you can use as a starting point";
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
                LoadExtremeBreak = false;
                BodyAveragePeriod = 3;
                PoleMinBars = 1;
                PoleMaxBars = 4;
                AverageBodySizeRatio = 1.5;
                ExtensionSizeMinATRMultiples = 1;
                AverageBarExtensionATRMultiples = 0.3;
                MinAverageCloseRatio = 0.5;
                SetupType = TdjDonchianFlagPoleSetupProperties.SetupType.FormationBreakout;
                MinPullbackBars = 1;
                MaxPullbackBars = 5;
                ComparisonDCPeriod = 100;
                PoleDCRangeMinPercentage = 10;
                PoleDCRangeMaxPercentage = 50;
                MinPullbackPercentage = 30;
                MaxPullbackPercentage = 100;
                ImpulseExtremeBreak = false;

                StartTime = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
                EndTime = DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);
                RewardRisk = 1.5;
            }
            else if (State == State.Configure)
            {
            }
            else if (State == State.DataLoaded)
            {
                flagPoleSetup = TdjDonchianFlagPoleSetup(DonchianChannelPeriod, LoadPeriod, LoadExtremeBreak, BodyAveragePeriod, PoleMinBars, PoleMaxBars, AverageBodySizeRatio, ExtensionSizeMinATRMultiples, AverageBarExtensionATRMultiples, MinAverageCloseRatio, SetupType, MinPullbackBars, MaxPullbackBars, ComparisonDCPeriod, PoleDCRangeMinPercentage, PoleDCRangeMaxPercentage, MinPullbackPercentage, MaxPullbackPercentage, ImpulseExtremeBreak);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 2) return;

            if (Position.MarketPosition == MarketPosition.Flat
                && intoTimeInterval(Time[0]))
            {
                manageEntry();
            }
        }

        private bool intoTimeInterval(DateTime currentTime)
        {
            return currentTime.TimeOfDay >= StartTime.TimeOfDay && currentTime.TimeOfDay < EndTime.TimeOfDay;
        }

        private void manageEntry()
        {
            if (flagPoleSetup.SignalUp[0])
            {
                setLongTargetAndStop();
                EnterLongStopMarket(Convert.ToInt32(DefaultQuantity), (flagPoleSetup.SignalUpUpperLevel[0] + 1 * TickSize), entryLongSignalName);
            }

            if (flagPoleSetup.SignalDown[0])
            {
                setShortTargetAndStop();
                EnterShortStopMarket(Convert.ToInt32(DefaultQuantity), (flagPoleSetup.SignalDownLowerLevel[0] - 1 * TickSize), entryShortSignalName);
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
            double stopLossPrice = flagPoleSetup.SignalUpLowerLevel[0] - 1 * TickSize;
            double risk = (flagPoleSetup.SignalUpUpperLevel[0] + 1 * TickSize) - (flagPoleSetup.SignalUpLowerLevel[0] - 1 * TickSize);
            double reward = risk * RewardRisk;
            double targetPrice = Instrument.MasterInstrument.RoundToTickSize(flagPoleSetup.SignalUpUpperLevel[0] + 1 * TickSize + reward);

            SetStopLoss(entryLongSignalName, CalculationMode.Price, stopLossPrice, false);
            SetProfitTarget(entryLongSignalName, CalculationMode.Price, targetPrice, false);
        }

        private void setShortTargetAndStop()
        {
            double stopLossPrice = flagPoleSetup.SignalDownUpperLevel[0] + 1 * TickSize;
            double risk = (flagPoleSetup.SignalDownUpperLevel[0] + 1 * TickSize) - (flagPoleSetup.SignalDownLowerLevel[0] - 1 * TickSize);
            double reward = risk * RewardRisk;
            double targetPrice = Instrument.MasterInstrument.RoundToTickSize(flagPoleSetup.SignalDownLowerLevel[0] - 1 * TickSize - reward);
            SetStopLoss(entryShortSignalName, CalculationMode.Price, stopLossPrice, false);
            SetProfitTarget(entryShortSignalName, CalculationMode.Price, targetPrice, false);
        }

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
        [Display(Name = "Load Extreme Break", Description = "When enabled, the bar that made a new high or low during the loading phase, must be broken", Order = 3, GroupName = "001 Detector Parameters")]
        public bool LoadExtremeBreak
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Body Average Period", Description = "Number of bars considered the average body size calculation.", Order = 4, GroupName = "001 Detector Parameters")]
        public int BodyAveragePeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole Min Bars", Description = "Minimum number of bars required in the flag pole.", Order = 5, GroupName = "001 Detector Parameters")]
        public int PoleMinBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole Max Bars", Description = "Maximum number of bars allowed in the flag pole.", Order = 6, GroupName = "001 Detector Parameters")]
        public int PoleMaxBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Average Body Size Ratio", Description = "Maximum number of bars allowed in the flag pole.", Order = 7, GroupName = "001 Detector Parameters")]
        public double AverageBodySizeRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Extension Size Min ATR Multiples", Description = "The minimum size of the extension in the direction of the flag pole in ATR multiples", Order = 8, GroupName = "001 Detector Parameters")]
        public double ExtensionSizeMinATRMultiples
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Average Bar Extension ATR Multiples", Description = "The average bar extension of the flag measured in ATR multiples", Order = 9, GroupName = "001 Detector Parameters")]
        public double AverageBarExtensionATRMultiples
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Min Average Close Ratio", Description = "The minimum average close ratio of the bars forming the pole in the direction of the flag pole", Order = 10, GroupName = "001 Detector Parameters")]
        public double MinAverageCloseRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Setup Type", Description = "The type of setup", Order = 1, GroupName = "002 Parameters")]
        public TdjDonchianFlagPoleSetupProperties.SetupType SetupType
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Min Pullback Bars", Order = 2, GroupName = "002 Parameters")]
        public int MinPullbackBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Pullback Bars", Order = 3, GroupName = "002 Parameters")]
        public int MaxPullbackBars
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Comparison DC Period", Order = 4, GroupName = "002 Parameters")]
        public int ComparisonDCPeriod
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole DC Range Min Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 5, GroupName = "002 Parameters")]
        public int PoleDCRangeMinPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Pole DC Range Max Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 6, GroupName = "002 Parameters")]
        public int PoleDCRangeMaxPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Pullback Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 7, GroupName = "002 Parameters")]
        public int MinPullbackPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Pullback Percentage", Description = "The maximum percentage of the flag pole relative to the range of Donchian Channel.", Order = 8, GroupName = "002 Parameters")]
        public int MaxPullbackPercentage
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Impulse Extreme Break", Description = "When enabled, the bar that made a new high or low, must be broken", Order = 9, GroupName = "002 Parameters")]
        public bool ImpulseExtremeBreak
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time", Order = 1, GroupName = "003 Strategy Parameters")]
        public DateTime StartTime
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time", Order = 2, GroupName = "003 Strategy Parameters")]
        public DateTime EndTime
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Reward/Risk", Order = 3, GroupName = "003 Strategy Parameters")]
        public double RewardRisk
        { get; set; }
        #endregion

    }
}
