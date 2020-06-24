using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
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

            //0
            pManager.AddNumberParameter("Annual Wind Speeds", "V,wind", "annual wind speeds, list length = 8760", GH_ParamAccess.list);

            //1
            pManager.AddNumberParameter("Annual Wind Directions", "Dir,wind", "annual wind directions, list length = 8760", GH_ParamAccess.list);

            //2
            pManager.AddNumberParameter("SpeedupFactors", "Vrel", "speedup factors per point. Grafted, so that each tree is one direction", GH_ParamAccess.tree);

            //3
            pManager.AddNumberParameter("Threshold", "Vmax", "threshold (m/s)  default is 5", GH_ParamAccess.item, 5.0);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Comfort", "C", "annual comfort per point. Number represent the hours of the year where 5m/s is exceeded", GH_ParamAccess.list);
            pManager.AddNumberParameter("raw per direction", "SPD", "x", GH_ParamAccess.tree);
            pManager.AddNumberParameter("hours above threshold, per direction", "x", "x", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double thresholdVelocity = 5.0;
            DA.GetData(3, ref thresholdVelocity);


            List<double> windVelocities = new List<double>();
            DA.GetDataList(0, windVelocities);
            
            List<double> windDirections = new List<double>();
            DA.GetDataList(1, windDirections);

            GH_Structure<GH_Number> speedups = new GH_Structure<GH_Number>();
            DA.GetDataTree(2, out speedups);


            int noWindDirections = speedups.Branches.Count;
            //int x = speedups.Branches.Count;

            Rhino.RhinoApp.WriteLine($"noWindDirs: {noWindDirections}");

            List <List<double>> windVelocitiesPerDirection = new List<List<double>>(noWindDirections);
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

            

            for (int h = 0; h < 8760; h++) // for each hour
            {

                for (int i = 0; i < noWindDirections; i++) // for each direction
                {
                    double thisAngle = 360.0 / noWindDirections * i;
                    double thisMax = thisAngle  + angleTol;
                    double thisMin = thisAngle - angleTol;

                    bool extra = false;
                    if (thisAngle == 0 && (Math.Abs(windDirections[h] - 360.0) < angleTol))
                        extra = true;

                    if ((Math.Abs(thisAngle - windDirections[h]) < angleTol || (Math.Abs(windDirections[h] - thisAngle) <= angleTol)) || extra)
                    {
                        windVelocitiesPerDirection[i].Add(windVelocities[h]);
                        Rhino.RhinoApp.WriteLine($"{h} - adding {windVelocities[h]}m/s ({windDirections[h]}) to direction {thisAngle}°");
                        //foundDir = true;
                        break;
                    }
                    
                }

            }


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

            DA.SetDataTree(1, outSpeedsPerDirection);




            // =================================
            // PARSIN ON GEOMETRY
            // =================================


            int noPoints = speedups.get_Branch(0).Count;

            for (int i = 0; i < noWindDirections; i++) // for each direction
            {
                double thisAngle = 360.0 / noWindDirections * i;

                for (int j = 0; j < noPoints; j++) // for each point
                {
                    velocitiesPerPoint.Add(new List<double>());

                    for (int k = 0; k < windVelocitiesPerDirection[i].Count; k++)
                    {

                        double speedup = (speedups[i][j] as GH_Number).Value;
                        velocitiesPerPoint[j].Add(speedup * windVelocitiesPerDirection[i][k]);
                        
                    }

                }

            }

            double[] timeAboveThreshold = new double[noPoints];

            DataTree<GH_Number> outResults = new DataTree<GH_Number>();

            for (int i = 0; i < noPoints; i++)
            {
                timeAboveThreshold[i] = velocitiesPerPoint[i].Where(x => x <= thresholdVelocity).Count();

            }
    
            DA.SetDataList(0, timeAboveThreshold);
 



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