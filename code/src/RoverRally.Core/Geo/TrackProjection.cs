using RoverRally.Core.Models;

namespace RoverRally.Core.Geo
{
    /// <summary>
    /// Maps a position on the proving ground onto the track canvas. The track
    /// is small enough (roughly 700m across) that a flat linear mapping is
    /// indistinguishable from a proper projection at this scale.
    /// </summary>
    public class TrackProjection
    {
        private readonly double _north;
        private readonly double _south;
        private readonly double _west;
        private readonly double _east;
        private readonly double _width;
        private readonly double _height;

        public TrackProjection(double north, double south, double west, double east, double widthPixels, double heightPixels)
        {
            _north = north;
            _south = south;
            _west = west;
            _east = east;
            _width = widthPixels;
            _height = heightPixels;
        }

        /// <summary>
        /// Projects a position to canvas coordinates. Returns false when the
        /// position falls outside the mapped area, in which case the caller
        /// should not draw it.
        /// </summary>
        public bool TryProject(TrackPoint point, out double x, out double y)
        {
            x = (point.Longitude - _west) / (_east - _west) * _width;
            y = (_north - point.Latitude) / (_north - _south) * _height;

            return x >= 0 && x <= _width && y >= 0 && y <= _height;
        }

        /// <summary>
        /// The inverse of <see cref="TryProject"/>: turns a canvas pixel back
        /// into the position that would project there. Callers only ever
        /// unproject pixels they chose themselves (a fixed point drawn on the
        /// canvas), so unlike <see cref="TryProject"/> there is no
        /// out-of-range case to report.
        /// </summary>
        public TrackPoint Unproject(double x, double y)
        {
            double longitude = _west + x / _width * (_east - _west);
            double latitude = _north - y / _height * (_north - _south);

            return new TrackPoint(latitude, longitude);
        }
    }
}
