using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;

namespace RoverRally.App.Views
{
    public partial class TrackView : UserControl
    {
        private const int TrailLength = 300;
        private const double CanvasWidth = 720;
        private const double CanvasHeight = 480;

        private static readonly Color[] RoverColours =
        {
            Color.FromRgb(0x3F, 0xA7, 0xD6),
            Color.FromRgb(0x7A, 0xC7, 0x4F),
            Color.FromRgb(0xE8, 0xB0, 0x3A),
            Color.FromRgb(0xD6, 0x5A, 0x4A),
            Color.FromRgb(0xB1, 0x8A, 0xE0)
        };

        private readonly Dictionary<byte, Ellipse> _markers = new Dictionary<byte, Ellipse>();
        private readonly Dictionary<byte, Line> _headings = new Dictionary<byte, Line>();
        private readonly Dictionary<byte, TextBlock> _labels = new Dictionary<byte, TextBlock>();
        private readonly Dictionary<byte, Polyline> _trails = new Dictionary<byte, Polyline>();

        private TrackProjection? _projection;

        public TrackView()
        {
            InitializeComponent();
        }

        public void Configure(double north, double south, double west, double east)
        {
            _projection = new TrackProjection(north, south, west, east, CanvasWidth, CanvasHeight);
        }

        public void SetGeofence(TrackPoint[] fence)
        {
            if (_projection == null) return;

            PointCollection points = new PointCollection();

            foreach (TrackPoint corner in fence)
            {
                double x, y;
                _projection.TryProject(corner, out x, out y);
                points.Add(new Point(x, y));
            }

            GeofencePolygon.Points = points;
        }

        public void UpdateRover(Rover rover)
        {
            if (_projection == null) return;

            double x, y;
            bool onTrack = _projection.TryProject(rover.Position, out x, out y);

            // A rover that has driven off the surveyed area still reports, but
            // there is nowhere sensible to put the marker - or the trail.
            if (!onTrack) return;

            Polyline trail = GetTrail(rover);
            trail.Points.Add(new Point(x, y));

            while (trail.Points.Count > TrailLength)
            {
                trail.Points.RemoveAt(0);
            }

            Ellipse marker = GetMarker(rover);
            Canvas.SetLeft(marker, x - marker.Width / 2);
            Canvas.SetTop(marker, y - marker.Height / 2);

            TextBlock label = _labels[rover.Id];
            Canvas.SetLeft(label, x + 10);
            Canvas.SetTop(label, y - 8);

            Line heading = _headings[rover.Id];
            double radians = (rover.Heading - 90) * System.Math.PI / 180.0;
            heading.X1 = x;
            heading.Y1 = y;
            heading.X2 = x + System.Math.Cos(radians) * 16;
            heading.Y2 = y + System.Math.Sin(radians) * 16;
        }

        public void ClearTrails()
        {
            foreach (Polyline trail in _trails.Values)
            {
                trail.Points.Clear();
            }
        }

        private Ellipse GetMarker(Rover rover)
        {
            Ellipse? marker;
            if (_markers.TryGetValue(rover.Id, out marker)) return marker;

            Color colour = RoverColours[rover.Id % RoverColours.Length];

            marker = new Ellipse();
            marker.Width = 13;
            marker.Height = 13;
            marker.Fill = new SolidColorBrush(colour);
            marker.Stroke = Brushes.White;
            marker.StrokeThickness = 1.5;
            _markers[rover.Id] = marker;
            RoverLayer.Children.Add(marker);

            Line heading = new Line();
            heading.Stroke = Brushes.White;
            heading.StrokeThickness = 1.5;
            _headings[rover.Id] = heading;
            RoverLayer.Children.Add(heading);

            TextBlock label = new TextBlock();
            label.Text = rover.Name;
            label.Foreground = new SolidColorBrush(colour);
            label.FontSize = 11;
            _labels[rover.Id] = label;
            RoverLayer.Children.Add(label);

            return marker;
        }

        private Polyline GetTrail(Rover rover)
        {
            Polyline? trail;
            if (_trails.TryGetValue(rover.Id, out trail)) return trail;

            trail = new Polyline();
            trail.Stroke = new SolidColorBrush(RoverColours[rover.Id % RoverColours.Length]);
            trail.StrokeThickness = 1.6;
            trail.Opacity = 0.65;
            _trails[rover.Id] = trail;
            TrailLayer.Children.Add(trail);

            return trail;
        }
    }
}
