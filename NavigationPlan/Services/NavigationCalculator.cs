using NavigationPlan.Models;

namespace NavigationPlan.Services;

public static class NavigationCalculator
{
    private static double Rad(double deg) => deg * Math.PI / 180.0;
    private static double Deg(double rad) => rad * 180.0 / Math.PI;

    public static double Normalize360(double deg)
    {
        deg %= 360;
        return deg < 0 ? deg + 360 : deg;
    }

    public static double TrueTrack(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = Rad(lat1), φ2 = Rad(lat2);
        double Δλ = Rad(lon2 - lon1);

        double x = Math.Sin(Δλ) * Math.Cos(φ2);
        double y = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);

        return Normalize360(Deg(Math.Atan2(x, y)));
    }

    public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = Rad(lat1), φ2 = Rad(lat2);
        double Δφ = Rad(lat2 - lat1);
        double Δλ = Rad(lon2 - lon1);

        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2)
                 + Math.Cos(φ1) * Math.Cos(φ2) * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return 6371.0 * c / 1.852;
    }

    // windDir = direction FROM which wind blows (meteorological convention)
    public static (double WCA, double GS) WindCorrection(
        double tt, double tas, double windDir, double windSpeed)
    {
        double windTrack = Normalize360(windDir + 180.0);
        double angleRad = Rad(Normalize360(windTrack - tt));

        double sinArg = Math.Clamp((windSpeed / tas) * Math.Sin(angleRad), -1.0, 1.0);
        double wcaRad = Math.Asin(sinArg);
        double wca = Deg(wcaRad);

        double gs = tas * Math.Cos(wcaRad) + windSpeed * Math.Cos(angleRad);
        if (gs < 1.0) gs = 1.0;

        return (wca, gs);
    }

    public static List<NavLeg> Calculate(List<Waypoint> waypoints, FlightPlan plan)
    {
        var legs = new List<NavLeg>();
        if (waypoints.Count == 0) return legs;

        double cumulativeMinutes = 0;

        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            var leg = new NavLeg
            {
                WaypointName = wp.Name,
                Level = plan.Level,
                Tas = plan.TAS,
                Ata = string.Empty,
                Remarks = string.Empty,
            };

            if (i == 0)
            {
                // Departure row — no leg values, just the departure ETA
                leg.DistanceNm = 0;
                leg.TimeMin = 0;
                leg.TrueTrack = 0;
                leg.TrueHeading = 0;
                leg.MagneticHeading = 0;
                leg.GroundSpeed = plan.TAS;
                leg.Wca = string.Empty;
                leg.Eta = plan.DepartureTime.ToString("HH:mm");
            }
            else
            {
                var prev = waypoints[i - 1];
                double tt = TrueTrack(prev.Latitude, prev.Longitude, wp.Latitude, wp.Longitude);
                double dist = DistanceNm(prev.Latitude, prev.Longitude, wp.Latitude, wp.Longitude);
                var (wca, gs) = WindCorrection(tt, plan.TAS, plan.WindDirection, plan.WindSpeed);

                double th = Normalize360(tt + wca);
                double mh = Normalize360(th - plan.MagneticVariation);
                int timeMin = (int)Math.Round((dist / gs) * 60.0);

                cumulativeMinutes += timeMin;
                var eta = plan.DepartureTime.AddMinutes(cumulativeMinutes);

                int wcaDeg = (int)Math.Round(wca);
                leg.TrueTrack = (int)Math.Round(tt);
                leg.TrueHeading = (int)Math.Round(th);
                leg.MagneticHeading = (int)Math.Round(mh);
                leg.GroundSpeed = (int)Math.Round(gs);
                leg.DistanceNm = Math.Round(dist, 1);
                leg.TimeMin = timeMin;
                leg.Wca = wcaDeg >= 0 ? $"+{wcaDeg}" : $"{wcaDeg}";
                leg.Eta = eta.ToString("HH:mm");

                if (gs <= 1.0)
                    leg.Remarks = "WARN: GS too low!";
            }

            legs.Add(leg);
        }

        return legs;
    }
}
