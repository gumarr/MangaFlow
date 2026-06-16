using System;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace MangaFlow.App.Views;

public sealed partial class SelectionWindow : Window
{
    private Point _startPoint;
    private bool _isDragging;
    private readonly TaskCompletionSource<(double X, double Y, double Width, double Height)?> _tcs = new();

    public SelectionWindow()
    {
        this.InitializeComponent();

        // Configure full screen overlay
        var appWindow = this.AppWindow;
        appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

        // Configure cursor to crosshair via CursorGrid implementation

        this.Activated += (s, e) =>
        {
            RootGrid.Focus(FocusState.Programmatic);
        };
    }

    public Task<(double X, double Y, double Width, double Height)?> GetSelectionAsync()
    {
        return _tcs.Task;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(RootGrid);
        if (properties.Properties.IsLeftButtonPressed)
        {
            _startPoint = properties.Position;
            _isDragging = true;
            RootGrid.CapturePointer(e.Pointer);

            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Visibility = Visibility.Visible;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var currentPoint = e.GetCurrentPoint(RootGrid).Position;

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
            SelectionRect.Visibility = Visibility.Collapsed;

            var currentPoint = e.GetCurrentPoint(RootGrid).Position;

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            if (width > 5 && height > 5)
            {
                _tcs.TrySetResult((x, y, width, height));
            }
            else
            {
                _tcs.TrySetResult(null);
            }

            this.Close();
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            _tcs.TrySetResult(null);
            this.Close();
        }
    }
}

public class CursorGrid : Grid
{
    public CursorGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    }
}
