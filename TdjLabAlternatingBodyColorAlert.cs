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
using NinjaTrader.NinjaScript.Indicators.TradingDJ.Patterns;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.TradingDJStrategyLab
{
    public class TdjLabAlternatingBodyColorAlert : Strategy
    {
        private DonchianChannel dc100;
        private Series<double> dc100Range;
        private EMA emaBodyRange;
        private Series<double> emaBodyRangeDcRangeRatio;
        private Series<bool> rangeFilterOk;

        private TdjAlternatingBodyColor alternatingBodyColor;

        private string soundFilePath;

        private string entryLongSignalName = @"EntryLong";
        private string entryShortSignalName = @"EntryShort";

        private string timeExitLongSignalName = @"ExitTimeLong";
        private string timeExitShortSignalName = @"ExitTimeShort";
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"A strategy to show how to use TdjAlternatingBodyColor by tradingdj.com. Also an example of how to use the Sound Alert";
                Name = "Alternating Body Color V1";
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

                Confirmation = true;

                MinRangeTicks = 70;
                MaxRangeTicks = 150;
                MinAverageBodyRangeDCRangeRatio = 0.15;
                MaxAverageBodyRangeDCRangeRatio = 0.50;

                StartTime = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
                EndTime = DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);
                CloseAtBar = 10;
                Target = 0;
                Stop = 0;
                TrailStop = 0;
                EntryType = TdjLabBaseStrategyProperties.EntryOrderType.StopMarket;

                EnableSoundAlert = false;
                SoundAlertFile = "Alert1.wav";
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
                initSoundFiles();
                alternatingBodyColor = TdjAlternatingBodyColor(Confirmation);
                dc100 = DonchianChannel(100);
                dc100Range = new Series<double>(this);
                emaBodyRange = EMA(TdjBodyRange(), 5);
                emaBodyRangeDcRangeRatio = new Series<double>(this);
                rangeFilterOk = new Series<bool>(this);
            }
        }

        private void initSoundFiles()
        {
            if (SoundAlertFile.Contains("/") || SoundAlertFile.Contains("\\"))
            {
                soundFilePath = SoundAlertFile;
            }
            else
            {
                soundFilePath = NinjaTrader.Core.Globals.InstallDir + @"\sounds\" + SoundAlertFile;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 2) return;

            manageMinRangeFilter();

            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (intoTimeInterval(Time[0]))
                {
                    manageEntry();
                    manageSoundAlert();
                }
            }
            else
            {
                manageExit();
            }
        }

        private void manageSoundAlert()
        {
            if ((alternatingBodyColor.SignalUp[0] || alternatingBodyColor.SignalDown[0]) 
                && !rangeFilterOk[0] && EnableSoundAlert)
            {
                PlaySound(NinjaTrader.Core.Globals.InstallDir + @"\sounds\Alert1.wav");
                PlaySound(soundFilePath);
            }
        }

        #region Range Filter
        private void manageMinRangeFilter()
        {
            dc100Range[0] = dc100.Upper[0] - dc100.Lower[0];
            emaBodyRangeDcRangeRatio[0] = emaBodyRange[0] / dc100Range[0];

            int donchianRangeTicks = Convert.ToInt32(dc100Range[0] / TickSize);
            // You can use this line to print the values in the output window to see the values
            // You can also copy it to Excel to analyze the data
            //Print(Time[0] + ";" + emaBodyRangeDcRangeRatio[0] + ";" + donchianRangeTicks);

            rangeFilterOk[0] = emaBodyRangeDcRangeRatio[0] >= MinAverageBodyRangeDCRangeRatio 
                && emaBodyRangeDcRangeRatio[0] <= MaxAverageBodyRangeDCRangeRatio 
                && donchianRangeTicks >= MinRangeTicks 
                && donchianRangeTicks <= MaxRangeTicks;
        }
        #endregion

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
            if (alternatingBodyColor.SignalUp[0] 
                && rangeFilterOk[0])
            {
                manageLongEntry(High[0] + 1 * TickSize);
            }

            if (alternatingBodyColor.SignalDown[0]
                && rangeFilterOk[0])
            {
                manageShortEntry(Low[0] - 1 * TickSize);
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

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Confirmation Candle", Order = 1, GroupName = "001 Parameters")]
        [Description("If true, it need another candle with the same color to confirm the setup")]
        public bool Confirmation { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Min Range Ticks", Order = 1, GroupName = "002 Donchian Channel Range Filter")]
        public int MinRangeTicks
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Max Range Ticks", Order = 2, GroupName = "002 Donchian Channel Range Filter")]
        public int MaxRangeTicks
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Min Average Body Range / DC Range Ratio", Order = 3, GroupName = "002 Donchian Channel Range Filter")]
        public double MinAverageBodyRangeDCRangeRatio
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Max Average Body Range / DC Range Ratio", Order = 4, GroupName = "002 Donchian Channel Range Filter")]
        public double MaxAverageBodyRangeDCRangeRatio
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "StartTime", Order = 1, GroupName = "003 Strategy Parameters")]
        public DateTime StartTime
        { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "EndTime", Order = 2, GroupName = "003 Strategy Parameters")]
        public DateTime EndTime
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Entry Type", Order = 3, GroupName = "003 Strategy Parameters")]
        public TdjLabBaseStrategyProperties.EntryOrderType EntryType { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Close At Bar", Order = 4, GroupName = "003 Strategy Parameters")]
        public int CloseAtBar
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Target", Order = 5, GroupName = "003 Strategy Parameters")]
        public int Target
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Stop", Order = 6, GroupName = "003 Strategy Parameters")]
        public int Stop
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trail Stop", Order = 7, GroupName = "003 Strategy Parameters")]
        public int TrailStop
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Sound Alert", Order = 4, GroupName = "004 Sound Alert Parameters")]
        public bool EnableSoundAlert { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Sound File", Order = 5, GroupName = "004 Sound Alert Parameters")]
        public string SoundAlertFile { get; set; }
        #endregion
    }
}
