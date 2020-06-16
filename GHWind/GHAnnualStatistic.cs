using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHAnnualStatistic : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHAnnualStatistic class.
        /// </summary>
        public GHAnnualStatistic()
          : base("Annual Statistic", "AnnStat",
              "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded",
              "Grace Hopper", "Wind")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Annual Wind Speeds", "V,wind", "annual wind speeds, list length = 8760", GH_ParamAccess.list);
            pManager.AddNumberParameter("Annual Wind Directions", "Dir,wind", "annual wind directions, list length = 8760", GH_ParamAccess.list);
            pManager.AddNumberParameter("SpeedupFactors", "Vrel", "speedup factors per point. Grafted, so that each tree is one direction", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Comfort", "C", "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            double thresholdVelocity = 5.0;
            double[] thresholdTimes = new double[4] {
                87.6 * 2.5,
                87.6 * 5,
                87.6 * 10,
                87.6 * 15};

            //List<double[]> speedups = new List<double[]>();
            List<double> windVelocities = new List<double>();
            
            List<double> windDirections = new List<double>();

            GH_Structure<IGH_Goo> speedups = new GH_Structure<IGH_Goo>();

            DA.GetDataTree(1, out speedups);

            int noWindDirections = speedups.PathCount;

            List<List<double>> windVelocitiesPerDirection = new List<List<double>>(noWindDirections);
            List<List<double>> velocitiesPerPointPerDir = new List<List<double>>(noWindDirections);


            double angleTol = 360.0 / noWindDirections / 2.0; // at 16 dirs, each angle is 22.5   this means +/-11.25° to each side.

            for (int i = 0; i < noWindDirections; i++)
            {
                double thisAngle = 360.0 / noWindDirections * i;

                for (int j = 0; j < 8760; j++)
                {
                    bool extra = false;
                    if (thisAngle == 0 && (Math.Abs(windDirections[j] - 360.0) < angleTol))
                        extra = true;
                    if ((Math.Abs(thisAngle-windDirections[j]) < angleTol || (Math.Abs(windDirections[j] - thisAngle) <= angleTol)) || extra)
                    {
                        windVelocitiesPerDirection[i].Add(windVelocities[j]);
                    }

                }

            }



            for (int i = 0; i < speedups.PathCount; i++)
            {
                double direction = i / (double)noWindDirections;
                for (int j = 0; j < speedups[0].Count; j++)
                {
                    double speedup = 0;
                    speedups[i][j].CastTo(out speedup);
                    velocitiesPerPointPerDir[i].Add(speedup * );
                }// TODO
            }


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
            get { return new Guid("148656dd-8362-4469-8d80-87b6eed265de"); }
        }
    }
}