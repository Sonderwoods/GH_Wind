using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GHWind
{
    public class GHtestAsync : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHtestAsync class.
        /// </summary>
        public GHtestAsync()
          : base("GHtestAsync", "Nickname",
              "Description",
              "Category", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        Polyline resultPolyline;
        bool skipSolution;
        bool componentBusy;

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (skipSolution)
            {
                skipSolution = false;
                DA.IncrementIteration();
                DA.SetData(0, resultPolyline);
                Grasshopper.Instances.RedrawAll();
            }
            else if (!componentBusy)
            {
                DA.DisableGapLogic();

                Brep BBox = null;
                if (!DA.GetData(0, ref BBox)) return;

                //someComputingEngine = new SomeComputingEngine();

                //Task<Polyline> computingTask = new Task<Polyline>(() => someComputingEngine.GenerateThePath(BBox));
                //computingTask.ContinueWith(r =>
                {
                    if (r.Status == TaskStatus.RanToCompletion)
                    {
                        Polyline pln = computingTask.Result;
                        if (pln != null)
                        {
                            NickName = "Task Finished!";
                            skipSolution = true;
                            resultPolyline = pln;
                            ExpireSolution(false);
                            Grasshopper.Instances.ActiveCanvas.Document.NewSolution(false);
                        }
                        else
                        {
                            NickName = "Task Failed.";
                            Grasshopper.Instances.RedrawAll();
                        }
                        componentBusy = false;
                    }
                    else if (r.Status == TaskStatus.Faulted)
                    {
                        NickName = "Task Failed.";
                        Grasshopper.Instances.RedrawAll();
                        componentBusy = false;
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext());
                //computingTask.Start();
                NickName = "Processing...";
                Grasshopper.Instances.RedrawAll();
                componentBusy = true;
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
            get { return new Guid("77105a50-c425-4478-a7f4-a7df641f502d"); }
        }
    }
}