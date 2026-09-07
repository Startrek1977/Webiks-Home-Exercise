using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;

namespace RoverRally.App.Views
{
    /// <summary>
    /// Draws the track, the geofence, and every rover's marker/trail/heading
    /// on a fixed-size canvas. Reacts to its bound properties rather than
    /// being driven imperatively (#73): the view model sets
    /// <see cref="TrackNorth"/>/<see cref="TrackSouth"/>/<see cref="TrackWest"/>/
    /// <see cref="TrackEast"/>, <see cref="Geofence"/> and <see cref="Rovers"/>
    /// once via binding, and this control redraws itself whenever a bound
    /// rover's own <see cref="Rover.Position"/>/<see cref="Rover.Heading"/>
    /// changes - the same reactive shape a chart control uses for
    /// ItemsSource, so no code-behind logic is needed anywhere upstream.
    /// </summary>
    public partial class TrackView : UserControl
    {
        private const int TrailLength = 300;

        private static readonly Color[] RoverColours =
        {
            Color.FromRgb(0x3F, 0xA7, 0xD6),
            Color.FromRgb(0x7A, 0xC7, 0x4F),
            Color.FromRgb(0xE8, 0xB0, 0x3A),
            Color.FromRgb(0xD6, 0x5A, 0x4A),
            Color.FromRgb(0xB1, 0x8A, 0xE0)
        };

        public static readonly DependencyProperty TrackNorthProperty =
            DependencyProperty.Register(nameof(TrackNorth), typeof(double), typeof(TrackView),
                new PropertyMetadata(0.0, OnBoundsChanged));

        public static readonly DependencyProperty TrackSouthProperty =
            DependencyProperty.Register(nameof(TrackSouth), typeof(double), typeof(TrackView),
                new PropertyMetadata(0.0, OnBoundsChanged));

        public static readonly DependencyProperty TrackWestProperty =
            DependencyProperty.Register(nameof(TrackWest), typeof(double), typeof(TrackView),
                new PropertyMetadata(0.0, OnBoundsChanged));

        public static readonly DependencyProperty TrackEastProperty =
            DependencyProperty.Register(nameof(TrackEast), typeof(double), typeof(TrackView),
                new PropertyMetadata(0.0, OnBoundsChanged));

        public static readonly DependencyProperty GeofenceProperty =
            DependencyProperty.Register(nameof(Geofence), typeof(TrackPoint[]), typeof(TrackView),
                new PropertyMetadata(null, OnGeofenceChanged));

        public static readonly DependencyProperty RoversProperty =
            DependencyProperty.Register(nameof(Rovers), typeof(ObservableCollection<Rover>), typeof(TrackView),
                new PropertyMetadata(null, OnRoversChanged));

        private readonly Dictionary<byte, Ellipse> _markers = new Dictionary<byte, Ellipse>();
        private readonly Dictionary<byte, Line> _headings = new Dictionary<byte, Line>();
        private readonly Dictionary<byte, TextBlock> _labels = new Dictionary<byte, TextBlock>();
        private readonly Dictionary<byte, Polyline> _trails = new Dictionary<byte, Polyline>();

        /// <summary>
        /// Every rover currently subscribed to, keyed by id so a Reset (which
        /// carries no OldItems to unsubscribe from) can still find them.
        /// </summary>
        private readonly Dictionary<byte, Rover> _subscribedRovers = new Dictionary<byte, Rover>();

        private TrackProjection? _projection;

        public TrackView()
        {
            InitializeComponent();
        }

        public double TrackNorth
        {
            get => (double)GetValue(TrackNorthProperty);
            set => SetValue(TrackNorthProperty, value);
        }

        public double TrackSouth
        {
            get => (double)GetValue(TrackSouthProperty);
            set => SetValue(TrackSouthProperty, value);
        }

        public double TrackWest
        {
            get => (double)GetValue(TrackWestProperty);
            set => SetValue(TrackWestProperty, value);
        }

        public double TrackEast
        {
            get => (double)GetValue(TrackEastProperty);
            set => SetValue(TrackEastProperty, value);
        }

        public TrackPoint[]? Geofence
        {
            get => (TrackPoint[]?)GetValue(GeofenceProperty);
            set => SetValue(GeofenceProperty, value);
        }

        public ObservableCollection<Rover>? Rovers
        {
            get => (ObservableCollection<Rover>?)GetValue(RoversProperty);
            set => SetValue(RoversProperty, value);
        }

        private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TrackView)d).RebuildProjection();
        }

        private static void OnGeofenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TrackView)d).RedrawGeofence();
        }

        private static void OnRoversChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TrackView view = (TrackView)d;

            if (e.OldValue is ObservableCollection<Rover> oldRovers)
            {
                oldRovers.CollectionChanged -= view.Rovers_CollectionChanged;
                foreach (Rover rover in oldRovers) view.Unsubscribe(rover);
            }

            if (e.NewValue is ObservableCollection<Rover> newRovers)
            {
                newRovers.CollectionChanged += view.Rovers_CollectionChanged;
                foreach (Rover rover in newRovers) view.Subscribe(rover);
            }
        }

        private void Rovers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Reset (e.g. Rovers.Clear()) carries no OldItems - everything
            // currently tracked has to be found via _subscribedRovers
            // instead. ToList() snapshots it first since Unsubscribe removes
            // from the same dictionary as it goes.
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (Rover rover in _subscribedRovers.Values.ToList()) Unsubscribe(rover);
                return;
            }

            if (e.OldItems != null)
            {
                foreach (Rover rover in e.OldItems) Unsubscribe(rover);
            }

            if (e.NewItems != null)
            {
                foreach (Rover rover in e.NewItems) Subscribe(rover);
            }
        }

        private void Subscribe(Rover rover)
        {
            rover.PropertyChanged += Rover_PropertyChanged;
            _subscribedRovers[rover.Id] = rover;
            Redraw(rover);
        }

        /// <summary>
        /// Undoes Subscribe: stops listening to this rover and removes every
        /// visual it owns, so a removed or reset rover doesn't leave a stale
        /// marker/trail/label on screen or keep this control alive in its
        /// PropertyChanged invocation list.
        /// </summary>
        private void Unsubscribe(Rover rover)
        {
            rover.PropertyChanged -= Rover_PropertyChanged;
            _subscribedRovers.Remove(rover.Id);
            RemoveVisuals(rover.Id);
        }

        private void RemoveVisuals(byte roverId)
        {
            Ellipse? marker;
            if (_markers.TryGetValue(roverId, out marker))
            {
                RoverLayer.Children.Remove(marker);
                _markers.Remove(roverId);
            }

            Line? heading;
            if (_headings.TryGetValue(roverId, out heading))
            {
                RoverLayer.Children.Remove(heading);
                _headings.Remove(roverId);
            }

            TextBlock? label;
            if (_labels.TryGetValue(roverId, out label))
            {
                RoverLayer.Children.Remove(label);
                _labels.Remove(roverId);
            }

            Polyline? trail;
            if (_trails.TryGetValue(roverId, out trail))
            {
                TrailLayer.Children.Remove(trail);
                _trails.Remove(roverId);
            }
        }

        private void Rover_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Rover.Position) || e.PropertyName == nameof(Rover.Heading))
            {
                Redraw((Rover)sender!);
            }
        }

        private void RebuildProjection()
        {
            _projection = new TrackProjection(TrackNorth, TrackSouth, TrackWest, TrackEast,
                                              MapMetrics.CanvasWidth, MapMetrics.CanvasHeight);
            RedrawGeofence();

            if (Rovers != null)
            {
                foreach (Rover rover in Rovers) Redraw(rover);
            }
        }

        private void RedrawGeofence()
        {
            if (_projection == null || Geofence == null) return;

            PointCollection points = new PointCollection();

            foreach (TrackPoint corner in Geofence)
            {
                double x, y;
                _projection.TryProject(corner, out x, out y);
                points.Add(new Point(x, y));
            }

            GeofencePolygon.Points = points;
        }

        private void Redraw(Rover rover)
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
