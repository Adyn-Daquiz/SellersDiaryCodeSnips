using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microcharts;
using Microcharts.Forms;
using MobileDiary.Core;
using SkiaSharp;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileDiary.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ReportView : ContentPage
    {
        #region Fields

        private readonly List<ChartEntry> _profitEntries = new List<ChartEntry>();
        private readonly List<ChartEntry> _disburseEntries = new List<ChartEntry>();
        private readonly List<ChartEntry> _customerEntries = new List<ChartEntry>();

        #endregion

        #region Constructor

        public ReportView()
        {
            InitializeComponent();
        }

        #endregion

        #region Events

        private void ContentPage_Appearing(object sender, EventArgs e)
        {
            GenerateReports();
        }

        private void PckReport_SelectedIndexChanged(object sender, EventArgs e)
        {
            HideAllSections();

            switch (PckReport.SelectedIndex)
            {
                case 0:
                    SlMonthlyProfit.IsVisible = true;
                    break;
                case 1:
                    SlMonthlyDisburse.IsVisible = true;
                    break;
                case 2:
                    SlMonthlyNoOfCustomer.IsVisible = true;
                    break;
                case 3:
                    SlPendingOrders.IsVisible = true;
                    GetPendingOrders();
                    SearchedPendingOrders();
                    break;
                case 4:
                    SlOverallRecord.IsVisible = true;
                    SearchedOverallRecord();
                    break;
            }
        }

        private void SelectReport_Click(object sender, EventArgs e)
        {
            DisableButtonTemporarily(BtnReport);
            PckReport.Focus();
        }

        private void DpDeliveries_DateSelected(object sender, DateChangedEventArgs e)
        {
            SearchedPendingOrders();
        }

        private void DpOvrAllDeliveries_DateSelected(object sender, DateChangedEventArgs e)
        {
            SearchedOverallRecord();
        }

        #endregion

        #region Reports

        private void GenerateReports()
        {
            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                UpdateDeliveredOrders(unitOfWork);
                GenerateProfitReport(unitOfWork);
                GenerateDisburseReport(unitOfWork);
                GenerateCustomerReport(unitOfWork);
            }
        }

        private void UpdateDeliveredOrders(UnitOfWork unitOfWork)
        {
            var delivered = unitOfWork.Solds
                .GetAll()
                .Where(x => x.DateDelivered.Date <= DateTime.Now.Date && x.Status == "ORDERED");

            if (delivered.Any())
            {
                foreach (var order in delivered)
                    order.Status = "DELIVERED";

                unitOfWork.Solds.UpdateRange(delivered);

                if (unitOfWork.Complete() <= 0)
                    throw new Exception("Updating Orders Failed!");
            }
        }

        private void GenerateProfitReport(UnitOfWork unitOfWork)
        {
            var solds = unitOfWork.Solds.GetAll().OrderBy(x => x.DateDelivered).ToList();

            var grouped = from s in solds
                          group s by new { s.DateDelivered.Month, s.DateDelivered.Year }
                into g
                          select new
                          {
                              g.Key.Month,
                              g.Key.Year,
                              Profit = g.Sum(x => x.Profit)
                          };

            foreach (var g in grouped)
            {
                _profitEntries.Add(new ChartEntry((float)g.Profit)
                {
                    Label = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Month) + " " + g.Year,
                    ValueLabel = g.Profit.ToString("N")
                });
            }

            TxtProfitNet.Text = solds.Sum(x => x.Profit).ToString("N");
            TxtNoTransaction.Text = solds.GroupBy(x => x.OrderNo).Count().ToString();
            TxtSalesPerformance.Text = CalculateSalesPerformance(solds) + " %";

            MyLineChartProfit.Chart = CreateLineChart(_profitEntries);
        }

        private void GenerateDisburseReport(UnitOfWork unitOfWork)
        {
            var stocks = unitOfWork.Stocks.GetAll().OrderBy(x => x.DateOrdered).ToList();

            var grouped = from s in stocks
                          group s by new { s.DateOrdered.Month, s.DateOrdered.Year }
                into g
                          select new
                          {
                              g.Key.Month,
                              g.Key.Year,
                              Total = g.Sum(x => x.TotalPrice)
                          };

            foreach (var g in grouped)
            {
                _disburseEntries.Add(new ChartEntry((float)g.Total)
                {
                    Label = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Month) + " " + g.Year,
                    ValueLabel = g.Total.ToString("N")
                });
            }

            var items = unitOfWork.Items.GetAll().Where(x => x.Active).ToList();
            double totalQty = items.Sum(x => x.Quantity);
            double totalCritical = items.Sum(x => x.Critical);
            double computeCritical = (totalQty / (totalCritical * 2)) * 100;

            TxtDisburse.Text = stocks.Sum(x => x.TotalPrice).ToString("N");
            TxtTotalQty.Text = stocks.Sum(x => x.Quantity).ToString();
            TxtCritical.Text = items.Count(x => x.Critical > x.Quantity).ToString();
            TxtItemHandling.Text = Math.Round(Math.Min(100, Math.Max(0, computeCritical)), 2) + " %";

            MyLineChartDisburse.Chart = CreateLineChart(_disburseEntries);
        }

        private void GenerateCustomerReport(UnitOfWork unitOfWork)
        {
            var solds = unitOfWork.Solds.GetAll().OrderBy(x => x.DateDelivered).ToList();

            var grouped = from s in solds
                          group s by new { s.DateDelivered.Month, s.DateDelivered.Year }
                into g
                          select new
                          {
                              g.Key.Month,
                              g.Key.Year,
                              Customers = g.Select(x => x.CustomerName).Distinct().Count()
                          };

            foreach (var g in grouped)
            {
                _customerEntries.Add(new ChartEntry(g.Customers)
                {
                    Label = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Month) + " " + g.Year,
                    ValueLabel = g.Customers.ToString()
                });
            }

            TxtTotalNewCustomer.Text = solds.Select(x => x.CustomerName).Distinct().Count().ToString();
            MyBarChartCustomer.Chart = CreateBarChart(_customerEntries);
        }

        #endregion

        #region Record Helpers

        private void GetOverallRecord()
        {
            ActInd.IsRunning = true;

            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                LvSolds.ItemsSource = unitOfWork.Solds.GetAll()
                    .Where(x => x.Status == "DELIVERED")
                    .OrderBy(x => x.DateDelivered)
                    .GroupBy(x => x.OrderNo)
                    .Select(x => new
                    {
                        OrderNo = x.First().OrderNo,
                        CustomerName = "Custr: " + x.First().CustomerName,
                        DateOrdered = "Ord. Date: " + x.First().DateOrdered.Date,
                        DateDelivered = "Dlv. Date: " + x.First().DateDelivered.Date,
                        Profit = "Profit: " + x.Sum(y => y.Profit),
                        NetAmount = "Net Amount: " + x.Sum(y => y.TotalPrice)
                    }).ToList();

                LvDisburse.ItemsSource = unitOfWork.Stocks.GetAll()
                    .OrderBy(x => x.DateOrdered)
                    .GroupBy(x => x.ReceiptNo)
                    .Select(x => new
                    {
                        ReceiptNo = x.First().ReceiptNo,
                        SupplierName = "Suplr: " + unitOfWork.Suppliers.Get(x.First().SupplierId).Fullname,
                        DateOrdered = "Ord. Date: " + x.First().DateOrdered.Date,
                        NetAmount = "Net Amount: " + x.Sum(y => y.TotalPrice)
                    }).ToList();
            }

            ActInd.IsRunning = false;
        }

        private void SearchedOverallRecord()
        {
            ActInd.IsRunning = true;

            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                DateTime selectedDate = DpOvrAllDeliveries.Date.Date;

                var soldList = unitOfWork.Solds.GetAll()
                    .Where(x => x.Status == "DELIVERED" && x.DateOrdered.Date == selectedDate)
                    .OrderBy(x => x.DateDelivered)
                    .GroupBy(x => x.OrderNo)
                    .Select(x => new
                    {
                        OrderNo = x.First().OrderNo,
                        CustomerName = "Custr: " + x.First().CustomerName,
                        DateOrdered = "Ord. Date: " + x.First().DateOrdered.Date,
                        DateDelivered = "Dlv. Date: " + x.First().DateDelivered.Date,
                        Profit = "Profit: " + x.Sum(y => y.Profit),
                        NetAmount = "Net Amount: " + x.Sum(y => y.TotalPrice),
                        VarProfit = x.Sum(y => y.Profit),
                        VarNetWorth = x.Sum(y => y.TotalPrice)
                    }).ToList();

                LvSolds.ItemsSource = soldList;
                TxtSrTotalTransaction.Text = soldList.Count.ToString();
                TxtSrTotalProfit.Text = soldList.Sum(x => x.VarProfit).ToString("N");
                TxtSrTotalNet.Text = soldList.Sum(x => x.VarNetWorth).ToString("N");

                var disburseList = unitOfWork.Stocks.GetAll()
                    .Where(x => x.DateOrdered.Date == selectedDate)
                    .OrderBy(x => x.DateOrdered)
                    .GroupBy(x => x.ReceiptNo)
                    .Select(x => new
                    {
                        ReceiptNo = x.First().ReceiptNo,
                        SupplierName = "Suplr: " + unitOfWork.Suppliers.Get(x.First().SupplierId).Fullname,
                        DateOrdered = "Ord. Date: " + x.First().DateOrdered.Date,
                        NetAmount = "Net Amount: " + x.Sum(y => y.TotalPrice),
                        VarNetWorth = x.Sum(y => y.TotalPrice)
                    }).ToList();

                LvDisburse.ItemsSource = disburseList;
                TxtDrTotalTransaction.Text = disburseList.Count.ToString();
                TxtDrTotalNet.Text = disburseList.Sum(x => x.VarNetWorth).ToString("N");
            }

            ActInd.IsRunning = false;
        }

        private void GetPendingOrders()
        {
            ActInd.IsRunning = true;

            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                var pendingOrders = unitOfWork.Solds.GetAll()
                    .Where(x => x.Status == "ORDERED")
                    .GroupBy(x => x.OrderNo)
                    .Select(x => new
                    {
                        OrderNo = x.First().OrderNo,
                        CustomerName = "Custr: " + x.First().CustomerName,
                        DateOrdered = "Ord. Date: " + x.First().DateOrdered.Date,
                        DateDelivered = "Dlv. Date: " + x.First().DateDelivered.Date,
                        RemainingDays = "Remaining Days: " +
                                        ((x.First().DateDelivered.Date - DateTime.Now.Date).TotalDays <= 0
                                            ? "NOW"
                                            : (x.First().DateDelivered.Date - DateTime.Now.Date).TotalDays.ToString()),
                        NetAmount = "Net Amount: " + x.Sum(y => y.TotalPrice)
                    }).ToList();

                LvPendings.ItemsSource = pendingOrders;
            }

            ActInd.IsRunning = false;
        }

        private void SearchedPendingOrders()
        {
            ActInd.IsRunning = true;

            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                DateTime selectedDate = DpDeliveries.Date.Date;

                var searchedOrders = unitOfWork.Solds.GetAll()
                    .Where(x => x.DateDelivered.Date == selectedDate)
                    .GroupBy(x => x.OrderNo)
                    .Select(x => new
                    {
                        OrderNo = x.First().OrderNo,
                        CustomerName = "Custr: " + x.First().CustomerName,
                        DateOrdered = "Ord. Date: " + x.First().DateOrdered.Date,
                        DateDelivered = "Dlv. Date: " + x.First().DateDelivered.Date,
                        RemainingDays = "Remaining Days: " +
                                        ((x.First().DateDelivered.Date - DateTime.Now.Date).TotalDays <= 0
                                            ? "DELIVERED"
                                            : (x.First().DateDelivered.Date - DateTime.Now.Date).TotalDays.ToString()),
                        NetAmount = "Net Amount: " + x.Sum(y => y.TotalPrice)
                    }).ToList();

                LvSearchedPendings.ItemsSource = searchedOrders;
            }

            ActInd.IsRunning = false;
        }

        #endregion

        #region Helpers

        private string CalculateSalesPerformance(IEnumerable<MobileDiary.Core.Models.Sold> solds)
        {
            double totalSales = solds.Sum(x => x.TotalPrice);
            double baseCost = solds.Sum(x => ((x.TotalPrice / x.Quantity) - x.Profit) +
                                             (((x.TotalPrice / x.Quantity) - x.Profit) * 0.1));

            if (baseCost == 0) return "0";

            double performance = (totalSales / baseCost) * 100;
            performance = Math.Min(100, Math.Max(0, performance));

            return Math.Round(performance, 2).ToString();
        }

        private Chart CreateLineChart(IEnumerable<ChartEntry> entries)
        {
            return new LineChart
            {
                Entries = entries,
                LineSize = 2,
                PointSize = 10
            };
        }

        private Chart CreateBarChart(IEnumerable<ChartEntry> entries)
        {
            return new BarChart
            {
                Entries = entries
            };
        }

        private void HideAllSections()
        {
            SlHome.IsVisible = false;
            SlMonthlyProfit.IsVisible = false;
            SlMonthlyDisburse.IsVisible = false;
            SlMonthlyNoOfCustomer.IsVisible = false;
            SlOverallRecord.IsVisible = false;
            SlPendingOrders.IsVisible = false;
        }

        private void DisableButtonTemporarily(Button button, double seconds = 0.5)
        {
            button.IsEnabled = false;
            button.BackgroundColor = Color.Gray;

            Device.StartTimer(TimeSpan.FromSeconds(seconds), () =>
            {
                button.IsEnabled = true;
                button.BackgroundColor = Color.Black;
                return false;
            });
        }

        #endregion
    }
}
