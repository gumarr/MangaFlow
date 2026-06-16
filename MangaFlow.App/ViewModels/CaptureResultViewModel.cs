using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace MangaFlow.App.ViewModels;

public partial class CaptureResultViewModel : ObservableObject
{
    [ObservableProperty]
    private BitmapImage? _capturedImage;

    [ObservableProperty]
    private string _dimensions = string.Empty;

    [ObservableProperty]
    private string _timestamp = string.Empty;

    public async Task SetCaptureDataAsync(byte[] imageBytes, double width, double height, DateTime timestamp)
    {
        Dimensions = $"{(int)width} x {(int)height} px";
        Timestamp = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        if (imageBytes == null || imageBytes.Length == 0)
        {
            return;
        }

        try
        {
            var bitmapImage = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(imageBytes);
                    await writer.StoreAsync();
                }
                stream.Seek(0);
                await bitmapImage.SetSourceAsync(stream);
            }
            CapturedImage = bitmapImage;
        }
        catch (Exception)
        {
            // Fallback: don't crash the UI if image loading fails
        }
    }
}
