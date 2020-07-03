using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHAnnualStatsNew : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHAnnualStatsNew class.
        /// </summary>
        public GHAnnualStatsNew()
         : base("Annual Statistic new", "AnnStat new",
             "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded",
             "GreenScenario", "07 | Preview")
        {
        }
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            //0
            pManager.AddNumberParameter("Speeds Per Direction", "SPDs", "speeds per direction. Tree. Number of branches must match the simulated ones.", GH_ParamAccess.tree);

            //1
            pManager.AddNumberParameter("Dirs", "Dirs", "Dirs - connect from DominantWinds component.", GH_ParamAccess.list);

            //2
            pManager.AddNumberParameter("SpeedupFactors", "Vrel", "speedup factors per point. Grafted, so that each tree is one direction", GH_ParamAccess.tree);

            //3
            pManager.AddNumberParameter("Threshold", "Vmax", "threshold (m/s)  default is 5", GH_ParamAccess.item, 5.0);
            pManager[3].Optional = true;

            //4
            pManager.AddBooleanParameter("debug", "debug", "debug", GH_ParamAccess.item, false);
            pManager[4].Optional = true;

            //5
            pManager.AddBooleanParameter("Run?", "Run?", "Run", GH_ParamAccess.item, false);
            pManager[5].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Comfort per Point", "Comfort per Point", "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded", GH_ParamAccess.list);
            pManager.AddNumberParameter("xx Speed per Direction", "SPD", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("xx hours above threshold, per direction", "hours above threshold, per direction", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("xx vels per point", "VPP", "x", GH_ParamAccess.tree);
        }


        double averageInputs = 0;
        double averageWindVelocities = 0;
        
        List<double> outThresholdHoursPerPoint = new List<double>();
        GH_Structure<GH_Number> outThresholdHoursPerPointPerDirection = new GH_Structure<GH_Number>();
        double[,] thresholdHoursPerPointPerDirection;
        List<double> thresholdsForDirectionCheck = new List<double>();


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {



            //List<List<double>> windVelocities = new List<List<double>>();


            GH_Structure<GH_Number> inWindVelocities = new GH_Structure<GH_Number>();
            DA.GetDataTree(0, out inWindVelocities);

            List<double> windDirections = new List<double>();
            DA.GetDataList(1, windDirections);

            GH_Structure<GH_Number> inVelocitiesPerPointPerDir = new GH_Structure<GH_Number>();
            DA.GetDataTree(2, out inVelocitiesPerPointPerDir);

            double thresholdVelocity = 5.0;
            DA.GetData(3, ref thresholdVelocity);

            bool debug = false;
            DA.GetData(4, ref debug);

            bool run = false;
            DA.GetData(5, ref run);


            if (!run)
            {
                DA.SetDataList(0, outThresholdHoursPerPoint);
                DA.SetDataTree(1, outThresholdHoursPerPointPerDirection);
                return;
            }


            //int noHours = inWindVelocities.PathCount;
            int noPoints = inVelocitiesPerPointPerDir.get_Branch(0).Count;
            int noWindDirections = inVelocitiesPerPointPerDir.Branches.Count;

            double[,] speedupsPerPointPerDir = new double[noPoints, noWindDirections];

            List<List<double>> windVelocitiesPerDirPerPoint = new List<List<double>>();

            if (debug) Rhino.RhinoApp.WriteLine($"stats 001");



            if (debug) Rhino.RhinoApp.WriteLine($"noWindDirections: {noWindDirections},  inWindVelocities.PathCount: {inWindVelocities.PathCount}");
            if (debug) Rhino.RhinoApp.WriteLine($"noPoints: {noPoints},  inWindVelocities.Branches[0].Count: {inWindVelocities.Branches[0].Count}");


            if (debug) Rhino.RhinoApp.WriteLine($"foreach  inWindVelocities.PathCount {inWindVelocities.PathCount}:");
            for (int i = 0; i < inWindVelocities.PathCount; i++)
            {

                if (debug && i<10) Rhino.RhinoApp.WriteLine($"foreach  noPoints {noPoints}:");
                for (int j = 0; j < noPoints; j++)
                {
                    if (j == noPoints) throw new Exception("j too high");
                    if (i == noWindDirections) throw new Exception("j too high");

                    //if (debug && i < 10) Rhino.RhinoApp.WriteLine($"t");
                    speedupsPerPointPerDir[j, i] = 0;
                    //if (debug && i < 10) Rhino.RhinoApp.WriteLine($"y");
                    //if (debug && i < 10) Rhino.RhinoApp.WriteLine($"inVelocitiesPerPointPerDir.Branches.Count  {inVelocitiesPerPointPerDir.Branches.Count}");
                   // if (debug && i < 10) Rhino.RhinoApp.WriteLine($"inVelocitiesPerPointPerDir.Branches[i].Count  {inVelocitiesPerPointPerDir.Branches[i].Count}");
                    speedupsPerPointPerDir[j, i] = inVelocitiesPerPointPerDir.Branches[i][j].Value;

                }
            }

            if (debug) Rhino.RhinoApp.WriteLine($"stats 002.. Foreach inVelocitiesPerPointPerDir.PathCount {inVelocitiesPerPointPerDir.PathCount}");

            for (int i = 0; i < inVelocitiesPerPointPerDir.PathCount; i++)
            {
                List<double> l = new List<double>();
                for (int j = 0; j < inVelocitiesPerPointPerDir.Branches[i].Count; j++)
                    l.Add(inVelocitiesPerPointPerDir.Branches[i][j].Value);
                windVelocitiesPerDirPerPoint.Add(l);
            }

            if (debug) Rhino.RhinoApp.WriteLine($"stats 003");

            thresholdHoursPerPointPerDirection = new double[noPoints, noWindDirections];

            if (averageWindVelocities != windVelocitiesPerDirPerPoint[0].Average())
            {
                if (debug) Rhino.RhinoApp.WriteLine($"redoing thresholds");
                thresholdsForDirectionCheck = Utilities.GetThresholds(windDirections);
                averageWindVelocities = windVelocitiesPerDirPerPoint[0].Average();

            }
            else
            {
                if (debug) Rhino.RhinoApp.WriteLine($"reusing thresholds");
            }


            if (debug) Rhino.RhinoApp.WriteLine($"[AnnStat] noWindDirs: {noWindDirections}");

            if (debug) Rhino.RhinoApp.WriteLine($"stats 004");
            outThresholdHoursPerPointPerDirection = new GH_Structure<GH_Number>();
            outThresholdHoursPerPoint = new List<double>();

            for (int p = 0; p < noPoints; p++)
            {
                if (debug) Rhino.RhinoApp.WriteLine($"p = {p}");
                if (debug && p < 5) Rhino.RhinoApp.WriteLine($"foreach noWindDirections: {noWindDirections}");
                for (int d = 0; d < noWindDirections; d++)
                {
                    if (debug && d < 3) Rhino.RhinoApp.WriteLine($"foreach windVelocitiesPerPoint[d].Count: {windVelocitiesPerDirPerPoint[d].Count}");
                    for (int v = 0; v < windVelocitiesPerDirPerPoint[d].Count; v++)
                    {

                        if (debug && v < 10) Rhino.RhinoApp.WriteLine($"[{p}][{d}][{v}]  windVelocitiesPerDirPerPoint[{d}][{v}] {windVelocitiesPerDirPerPoint[d][v]} * speedupsPerPointPerDir[{p}, {d}] {speedupsPerPointPerDir[p, d]}");
                        //if (debug && d < 5) Rhino.RhinoApp.WriteLine($"speedupsPerPointPerDir[{p}, {d}] {speedupsPerPointPerDir[p, d]}");
                        if (windVelocitiesPerDirPerPoint[d][v] * speedupsPerPointPerDir[p, d] >= thresholdVelocity)
                        {

                            if (p >= noPoints) throw new Exception("p too high");
                            if (d >= noWindDirections) throw new Exception("d too high");
                            thresholdHoursPerPointPerDirection[p, d] += 1;
                        }
                    }

                }

                if (debug) Rhino.RhinoApp.WriteLine($"p={p} .. stats 004b");
                outThresholdHoursPerPoint.Add(thresholdHoursPerPointPerDirection.GetRow(p).Sum());

                for (int i = 0; i < thresholdHoursPerPointPerDirection.GetRow(p).Count; i++)
                {
                    if (debug) Rhino.RhinoApp.WriteLine($"{thresholdHoursPerPointPerDirection.GetRow(p)[i]}");
                }

                List<GH_Number> n = new List<GH_Number>();


                if (debug && p < 5) Rhino.RhinoApp.WriteLine($"info: thresholdHoursPerPointPerDirection.GetLength(0): {thresholdHoursPerPointPerDirection.GetLength(0)}");
                if (debug && p < 5) Rhino.RhinoApp.WriteLine($"foreach thresholdHoursPerPointPerDirection.GetLength(1): {thresholdHoursPerPointPerDirection.GetLength(1)}");
                for (int d = 0; d < thresholdHoursPerPointPerDirection.GetLength(1); d++)
                {

                    if (debug && p < 5) Rhino.RhinoApp.WriteLine($"thresholdHoursPerPointPerDirection[{p}, {d}]");
                    n.Add(new GH_Number(thresholdHoursPerPointPerDirection[p, d]));
                }
                if (debug && p < 5) Rhino.RhinoApp.WriteLine($"x");
                outThresholdHoursPerPointPerDirection.AppendRange(n, new GH_Path(p));
            }

            DA.SetData(0, outThresholdHoursPerPoint);
            DA.SetDataTree(1, outThresholdHoursPerPointPerDirection);

            
            
            

            /*



            double angleTol = 360.0 / noWindDirections / 2.0; // at 16 dirs, each angle is 22.5   this means +/-11.25° to each side.

            

            // =================================
            // PARSING ONLY ON WEATHER DATA. NO gEOMETRT/RESULTS HERE
            // =================================



            // outputting stats per wind direction (not dependant on geo)
            GH_Structure<GH_Number> outSpeedsPerDirection = new GH_Structure<GH_Number>();

            for (int i = 0; i < windVelocitiesPerDirection.Count; i++)
            {

                List<GH_Number> numbers = new List<GH_Number>();

                for (int j = 0; j < windVelocitiesPerDirection[i].Count; j++)
                {
                    numbers.Add(new GH_Number(windVelocitiesPerDirection[i][j]));
                }

                outSpeedsPerDirection.AppendRange(numbers, new GH_Path(i));
            }

            int[,] hoursAboveThresholdPerPointPerDirection = new int[noPoints, noWindDirections];

            GH_Structure<GH_Number> outSpeedsPerPoint = new GH_Structure<GH_Number>();
            GH_Structure<GH_Number> outHoursAboveThresholdPerPointPerDirection = new GH_Structure<GH_Number>();


            List<int> outHoursAbovePerPoint = new List<int>();


            for (int p = 0; p < noPoints; p++)
            {

                int hoursThisPoint = 0;

                for (int h = 0; h < noHours; h++)
                {

                    int thisWindDir = Utilities.GetClosestDirection(windDirections[h], thresholds);

                    double pointAccelerationThisDir = speedups.get_DataItem(speedups.get_Path(thisWindDir), p).Value; //speedup for point p at thisWindDir
                    double pointVelocityThisHour = pointAccelerationThisDir * windVelocities[h];

                    outSpeedsPerPoint.Append(new GH_Number(pointVelocityThisHour), new GH_Path(p, h));

                    if (pointVelocityThisHour > thresholdVelocity)
                    {
                        //outHoursAbovePerPoint[p]++;
                        hoursAboveThresholdPerPointPerDirection[p, thisWindDir]++;
                        hoursThisPoint++;

                    }

                    if (p < 1 && h < 50 && debug)
                        Rhino.RhinoApp.WriteLine($"[p {p:0}][h {h:0}/{noHours}][wdir {thisWindDir:0}] {100.0 * pointAccelerationThisDir:0.0}% · {windVelocities[h]}m/s = {pointVelocityThisHour:0.0}m/s  (exceeded so far: {hoursThisPoint})");


                }

                outHoursAbovePerPoint.Add(hoursThisPoint);
                //outHoursAbovePerPoint[p] = hoursThisPoint;

                if (p < 5 && debug)
                    Rhino.RhinoApp.WriteLine($"[p {p:0}] hours above: {outHoursAbovePerPoint[outHoursAbovePerPoint.Count - 1]} .. should be {hoursThisPoint}");


            } */




            //for (int p = 0; p < hoursAboveThresholdPerPointPerDirection.GetLength(0); p++)
            //{
            //    List<GH_Number> hoursPerDirection = new List<GH_Number>();
            //    //GH_Structure < GH_Number > hoursPerDirection = new GH_Structure<GH_Number>();

            //    for (int w = 0; w < hoursAboveThresholdPerPointPerDirection.GetLength(1); w++)
            //    {
            //        hoursPerDirection.Add(new GH_Number(hoursAboveThresholdPerPointPerDirection[p, w]));
            //    }
            //    outHoursAboveThresholdPerPointPerDirection.AppendRange(hoursPerDirection, new GH_Path(p));
            //}


            //DA.SetDataList(0, outHoursAbovePerPoint);
            //DA.SetDataTree(1, outSpeedsPerDirection);
            //DA.SetDataTree(2, outHoursAboveThresholdPerPointPerDirection);
            //DA.SetDataTree(3, outSpeedsPerPoint);






        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return GHWind.Properties.Resources.gs_wind;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("aec4cfe5-695b-4c29-8eb9-8e5141db7530"); }
        }
    }
}