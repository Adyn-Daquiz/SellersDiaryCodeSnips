using MobileDiary.Core;
using MobileDiary.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileDiary.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class TransactionView : ContentPage
    {
        #region Fields

        private bool _isBusy;
        private readonly ObservableCollection<Sold> _obsSold = new ObservableCollection<Sold>();
        private readonly ObservableCollection<Cart> _obsCart = new ObservableCollection<Cart>();

        #endregion

        #region Constructor

        public TransactionView()
        {
            InitializeComponent();
        }

        #endregion

        #region Helpers

        private void ClearCartDetails()
        {
            TxtItemName.Text = string.Empty;
            TxtQuantity.Text = string.Empty;
            TxtPrice.Text = string.Empty;
            TxtOriginalPrice.Text = string.Empty;
            PckUom.SelectedIndex = -1;
            LblReQty.Text = "0";
            TxtTotalPrice.Text = "0.00";
            TxtTotalProfit.Text = "0.00";
        }

        private void ClearAll()
        {
            _obsSold.Clear();
            _obsCart.Clear();
            TxtCustomer.Text = string.Empty;
            GetNextOrderNo();
            TxtNetAmount.Text = "0.00";
            TxtNetProfit.Text = "0.00";
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

        private bool SetBusy()
        {
            if (_isBusy) return true;
            _isBusy = true;
            return false;
        }

        private void ClearBusy()
        {
            _isBusy = false;
        }

        private void GetNextOrderNo()
        {
            try
            {
                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    var latestOrder = unitOfWork.Solds.GetAll().Any()
                        ? unitOfWork.Solds.GetLatest().OrderNo
                        : "ORD#0";

                    var currentNo = int.Parse(latestOrder.Split('#').Last());
                    TxtOrderNo.Text = $"ORD#{currentNo + 1}";
                }
            }
            catch
            {
                TxtOrderNo.Text = "ORD#1";
            }
        }

        private void UpdateNetTotals()
        {
            TxtNetAmount.Text = _obsSold.Sum(x => x.TotalPrice).ToString("N");
            TxtNetProfit.Text = _obsSold.Sum(x => x.Profit).ToString("N");
        }

        #endregion

        #region Events

        private void ContentPage_Appearing(object sender, EventArgs e)
        {
            GetNextOrderNo();
        }

        private void TxtItemName_TextChanged(object sender, TextChangedEventArgs e)
        {
            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                var items = unitOfWork.Items.GetWithName(TxtItemName.Text);

                if (!items.Any())
                {
                    LblReQty.Text = "ITEM DOESN'T EXIST";
                    PckUom.ItemsSource = null;
                    return;
                }

                LblReQty.Text = "PLEASE SELECT UNIT OF MEASUREMENT";
                PckUom.ItemsSource = items.Select(x => x.UnitOfMeasurement).ToList();
            }
        }

        private void TxtPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var price = Convert.ToDouble(TxtPrice.Text);
                var quantity = Convert.ToDouble(TxtQuantity.Text);
                var originalPrice = Convert.ToDouble(TxtOriginalPrice.Text);

                TxtTotalProfit.Text = ((price - originalPrice) * quantity).ToString("N2");
                TxtTotalPrice.Text = (price * quantity).ToString("N2");
            }
            catch
            {
                // ignore formatting errors
            }
        }

        private void PckUom_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    var item = unitOfWork.Items.GetWithNameUom(TxtItemName.Text, PckUom.SelectedItem != null ? PckUom.SelectedItem.ToString() : null);

                    if (item == null || !item.Active)
                    {
                        LblReQty.Text = "ITEM DOESN'T EXIST";
                        return;
                    }

                    var itemStock = unitOfWork.Stocks.GetWithItemNameUomList(TxtItemName.Text, PckUom.SelectedItem.ToString());
                    LblReQty.Text = unitOfWork.Items.Get(item.Id).Quantity.ToString();
                    TxtOriginalPrice.Text = itemStock.OrderByDescending(x => x.DateOrdered)
                                                    .Select(x => x.PricePerUnitOfMeasurement)
                                                    .FirstOrDefault()
                                                    .ToString();
                }
            }
            catch
            {
                PckUom.ItemsSource = null;
                ClearCartDetails();
            }
        }

        private void AddToCart_Click(object sender, EventArgs e)
        {
            if (SetBusy()) return;

            try
            {
                DisableButtonTemporarily(BtnAddCard);

                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    var item = unitOfWork.Items.GetWithNameUom(TxtItemName.Text, PckUom.SelectedItem != null ? PckUom.SelectedItem.ToString() : null);

                    if (item == null)
                    {
                        DisplayAlert("Notice", "Item doesn't exist!", "OK");
                        return;
                    }

                    if (_obsSold.Any(x => x.ItemId == item.Id))
                    {
                        DisplayAlert("Notice", item.Name + " already in the cart", "OK");
                        return;
                    }

                    // Add to Sold
                    _obsSold.Add(new Sold
                    {
                        ItemId = item.Id,
                        OrderNo = TxtOrderNo.Text,
                        CustomerName = TxtCustomer.Text,
                        DateOrdered = DpDateOrdered.Date,
                        DateDelivered = DpDateDelivered.Date,
                        Quantity = int.Parse(TxtQuantity.Text),
                        TotalPrice = double.Parse(TxtTotalPrice.Text),
                        Profit = double.Parse(TxtTotalProfit.Text),
                        Status = DpDateDelivered.Date > DateTime.Now.Date ? "ORDERED" : "DELIVERED"
                    });

                    // Add to Cart UI
                    _obsCart.Add(new Cart
                    {
                        ItemId = item.Id,
                        CustomerName = TxtCustomer.Text,
                        OrderNo = TxtOrderNo.Text,
                        ItemName = "Item: " + item.Name,
                        Price = "Price: " + double.Parse(TxtPrice.Text).ToString("0.00") + " (" + TxtQuantity.Text + " " + PckUom.SelectedItem + ")",
                        TotalProfit = "Profit: " + double.Parse(TxtTotalProfit.Text).ToString("0.00"),
                        TotalPrice = "- " + double.Parse(TxtTotalPrice.Text).ToString("0.00") + " -",
                        DateOrdered = "Ord. " + DpDateOrdered.Date.ToShortDateString()
                    });

                    LvCart.ItemsSource = _obsCart;

                    ClearCartDetails();
                    UpdateNetTotals();

                    if (_obsSold.Any())
                    {
                        TxtCustomer.IsEnabled = false;
                        DpDateDelivered.IsEnabled = false;
                        DpDateOrdered.IsEnabled = false;
                    }
                }
            }
            catch
            {
                DisplayAlert("Notice", "Please fill-up empty fields", "OK");
            }
            finally
            {
                ClearBusy();
            }
        }

        private async void Purchase_Click(object sender, EventArgs e)
        {
            if (SetBusy()) return;

            try
            {
                DisableButtonTemporarily(BtnPurchase);

                var confirm = await DisplayAlert("Purchase", "Are you sure?", "Yes", "No");
                if (!confirm) return;

                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    foreach (var o in _obsSold)
                    {
                        var dbItem = unitOfWork.Items.Get(o.ItemId);
                        dbItem.Quantity -= o.Quantity;
                    }

                    unitOfWork.Solds.AddRange(_obsSold);

                    if (unitOfWork.Complete() <= 0)
                        throw new Exception("Save failed!");
                }

                await DisplayAlert("Success", "Transaction Complete!", "OK");
                ClearAll();

                TxtCustomer.IsEnabled = true;
                DpDateDelivered.IsEnabled = true;
                DpDateOrdered.IsEnabled = true;
            }
            catch
            {
                await DisplayAlert("Save Failed", "Please check your items in Cart", "OK");
            }
            finally
            {
                ClearBusy();
            }
        }

        private void LvCartDelete(object sender, EventArgs e)
        {
            try
            {
                var btn = sender as ImageButton;
                if (btn == null || btn.CommandParameter == null) return;

                var itemId = int.Parse(btn.CommandParameter.ToString());
                var sold = _obsSold.FirstOrDefault(x => x.ItemId == itemId);
                var cart = _obsCart.FirstOrDefault(x => x.ItemId == itemId);

                if (sold != null) _obsSold.Remove(sold);
                if (cart != null) _obsCart.Remove(cart);

                LvCart.ItemsSource = _obsCart;
                UpdateNetTotals();

                if (!_obsSold.Any())
                {
                    TxtCustomer.IsEnabled = true;
                    DpDateDelivered.IsEnabled = true;
                    DpDateOrdered.IsEnabled = true;
                }
            }
            catch
            {
                DisplayAlert("Deletion Failed", "Please report this bug to https://ntns.multiscreensite.com", "OK");
            }
        }

        #endregion

        #region Nested Class

        private class Cart
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; }
            public string CustomerName { get; set; }
            public string OrderNo { get; set; }
            public string DateOrdered { get; set; }
            public string Quantity { get; set; }
            public string Price { get; set; }
            public string TotalPrice { get; set; }
            public string TotalProfit { get; set; }
        }

        #endregion
    }
}
