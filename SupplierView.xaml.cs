using MobileDiary.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileDiary.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SupplierView : ContentPage
    {
        private bool _isBusy;
        private StackLayout _selectedLayout;

        public SupplierView()
        {
            InitializeComponent();
        }

        #region Event Handlers

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshSuppliers(e.NewTextValue);
        }

        private void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            var tapped = e as TappedEventArgs;
            var layout = sender as StackLayout;

            if (layout != null && tapped != null)
            {
                ImgSelected.Source = tapped.Parameter != null
                    ? tapped.Parameter.ToString()
                    : null;

                HighlightSelection(layout);
            }
        }

        private async void AddNewSupplier(object sender, EventArgs e)
        {
            if (SetBusy()) return;

            try
            {
                DisableButtonTemporarily(BtnAddNew);
                await Navigation.PushAsync(new SupplierAddNewView());
            }
            finally
            {
                ClearBusy();
            }
        }

        private void LoadSupplier(object sender, EventArgs e)
        {
            RefreshSuppliers();
        }

        private async void ItemDelete_Click(object sender, EventArgs e)
        {
            if (SetBusy()) return;

            try
            {
                var confirm = await DisplayAlert("Delete", "Are you sure?", "Yes", "No");
                if (!confirm)
                    return;

                var btn = sender as ImageButton;
                if (btn != null)
                {
                    int supplierId;
                    if (int.TryParse(btn.CommandParameter != null
                        ? btn.CommandParameter.ToString()
                        : string.Empty, out supplierId))
                    {
                        using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                        {
                            var supplier = unitOfWork.Suppliers.Get(supplierId);
                            if (supplier != null)
                            {
                                supplier.Active = false;
                                if (unitOfWork.Complete() <= 0)
                                    throw new Exception("Save failed!");
                            }
                        }
                    }
                }

                RefreshSuppliers();
            }
            finally
            {
                ClearBusy();
            }
        }

        #endregion

        #region Helpers

        private void RefreshSuppliers(string filter = null)
        {
            using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
            {
                var suppliers = unitOfWork.Suppliers
                    .GetAll()
                    .Where(x => x.Active);

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    suppliers = suppliers.Where(x => x.Fullname.StartsWith(
                        filter, StringComparison.InvariantCultureIgnoreCase));
                }

                LvSupplier.ItemsSource = suppliers.ToList();
            }
        }

        private void HighlightSelection(StackLayout layout)
        {
            if (_selectedLayout != null)
                _selectedLayout.BackgroundColor = Color.Transparent;

            layout.BackgroundColor = Color.LightGray;
            _selectedLayout = layout;
        }

        private void DisableButtonTemporarily(Button button)
        {
            button.IsEnabled = false;
            button.BackgroundColor = Color.Gray;

            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
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

        #endregion
    }
}
