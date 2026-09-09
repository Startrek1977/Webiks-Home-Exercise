namespace RoverRally.App.Views
{
    /// <summary>
    /// The track canvas's fixed pixel geometry - shared, neutral ground
    /// between <see cref="TrackView"/> (which draws to it) and
    /// <see cref="ViewModels.StationViewModel"/> (which needs the same
    /// numbers to compute the start line's real-world position via
    /// <see cref="RoverRally.Core.Geo.TrackProjection.Unproject"/>, without
    /// either one referencing the other - see #73). Must match the Canvas
    /// dimensions and the "Start / finish" Rectangle in TrackView.xaml
    /// (Canvas.Left="298" Canvas.Top="103" Width="4" Height="34").
    /// </summary>
    public static class MapMetrics
    {
        public const double CanvasWidth = 720;
        public const double CanvasHeight = 480;

        public const double StartLineX = 300;
        public const double StartLineTopY = 103;
        public const double StartLineBottomY = 137;
    }
}
