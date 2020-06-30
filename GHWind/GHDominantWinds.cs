using System;
using System.Collections.Generic;
using System.ComponentModel;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHDominantWinds : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHAnnualStatsNew class.
        /// </summary>
        public GHDominantWinds()
         : base("Dominant Winds", "DomWinds",
             "Find predominant wind directions",
             "GreenScenario", "Thermal")
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
            pManager.AddNumberParameter("No wind dirs", "No wind dirs", "int,  no of wind directions. Typically 8, 12 or 16.", GH_ParamAccess.item);


            //3
            pManager.AddBooleanParameter("debug", "debug", "debug", GH_ParamAccess.item, false);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            
            pManager.AddNumberParameter("Speed per Direction", "SPD", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Directions", "Dirs", "x", GH_ParamAccess.list);

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

            //GH_Structure<GH_Number> speedups = new GH_Structure<GH_Number>();
            //DA.GetDataTree(2, out speedups);

            
            double windDirs = 0.0;
            DA.GetData(2, ref windDirs);

            int noWindDirections = (int)Math.Round(windDirs);

            //double thresholdVelocity = 5.0;
            //DA.GetData(3, ref thresholdVelocity);

            bool debug = false;
            DA.GetData(3, ref debug);

            int noHours = windVelocities.Count;

            Rhino.RhinoApp.WriteLine($"noWindDirs: {noWindDirections}");

            List<double> outDirections = new List<double>();

            List<List<double>> windVelocitiesPerDirection = new List<List<double>>(noWindDirections);

            double angleTol = 360.0 / noWindDirections / 2.0; // at 16 dirs, each angle is 22.5   this means +/-11.25° to each side.

            for (int i = 0; i < noWindDirections; i++)
            {
                windVelocitiesPerDirection.Add(new List<double>());
                outDirections.Add(angleTol * 2 * i);

            }

          


            for (int h = 0; h < noHours; h++)
            {

                int thisWindDir = -1;

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

                windVelocitiesPerDirection[thisWindDir].Add(windVelocities[h]);


            }

            GH_Structure<GH_Number> outSpeedsPerDirection = new GH_Structure<GH_Number>();


            for (int i = 0; i < noWindDirections; i++)
            {

                List<double> velocitiesInDirection = new List<double>(windVelocitiesPerDirection[i]);

                velocitiesInDirection.Sort();
                velocitiesInDirection.Reverse();


                List<GH_Number> numbers = new List<GH_Number>();

                for (int j = 0; j < windVelocitiesPerDirection[i].Count; j++)
                {
                    numbers.Add(new GH_Number(velocitiesInDirection[j]));
                }


                outSpeedsPerDirection.AppendRange(numbers, new GH_Path(i));
            }


            DA.SetDataTree(0, outSpeedsPerDirection);
            DA.SetDataList(1, outDirections);

            return;
    /*

            /*

            List<int> outHoursAbovePerPoint = new List<int>();


            for (int p = 0; p < noPoints; p++)
            {

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
                        Rhino.RhinoApp.WriteLine($"[p {p:0}][h {h:0}/{noHours}][wdir {thisWindDir:0}] {100.0 * pointAccelerationThisDir:0.0}% · {windVelocities[h]}m/s = {pointVelocityThisHour:0.0}m/s  (exceeded so far: {hoursThisPoint})");


                }

                outHoursAbovePerPoint.Add(hoursThisPoint);
                //outHoursAbovePerPoint[p] = hoursThisPoint;

                if (p < 5 && debug)
                    Rhino.RhinoApp.WriteLine($"[p {p:0}] hours above: {outHoursAbovePerPoint[outHoursAbovePerPoint.Count - 1]} .. should be {hoursThisPoint}");


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


            //DA.SetDataList(0, outHoursAbovePerPoint);
            //DA.SetDataTree(1, outSpeedsPerDirection);
            //DA.SetDataTree(2, outHoursAboveThresholdPerPointPerDirection);
            //DA.SetDataTree(3, outSpeedsPerPoint);

            */




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
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3e07eede-5406-434b-be5a-effa799600a4"); }
        }
    }
}