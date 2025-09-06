using MobileDiary.Core;
using MobileDiary.Core.Models;
using Plugin.Media.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileDiary.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class OrdersView : ContentPage
    {
        private bool _isRunning;
        private StackLayout _selectedLayout;

        public OrdersView()
        {
            InitializeComponent();
            _isRunning = false;
        }

        private void TxtOrderNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    var listSold = unitOfWork.Solds.GetWithOrderNo(TxtOrderNo.Text)
                        .Select(x => new
                        {
                            ItemName = x.Item.Name,
                            TotalPrice = "Total Price: " + x.TotalPrice.ToString(),
                            Quantity = "Quantity: " + x.Quantity.ToString(),
                            Customer = x.CustomerName,
                            DlrDate = x.DateDelivered.Date,
                            OrdDate = x.DateOrdered.Date,
                            PlainTotalPrice = x.TotalPrice,
                            PlainProfit = x.Profit,
                            Status = x.Status
                        })
                        .ToList();

                    if (listSold.Count == 0)
                    {
                        ClearDetails();
                        return;
                    }

                    var firstOrder = listSold.First();

                    LvCart.ItemsSource = listSold;
                    LblCustomer.Text = "Customer: " + firstOrder.Customer;
                    LblDeliveredDate.Text = "Dlvr. " + firstOrder.DlrDate.ToString("MM/dd/yyyy");
                    LblOrderedDate.Text = "Ord. " + firstOrder.OrdDate.ToString("MM/dd/yyyy");
                    LblNet.Text = "Net: " + listSold.Sum(x => x.PlainTotalPrice).ToString("N");
                    LblProfit.Text = "Profit: " + listSold.Sum(x => x.PlainProfit).ToString("N");
                    LblStatus.Text = firstOrder.Status;
                }
            }
            catch (Exception ex)
            {
                // TODO: log exception (ex) if needed
            }
        }

        private void ClearDetails()
        {
            LvCart.ItemsSource = null;
            LblCustomer.Text = "Customer: Name";
            LblDeliveredDate.Text = "Dlvr. 00/00/0000";
            LblOrderedDate.Text = "Ord. 00/00/0000";
            LblNet.Text = "Net: 0.00";
            LblProfit.Text = "Profit: 0.00";
            LblStatus.Text = "STATUS";
        }

        private void ContentPage_Appearing(object sender, EventArgs e)
        {
            // If preload data, uncomment:
            /*
            try
            {
                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    LvOrderNos.ItemsSource = unitOfWork.Solds.GetAllOrderNoDistinct();
                }
            }
            catch (Exception ex)
            {
                // TODO: log exception (ex) if needed
            }
            */
        }

        private void Manual_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (ChkManual.IsChecked)
            {
                TxtOrderNo.IsVisible = true;
                FrOrderNo.IsVisible = false;
                rdSearch.HeightRequest = 60;
            }
            else
            {
                rdSearch.HeightRequest = 150;
                TxtOrderNo.IsVisible = false;
                FrOrderNo.IsVisible = true;
            }
        }

        private void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            try
            {
                if (_isRunning)
                    return;

                _isRunning = true;

                var tappedArgs = (TappedEventArgs)e;
                var orderNo = tappedArgs.Parameter != null ? tappedArgs.Parameter.ToString() : string.Empty;

                TxtOrderNo.Text = orderNo;

                if (_selectedLayout != null)
                    _selectedLayout.BackgroundColor = Color.Transparent;

                var entity = sender as StackLayout;
                if (entity != null)
                {
                    entity.BackgroundColor = Color.Gray;
                    _selectedLayout = entity;
                }

                _isRunning = false;
            }
            catch (Exception ex)
            {
                // TODO: log exception (ex) if needed
                _isRunning = false;
            }
        }

        private async void Delete_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (_isRunning)
                    return;

                _isRunning = true;

                var confirm = await DisplayAlert("Delete", "Are you sure?", "Yes", "No");
                if (!confirm)
                {
                    _isRunning = false;
                    return;
                }

                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    var solds = unitOfWork.Solds.GetWithOrderNo(TxtOrderNo.Text);
                    unitOfWork.Solds.RemoveRange(solds);

                    if (unitOfWork.Complete() <= 0)
                        throw new Exception("Save Failed!");

                    await DisplayAlert("Success", "Deleted!", "OK");
                    TxtOrderNo.Text = string.Empty;
                }

                _isRunning = false;
            }
            catch (Exception ex)
            {
                // TODO: log exception (ex) if needed
                _isRunning = false;
            }
        }
    }
}
