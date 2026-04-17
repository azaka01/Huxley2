using System;

namespace Huxley2.Utils
{
    public static class HaversineDistance
    {
        private const double EarthRadiusMiles = 3958.7558657440545; // 6371000.0 metres / 1609.344, matching KMP SDK

        /// <summary>
        /// Calculate the distance in miles between two GPS coordinates
        /// using the Haversine formula to account for Earth's curvature.
        /// </summary>
        public static double CalculateMiles(double lat1, double lng1, double lat2, double lng2)
        {
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLng = DegreesToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusMiles * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
