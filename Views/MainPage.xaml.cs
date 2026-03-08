using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using slicer.Services;

namespace slicer.Views;

public sealed partial class MainPage : Page
{
    private SoftwareBitmap? _currentImage;
    private string? _currentFilePath;

    public MainPage()
    {
        InitializeComponent();
    }

    private void UpdateImageState(bool hasImage)
    {
        SliceSizeTextBox.IsEnabled = hasImage;
        SliceHorizontalButton.IsEnabled = hasImage && IsValidSliceSize();
        SliceVerticalButton.IsEnabled = hasImage && IsValidSliceSize();
        SaveButton.IsEnabled = hasImage && !string.IsNullOrEmpty(_currentFilePath);
        SaveAsButton.IsEnabled = hasImage;
        if (hasImage && _currentImage != null)
            StatusText.Text = $"Dimensions: {_currentImage.PixelWidth} x {_currentImage.PixelHeight} px";
        else
            StatusText.Text = "No image loaded";
    }

    private bool IsValidSliceSize()
    {
        if (string.IsNullOrWhiteSpace(SliceSizeTextBox.Text))
            return false;
        return int.TryParse(SliceSizeTextBox.Text.Trim(), out int val) && val > 0;
    }

    private void OnSliceSizeTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateImageState(_currentImage != null);
    }

    private void OnImageDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.Handled = true;
        }
    }

    private async void OnImageDrop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;

            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.FirstOrDefault() as StorageFile;
            if (file == null)
                return;

            if (!IsSupportedImageFile(file))
            {
                StatusText.Text = $"Unsupported format: {file.FileType}";
                return;
            }

            await LoadImageFromFile(file);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static bool IsSupportedImageExtension(string extension)
    {
        var ext = extension.ToLowerInvariant().TrimStart('.').Trim();
        return ext is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "tiff" or "tif";
    }

    private static bool IsSupportedImageFile(StorageFile file)
    {
        var ext = file.FileType.ToLowerInvariant().TrimStart('.').Trim();
        if (ext is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "tiff" or "tif")
            return true;
        var pathExt = System.IO.Path.GetExtension(file.Path).ToLowerInvariant().TrimStart('.').Trim();
        return pathExt is "png" or "jpg" or "jpeg" or "bmp" or "gif" or "tiff" or "tif";
    }

    private async Task LoadImageFromFile(StorageFile file)
    {
        try
        {
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            _currentImage = softwareBitmap;
            _currentFilePath = file.Path;
            await DisplayImage(softwareBitmap);
            UpdateImageState(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading image: {ex.Message}";
        }
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".tiff");
        picker.FileTypeFilter.Add(".tif");

        var file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        await LoadImageFromFile(file);
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
        {
            StatusText.Text = "No image to save";
            return;
        }
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            StatusText.Text = "No file path. Use Save As to choose a location.";
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(_currentFilePath);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            stream.Size = 0;

            var ext = System.IO.Path.GetExtension(file.Path).ToLowerInvariant();
            Guid encoderId = ext switch
            {
                ".png" => BitmapEncoder.PngEncoderId,
                ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
                ".bmp" => BitmapEncoder.BmpEncoderId,
                ".gif" => BitmapEncoder.GifEncoderId,
                ".tiff" or ".tif" => BitmapEncoder.TiffEncoderId,
                _ => BitmapEncoder.PngEncoderId
            };

            var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            encoder.SetSoftwareBitmap(_currentImage);
            if (encoderId == BitmapEncoder.JpegEncoderId)
            {
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                var props = new BitmapPropertySet();
                var quality = new BitmapTypedValue(0.9, Windows.Foundation.PropertyType.Single);
                props.Add("ImageQuality", quality);
                await encoder.BitmapProperties.SetPropertiesAsync(props);
            }
            await encoder.FlushAsync();

            StatusText.Text = $"Saved: {file.Name}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving image: {ex.Message}";
        }
    }

    private async void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
        {
            StatusText.Text = "No image to save";
            return;
        }

        var picker = new FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeChoices.Add("PNG", new[] { ".png" });
        picker.FileTypeChoices.Add("JPEG", new[] { ".jpg", ".jpeg" });
        picker.FileTypeChoices.Add("BMP", new[] { ".bmp" });
        picker.FileTypeChoices.Add("GIF", new[] { ".gif" });
        picker.FileTypeChoices.Add("TIFF", new[] { ".tiff", ".tif" });
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var file = await picker.PickSaveFileAsync();
        if (file == null)
            return;

        try
        {
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            stream.Size = 0;

            Guid encoderId = file.FileType.ToLowerInvariant() switch
            {
                ".png" => BitmapEncoder.PngEncoderId,
                ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
                ".bmp" => BitmapEncoder.BmpEncoderId,
                ".gif" => BitmapEncoder.GifEncoderId,
                ".tiff" or ".tif" => BitmapEncoder.TiffEncoderId,
                _ => BitmapEncoder.PngEncoderId
            };

            var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            encoder.SetSoftwareBitmap(_currentImage);
            if (encoderId == BitmapEncoder.JpegEncoderId)
            {
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                var props = new BitmapPropertySet();
                var quality = new BitmapTypedValue(0.9, Windows.Foundation.PropertyType.Single);
                props.Add("ImageQuality", quality);
                await encoder.BitmapProperties.SetPropertiesAsync(props);
            }
            await encoder.FlushAsync();

            _currentFilePath = file.Path;
            StatusText.Text = $"Saved: {file.Name}";
            UpdateImageState(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error saving image: {ex.Message}";
        }
    }

    private void OnSliceHorizontalClick(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null || !IsValidSliceSize())
            return;

        int size = int.Parse(SliceSizeTextBox.Text.Trim());
        _currentImage = ImageSlicerService.SliceHorizontal(_currentImage, size);
        _ = DisplayImage(_currentImage);
        UpdateImageState(true);
    }

    private void OnSliceVerticalClick(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null || !IsValidSliceSize())
            return;

        int size = int.Parse(SliceSizeTextBox.Text.Trim());
        _currentImage = ImageSlicerService.SliceVertical(_currentImage, size);
        _ = DisplayImage(_currentImage);
        UpdateImageState(true);
    }

    private async Task DisplayImage(SoftwareBitmap bitmap)
    {
        var source = new SoftwareBitmapSource();
        await source.SetBitmapAsync(bitmap);
        ImageDisplay.Source = source;
        // Cap at natural size so we never scale up, only down when too large
        ImageDisplay.MaxWidth = bitmap.PixelWidth;
        ImageDisplay.MaxHeight = bitmap.PixelHeight;
    }
}
