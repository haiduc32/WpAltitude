using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DEM.Net.Core;
using Microsoft.Extensions.Hosting;

namespace WpAltitude
{
    public class ConsoleApp
    {
        class Waypoint
        {
            public int Number {get; set;}

            public string Action {get; set;}

            public double Lon {get; set;}

            public double Lat {get; set;}

            public int Alt {get; set;}
        }

        private IElevationService _elevationService;
        private IHostApplicationLifetime _applicationLifetime;

        public ConsoleApp(IElevationService elevationService, IHostApplicationLifetime applicationLifetime)
        {
            _elevationService = elevationService;
            _applicationLifetime = applicationLifetime; 
        }

        public void Process(FileInfo inFile, FileInfo outFile, int minAltitude)
        {
            bool hasChanges = true;
            XmlDocument mission = new XmlDocument();
            mission.PreserveWhitespace = true;
            mission.Load(inFile.FullName);
            var waypointNodes = mission.SelectNodes("//missionitem");

            List<Waypoint> waypoints = new List<Waypoint>();
            foreach (XmlNode wpNode in waypointNodes)
            {
                Waypoint wp = new Waypoint
                {
                    Number = int.Parse(wpNode.Attributes["no"].Value),
                    Action = wpNode.Attributes["action"].Value,
                    Lon = double.Parse(wpNode.Attributes["lon"].Value),
                    Lat = double.Parse(wpNode.Attributes["lat"].Value),
                    Alt = int.Parse(wpNode.Attributes["alt"].Value)
                };
                waypoints.Add(wp);
            }

            waypoints = waypoints.OrderBy(x => x.Number).ToList();

            //if the last point is RTH then set it as the first coord so we can calculate altitude
            if (waypoints.Last().Action == "RTH")
            {
                Waypoint wp = waypoints.Last();
                wp.Lat = waypoints.First().Lat;
                wp.Lon = waypoints.First().Lon;
            }

            //ground elevation of the first WP
            int? firstWpEl = null;

            //iterate through all WPs and calcualate diff of altitude between each pair
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var first = waypoints[i];
                var second = waypoints[i+1];

                //must ensure that we set the altitude at least 50m oer any heights till second point

                var elevationLine = GeometryService.ParseGeoPointAsGeometryLine(
                    new GeoPoint(first.Lat, first.Lon), new GeoPoint(second.Lat, second.Lon));

                // Download DEM tiles if necessary
                _elevationService.DownloadMissingFiles(DEMDataSet.AW3D30, elevationLine.GetBoundingBox());

                var firstPoint = _elevationService.GetPointElevation(first.Lat, first.Lon, DEMDataSet.AW3D30);
                var secondPoint = _elevationService.GetPointElevation(second.Lat, second.Lon, DEMDataSet.AW3D30);
                List<GeoPoint> straightLine = new List<GeoPoint> { firstPoint, secondPoint };
                var m1 = straightLine.ComputeMetrics();

                //we set absolute altitude on WP and then will adjust to relative to first WP
                if (!firstWpEl.HasValue) 
                {
                    firstWpEl = (int)firstPoint.Elevation;
                    first.Alt = firstWpEl.Value + first.Alt;
                }
                
                var geoPoints = _elevationService.GetLineGeometryElevation(elevationLine, DEMDataSet.AW3D30);
                

                //filter them by elevation (413 - is the Dead Sea):
                geoPoints = geoPoints.Where(x => x.Elevation > -414).ToList();

                // Compute metrics (to get distance from origin)
                var metrics = geoPoints.ComputeMetrics();

                // Simplify line with 50m resolution
                var simplified = DouglasPeucker.DouglasPeuckerReduction(geoPoints.ToList(), 50 /* meters */);

                bool repeat;
                int max = 0;
                do
                {
                    repeat = false;
                    //int max = Math.Max((int)metrics.MaxElevation + minAltitude, (int)secondPoint.Elevation + second.Alt);
                    max = (int)secondPoint.Elevation + second.Alt;

                    double proportionalAltitude = (max - first.Alt) / metrics.Distance;
                    //check that no peaks are too high on the way 
                    foreach (var point in simplified)
                    {
                        //expected plane altitude at point
                        double expectedAlt = first.Alt + point.DistanceFromOriginMeters.Value * proportionalAltitude;
                        if (expectedAlt < point.Elevation.Value + minAltitude)
                        {
                            double addAltAtPoint = point.Elevation.Value + minAltitude - expectedAlt;
                            //now that we know the minimum alt difference that we need to add
                            double wpAddAlt = addAltAtPoint / point.DistanceFromOriginMeters.Value * metrics.Distance;
                            second.Alt += (int)Math.Ceiling(wpAddAlt);

                            repeat = true;
                            break;
                        }
                    }
                    
                    
                } while (repeat);
                second.Alt = max;
            }

            //now we need to recalculate the altitude relative to the first waypoint elevation
            foreach (var wp in waypoints)
            {
                wp.Alt -= firstWpEl.Value;
            }

            foreach (XmlNode wpNode in waypointNodes)
            {
                wpNode.Attributes["alt"].Value = 
                    waypoints.Single(x => x.Number.ToString() == wpNode.Attributes["no"].Value).Alt.ToString();
            }
            
            if (hasChanges)
            {
                using (var stringWriter = new StringWriter())
                using (var xmlTextWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings 
                    { Indent = true, OmitXmlDeclaration = false, NewLineHandling = NewLineHandling.None}))
                {
                    mission.WriteTo(xmlTextWriter);
                    xmlTextWriter.Flush();
                    
                    var sw = new StreamWriter(File.OpenWrite(outFile.FullName));
                    sw.Write(stringWriter.GetStringBuilder().ToString());
                    sw.Flush();
                    sw.Close();
                }
            }
        }
    }
}
