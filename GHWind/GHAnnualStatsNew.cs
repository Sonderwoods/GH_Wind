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
         : base("Annual Statistic", "Ann Wind Stats",
             "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded",
             "GH_Wind", "07 | Preview")
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
            pManager.AddNumberParameter("Comfort per Point", "Comfort per Point", "annual comfort per point. % of the year where 5m/s is exceeded", GH_ParamAccess.list);
            pManager.AddNumberParameter("xx Speed per Direction", "SPD", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("average windspeeds in the point", "Vavg", "Average wind speeds", GH_ParamAccess.list);
            //pManager.AddNumberParameter("xx hours above threshold, per direction", "hours above threshold, per direction", "x", GH_ParamAccess.tree);
            //pManager.AddNumberParameter("xx vels per point", "VPP", "x", GH_ParamAccess.tree);
        }


        double averageInputs = 0;
        double oldSumOfVelocities = 0;

        double[] accumulatedSpeedsPerPoint;

        List<double> outThresholdHoursPerPoint = new List<double>();
        GH_Structure<GH_Number> outThresholdHoursPerPointPerDirection = new GH_Structure<GH_Number>();
        //double[,] thresholdHoursPerPointPerDirection;
        List<double> thresholdsForDirectionCheck = new List<double>();


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {


            GH_Structure<GH_Number> inSPDsPerDirPerHours = new GH_Structure<GH_Number>();
            DA.GetDataTree(0, out inSPDsPerDirPerHours);

            List<double> inDirections = new List<double>();
            DA.GetDataList(1, inDirections);

            GH_Structure<GH_Number> inVrelSimVelocitiesPerDirPerPoint = new GH_Structure<GH_Number>();
            DA.GetDataTree(2, out inVrelSimVelocitiesPerDirPerPoint);


            double VmaxThreshold = 5.0;
            DA.GetData(3, ref VmaxThreshold);

            bool debug = false;
            DA.GetData(4, ref debug);

            bool run = false;
            DA.GetData(5, ref run);


            if (!run)
            {
                DA.SetDataList(0, outThresholdHoursPerPoint);
                //DA.SetDataTree(1, outThresholdHoursPerPointPerDirection);
                DA.SetDataList(2, accumulatedSpeedsPerPoint);
                return;
            }

            int noPoints = inVrelSimVelocitiesPerDirPerPoint.get_Branch(0).Count;
            int noWindDirections = inVrelSimVelocitiesPerDirPerPoint.Branches.Count;


            accumulatedSpeedsPerPoint = new double[noPoints];



            if (debug) Rhino.RhinoApp.WriteLine($"stats 001");



            // SPDS: 
            //inSPDsPerDirPerHours.Branches[windDir][point].Value;

            // Vrels:
            //inVrelSimVelocitiesPerDirPerPoint.Branches[windDir][point].Value;

            if (debug) Rhino.RhinoApp.WriteLine($"stats 002.. Foreach inVelocitiesPerPointPerDir.PathCount {inVrelSimVelocitiesPerDirPerPoint.PathCount}");



            // Checking if new weather data setup. Otherwise reuse the old.

            if (debug) Rhino.RhinoApp.WriteLine($"stats 003");


            double newSumOfVelocities = 0;

            foreach(List<GH_Number> numbers in inVrelSimVelocitiesPerDirPerPoint.Branches)
            {
                foreach(GH_Number number in numbers)
                {
                    newSumOfVelocities += number.Value;
                }
            }

            if (oldSumOfVelocities != newSumOfVelocities)
            {
                if (debug) Rhino.RhinoApp.WriteLine($"redoing thresholds");
                thresholdsForDirectionCheck = Utilities.GetThresholds(inDirections);
                oldSumOfVelocities = newSumOfVelocities;

            }
            else
            {
                if (debug) Rhino.RhinoApp.WriteLine($"reusing thresholds");
            }


            //if (debug) Rhino.RhinoApp.WriteLine($"stats 004");
            outThresholdHoursPerPointPerDirection = new GH_Structure<GH_Number>();
            outThresholdHoursPerPoint = new List<double>(new double[noPoints].ToList());

            List<double> thresholds = Utilities.GetThresholds(inDirections);


            if (debug) Rhino.RhinoApp.WriteLine($"foreach noPoints: {noPoints}");
            for (int p = 0; p < noPoints; p++)
            {

                List<GH_Number> speedsInThisPointPerDir = new List<GH_Number>();

                if (debug && p < 5) Rhino.RhinoApp.WriteLine($"foreach noWindDirections: {noWindDirections}");
                for (int d = 0; d < noWindDirections; d++)
                {

                    int hoursThisPointAndDirection = 0;

                    if (debug && d < 3 && p < 5) Rhino.RhinoApp.WriteLine($"foreach SPDS: inSPDsPerDirPerHours[{d}].Count: {inSPDsPerDirPerHours[d].Count}");
                    for (int s = 0; s < inSPDsPerDirPerHours[d].Count; s++)
                    {

                        
                        double result = inSPDsPerDirPerHours.Branches[d][s].Value * inVrelSimVelocitiesPerDirPerPoint.Branches[d][p].Value;

                        accumulatedSpeedsPerPoint[p] += result;

                        if (debug && s < 5 && p < 5 && d < 5) Rhino.RhinoApp.WriteLine($"[{p}][{d}][{s}]  inSPDsPerDirPerHours.Branches[{d}][{s}].Value {inSPDsPerDirPerHours.Branches[d][s].Value:0.0} *  inVrelSimVelocitiesPerDirPerPoint.Branches[{d}][{p}].Value { inVrelSimVelocitiesPerDirPerPoint.Branches[d][p].Value:0.0} = {result:0.0}");
                        if (result >= VmaxThreshold)
                        {
                            hoursThisPointAndDirection++;
                            if (debug && s < 5 && p < 5 && d < 5) Rhino.RhinoApp.WriteLine($"[{p}][{d}][{s}] adding");
                        }

                    }


                    speedsInThisPointPerDir.Add(new GH_Number(hoursThisPointAndDirection));


                }

                accumulatedSpeedsPerPoint[p] /= 8760.0; //should give average speed in this point over a year.

                outThresholdHoursPerPointPerDirection.AppendRange(speedsInThisPointPerDir, new GH_Path(p));


            }



            //now we got it per direction. Lets sum it up.

            List<double> hoursOutsideComfortPerPoint = new double[noPoints].ToList();

            for (int i = 0; i < outThresholdHoursPerPointPerDirection.Branches.Count; i++)
            {
                for (int j = 0; j < outThresholdHoursPerPointPerDirection.Branches[i].Count; j++)
                {

                    outThresholdHoursPerPoint[i] += outThresholdHoursPerPointPerDirection.Branches[i][j].Value / 8760.0 * 100.0; // to convert to pct per year
                }
            }
            //DA.SetDataList(0, hoursOutsideComfortPerPoint);

            //DA.SetData(1, outThresholdHoursPerPoint);

            //DA.SetDataList(2, accumulatedSpeedsPerPoint);



            




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
                return GHWind.Properties.Resources.discr;
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