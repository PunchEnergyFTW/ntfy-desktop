using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NtfyDesktop.Features.Feed;

public partial class FeedPage : Page
{
    // Compose stage sizing constants
    private const double CompactWidth = 320;
    private const double NormalWidth = 450;
    private const double ExpandedWidth = 850;

    private const double CompactMaxHeight = 36;
    private const double CompactMinHeight = 36;

    private const double NormalMaxHeight = 120;
    private const double NormalMinHeight = 56;

    private const double ExpandedMaxHeight = 300;
    private const double ExpandedMinHeight = 100;

    private static readonly PowerEase DefaultEase = new() { Power = 3, EasingMode = EasingMode.EaseOut };

    public FeedPage(FeedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Listen for state changes
        ComposeCardBorder.IsKeyboardFocusWithinChanged += OnComposeCardFocusChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnComposeCardFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ApplyComposeState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FeedViewModel.IsPublishExpanded))
        {
            ApplyComposeState();
        }
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FeedViewModel vm)
        {
            vm.IsPublishExpanded = false;
        }

        // Move focus out of the compose card; this will trigger
        // OnComposeCardFocusChanged → ApplyComposeState again,
        // but since we animate with SnapshotAndReplace, the second
        // call just smoothly continues to the same target.
        MessagesList.Focus();
    }

    /// <summary>
    /// Determines the current compose stage and smoothly animates
    /// the Border's MaxWidth and the TextBox's MinHeight/MaxHeight.
    /// Uses BeginAnimation with SnapshotAndReplace handoff, so each
    /// call automatically picks up the current animated value as
    /// its starting point — no snapping, no flashing.
    /// </summary>
    private void ApplyComposeState()
    {
        bool isFocused = ComposeCardBorder.IsKeyboardFocusWithin;
        bool isExpanded = DataContext is FeedViewModel vm && vm.IsPublishExpanded;

        double targetWidth;
        double targetMinHeight;
        double targetMaxHeight;
        Duration duration;

        if (isExpanded)
        {
            // Stage 3: Expanded
            targetWidth = ExpandedWidth;
            targetMinHeight = ExpandedMinHeight;
            targetMaxHeight = ExpandedMaxHeight;
            duration = new Duration(TimeSpan.FromMilliseconds(300));
        }
        else if (isFocused)
        {
            // Stage 2: Normal
            targetWidth = NormalWidth;
            targetMinHeight = NormalMinHeight;
            targetMaxHeight = NormalMaxHeight;
            duration = new Duration(TimeSpan.FromMilliseconds(250));
        }
        else
        {
            // Stage 1: Compact
            targetWidth = CompactWidth;
            targetMinHeight = CompactMinHeight;
            targetMaxHeight = CompactMaxHeight;
            duration = new Duration(TimeSpan.FromMilliseconds(200));
        }

        // Animate Border MaxWidth
        var widthAnim = new DoubleAnimation
        {
            To = targetWidth,
            Duration = duration,
            EasingFunction = DefaultEase,
        };
        ComposeCardBorder.BeginAnimation(Border.MaxWidthProperty, widthAnim);

        // Set MinHeight directly (no animation needed, avoids coercion bugs)
        MessageInput.MinHeight = targetMinHeight;

        // Animate TextBox MaxHeight
        var heightAnim = new DoubleAnimation
        {
            To = targetMaxHeight,
            Duration = duration,
            EasingFunction = DefaultEase,
        };
        MessageInput.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
    }
}
