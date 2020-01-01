# WpAltitude
iNav waypoint parser and fixer to help follow terrain.

iNav does not support following terrain. This console app can be used to load a mission with pre-set altitude and use those values as guidelines for altitude above terrain at the given waypoints. Another parameter is minimum ground altitude that is used to ensure that in between waypoints, no ground features are closer to your drone then the given value. If features are closer, the next waypoint altitude will be increased to ensure that your drone clears all ground features.

Usage ex:
WpAltitude -i my_mission_file.mission -o adjusted_file.mission -minAlt 50

-i -- input file

-o -- output file

-minAlt -- (meters) minimum altitude over terrain at any point between waypoints

Because there is no information for your elevation when you launch your drone, WayPoint 1 will be taken as reference elevation to calculate relative altitude for all the other WPs.

Algorithm is implemented for linear climb and dive (https://github.com/iNavFlight/inav/pull/5226). Ensure you use the latest version of iNav. If you use an older version of iNav that hasn't got the linear climb and dive, **DO NOT USE THIS APP**! 
