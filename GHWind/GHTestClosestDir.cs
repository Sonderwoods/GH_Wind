using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GHWind
{
    public class GHTestClosestDir : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHTestClosestDir class.
        /// </summary>
        public GHTestClosestDir()
          : base("GHTestClosestDir", "ClosestDir",
              "Description",
              "GreenScenario", "Test")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            pManager.AddNumberParameter("direction to check", "direction to check", "directions", GH_ParamAccess.list);
            pManager.AddNumberParameter("available directions", "available directions", "available directions", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("picked direction", "picked direction", "picked direction", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thresholds", "Thresholds", "Thresholds", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<double> directionsToCheck = new List<double>();
            DA.GetDataList(0, directionsToCheck);

            List<double> availableDirections = new List<double>();
            DA.GetDataList(1, availableDirections);

            List<double> outPickedDirections = new List<double>();

            List<double> thresholds = Utilities.GetThresholds(availableDirections);

            for (int i = 0; i < directionsToCheck.Count; i++)
                    outPickedDirections.Add(Utilities.GetClosestDirection(directionsToCheck[i], thresholds));

            thresholds.RemoveAt(0);


            DA.SetDataList(0, outPickedDirections);
            DA.SetDataList(1, thresholds);

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
            get { return new Guid("69b8cd7a-57bc-4ccd-8af5-131a72c2981b"); }
        }
    }
}