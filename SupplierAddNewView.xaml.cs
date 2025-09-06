using MobileDiary.Core;
using MobileDiary.Core.Models;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileDiary.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SupplierAddNewView : ContentPage
    {
        private bool _isBusy;
        private string _imagePath = string.Empty;

        public SupplierAddNewView()
        {
            InitializeComponent();
        }

        #region Event Handlers

        private void Save_Clicked(object sender, EventArgs e)
        {
            if (SetBusy()) return;

            try
            {
                DisableButtonTemporarily(BtnSave);

                if (string.IsNullOrWhiteSpace(TxtFullname.Text))
                {
                    DisplayAlert("Notice", "Please enter supplier name.", "OK");
                    return;
                }

                using (var unitOfWork = new UnitOfWork(new DatabaseContext()))
                {
                    var existing = unitOfWork.Suppliers.GetWithFullName(TxtFullname.Text);
                    if (existing != null && existing.Active)
                    {
                        DisplayAlert("Notice", "Supplier already exists.", "OK");
                        return;
                    }

                    var newSupplier = new Supplier
                    {
                        Fullname = TxtFullname.Text,
                        Address = TxtAddress.Text,
                        ContactNo = TxtContactNo.Text,
                        Email = TxtEmail.Text,
                        Active = true,
                        ImagePath = _imagePath
                    };

                    unitOfWork.Suppliers.Add(newSupplier);

                    if (unitOfWork.Complete() <= 0)
                        throw new Exception("Save failed!");

                    DisplayAlert("Saved", "Supplier saved successfully!", "OK");
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                ClearBusy();
            }
        }

        private async void ChooseImage_Clicked(object sender, EventArgs e)
        {
            if (SetBusy()) return;

            try
            {
                DisableButtonTemporarily(BtnImage);

                var status = await CrossPermissions.Current.CheckPermissionStatusAsync<StoragePermission>();
                if (status != PermissionStatus.Granted)
                {
                    status = await CrossPermissions.Current.RequestPermissionAsync<StoragePermission>();
                }

                await CrossMedia.Current.Initialize();

                if (!CrossMedia.Current.IsPickPhotoSupported)
                {
                    await DisplayAlert("Not supported", "Your device does not currently support this functionality.", "OK");
                    return;
                }

                var mediaOptions = new PickMediaOptions { PhotoSize = PhotoSize.Medium };
                var selectedImageFile = await CrossMedia.Current.PickPhotoAsync(mediaOptions);

                if (selectedImageFile == null)
                {
                    await DisplayAlert("Error", "Could not get the image. Please try again.", "OK");
                    return;
                }

                _imagePath = selectedImageFile.Path;
                ImgSelected.Source = ImageSource.FromStream(() => selectedImageFile.GetStream());
            }
            catch
            {
                await DisplayAlert("Notice", "No image selected.", "OK");
            }
            finally
            {
                ClearBusy();
            }
        }

        private void Clear_Clicked(object sender, EventArgs e)
        {
            DisableButtonTemporarily(BtnClear);
            ClearForm();
        }

        #endregion

        #region Helpers

        private void ClearForm()
        {
            TxtFullname.Text = string.Empty;
            TxtEmail.Text = string.Empty;
            TxtAddress.Text = string.Empty;
            TxtContactNo.Text = string.Empty;
            ImgSelected.Source = null;
            _imagePath = string.Empty;
        }

        private void DisableButtonTemporarily(Button button)
        {
            button.IsEnabled = false;
            button.BackgroundColor = Color.Gray;

            Device.StartTimer(TimeSpan.FromSeconds(0.5), () =>
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
