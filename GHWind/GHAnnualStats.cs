using System;
using System.Collections.Generic;
using System.ComponentModel;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHAnnualStats : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHAnnualStatsNew class.
        /// </summary>
        public GHAnnualStats()
         : base("OLD Annual Statistic", "AnnStat",
             "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded",
             "GH_Wind", "test")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            //0
            pManager.AddNumberParameter("Annual Wind Speeds", "V,wind", "annual wind speeds, list length = 8760", GH_ParamAccess.list);

            //1
            pManager.AddNumberParameter("Annual Wind Directions", "Dir,wind", "annual wind directions, list length = 8760", GH_ParamAccess.list);

            //2
            pManager.AddNumberParameter("SpeedupFactors", "Vrel", "speedup factors per point. Grafted, so that each tree is one direction", GH_ParamAccess.tree);

            //3
            pManager.AddNumberParameter("Threshold", "Vmax", "threshold (m/s)  default is 5", GH_ParamAccess.item, 5.0);
            pManager[3].Optional = true;

            //4
            pManager.AddBooleanParameter("debug", "debug", "debug", GH_ParamAccess.item, false);
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Comfort per Point", "Comfort per Point", "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded", GH_ParamAccess.list);
            pManager.AddNumberParameter("Speed per Direction", "SPD", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("hours above threshold, per direction", "hours above threshold, per direction", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("vels per point", "VPP", "x", GH_ParamAccess.tree);
        }


        


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {



            List<double> windVelocities = new List<double>();
            DA.GetDataList(0, windVelocities);

            List<double> windDirections = new List<double>();
            DA.GetDataList(1, windDirections);

            GH_Structure<GH_Number> speedups = new GH_Structure<GH_Number>();
            DA.GetDataTree(2, out speedups);

            double thresholdVelocity = 5.0;
            DA.GetData(3, ref thresholdVelocity);

            bool debug = false;
            DA.GetData(4, ref debug);

            int noHours = windVelocities.Count;
            int noPoints = speedups.get_Branch(0).Count;
            int noWindDirections = speedups.Branches.Count;
            //int x = speedups.Branches.Count;

            Rhino.RhinoApp.WriteLine($"noWindDirs: {noWindDirections}");

            List<List<double>> windVelocitiesPerDirection = new List<List<double>>(noWindDirections);
            List<List<double>> velocitiesPerPoint = new List<List<double>>(noWindDirections);

            for (int i = 0; i < noWindDirections; i++)
            {
                windVelocitiesPerDirection.Add(new List<double>());
                velocitiesPerPoint.Add(new List<double>());
            }

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

            int[,] hoursAboveThresholdPerPointPerDirection = new int[noPoints,noWindDirections];

            GH_Structure<GH_Number> outSpeedsPerPoint = new GH_Structure<GH_Number>();
            GH_Structure<GH_Number> outHoursAboveThresholdPerPointPerDirection = new GH_Structure<GH_Number>();


            List<int> outHoursAbovePerPoint = new List<int>();


            for (int p = 0; p < noPoints; p++)
            {

                if (p % 100 == 0)
                    Rhino.RhinoApp.WriteLine($"AnnStat checking point {p:000} of {noPoints}");

                int hoursThisPoint = 0;

                for (int h = 0; h < noHours; h++)
                {
                    
                    

                    int thisWindDir = -1;

                    // FINDING THE DIRECTION FOR THIS HOUR
                    for (int w = 0; w < noWindDirections; w++) // for each direction
                    {
                        
                        double thisAngle = 360.0 / noWindDirections * w;

                        bool extra = false;
                        if (thisAngle == 0 && (Math.Abs(windDirections[h] - 360.0) < angleTol))
                            extra = true;

                        if ((Math.Abs(thisAngle - windDirections[h]) < angleTol || (Math.Abs(windDirections[h] - thisAngle) <= angleTol)) || extra)
                        {
                            thisWindDir = w;

                            break;
                        }
                        

                    }
                    if (thisWindDir == -1)
                        throw new Exception("No direction");

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
                        Rhino.RhinoApp.WriteLine($"[p {p:0}][h {h:0}/{noHours}][wdir {thisWindDir:0}] {100.0*pointAccelerationThisDir:0.0}% · {windVelocities[h]}m/s = {pointVelocityThisHour:0.0}m/s  (exceeded so far: {hoursThisPoint})");


                }

                outHoursAbovePerPoint.Add(hoursThisPoint);
                //outHoursAbovePerPoint[p] = hoursThisPoint;

                if (p < 5 && debug)
                    Rhino.RhinoApp.WriteLine($"[p {p:0}] hours above: {outHoursAbovePerPoint[outHoursAbovePerPoint.Count-1]} .. should be {hoursThisPoint}");


            }

            for (int p = 0; p < hoursAboveThresholdPerPointPerDirection.GetLength(0); p++)
            {
                List<GH_Number> hoursPerDirection = new List<GH_Number>();
                //GH_Structure < GH_Number > hoursPerDirection = new GH_Structure<GH_Number>();

                for (int w = 0; w < hoursAboveThresholdPerPointPerDirection.GetLength(1); w++)
                {
                    hoursPerDirection.Add(new GH_Number(hoursAboveThresholdPerPointPerDirection[p, w]));
                }
                    outHoursAboveThresholdPerPointPerDirection.AppendRange(hoursPerDirection, new GH_Path(p));
            }


            DA.SetDataList(0, outHoursAbovePerPoint);
            DA.SetDataTree(1, outSpeedsPerDirection);
            DA.SetDataTree(2, outHoursAboveThresholdPerPointPerDirection);
            DA.SetDataTree(3, outSpeedsPerPoint);






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
                return GHWind.Properties.Resources.aytac;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("420ade09-1d78-48dd-916c-4c111f24ce5e"); }
        }
    }
}