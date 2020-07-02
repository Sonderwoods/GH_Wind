using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using FastFluidSolverMT;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// GH_Wind by Christoph Waibel.
// asynch version by Mathias Sønderskov Schaltz 2020, based on principles from:
// http://fucture.org/matas-ubarevicius/2016/05/23/async-grasshopper-components/#comment-52509

namespace GHWind
{


    public class GHFFDSolverAsyncMulti : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHFFDSolverAsync class.
        /// </summary>
        public GHFFDSolverAsyncMulti()
          : base("GS_GH_FFDSolver", "CFD",
              "Description",
              "GreenScenario", "Thermal")
        {
        }


        double[,,] p;
        List<double[,,]> veloutCen;
        List<double[,,]> veloutStag;
        double[,,] pstag;
        DataExtractor de;
        int[,,] obstacle_cells;
       
        List<DiscretizedGeometry> geoms = new List<DiscretizedGeometry>();
        Point3d origin = new Point3d();
        bool stopAll = false;

        int currentRun = 0;



        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            //#0
            pManager.AddGenericParameter("GeoClass", " GeoClass", "GeoClass from Discretize Meshes component.", GH_ParamAccess.list);

            //#1, #2
            pManager.AddNumberParameter("Domain size", "Domain size", "Domain x,y,z size in [m].", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Domain discretization", "Nx,Ny,Nz", "Domain discretization Nx, Ny, Nz, i.e. how many fluid cells in each direction.", GH_ParamAccess.list);

            //#3
            pManager.AddIntegerParameter("Terrain/Mode", "Terrain/Mode", "Terrain coefficients for wind speed. \n0 = Ocean; \n1 = Flat, open country\n2 = Rough, wooded country, urban, industrial, forest\n3 = Towns and Cities(default)\n4 = OpenFoam abl profile.", GH_ParamAccess.item, 3);
            pManager[3].Optional = true;

            //#4
            pManager.AddNumberParameter("No of steps", "steps", "Rule of thumb:\nDomain Depth/wind speed*2.5\n\nCalculation time horizon, i.e. sum of dt. until termination. Should be sufficient for domain to converge, but shouldn't be too much, otherwise wasted time. Numeric convergence indicators would help here.", GH_ParamAccess.item);

            //#5
            pManager.AddNumberParameter("_Time Step", "_dt", "Calculation time step dt.\nDefault = 1", GH_ParamAccess.item, 1);
            pManager[5].Optional = true;

            //#6
            pManager.AddNumberParameter("_Wind Speed", "_Vmet", "Wind Speed [m/s] at meteorological station at 10 m height above ground. Default is 5m/s", GH_ParamAccess.item, 5);
            pManager[6].Optional = true;



            //#7
            pManager.AddTextParameter("_Solver Parameters", "_params", "FFD solver parameters. Provide a semicolon-separated string, e.g. '1.511e-5;1e-4;1;30;2;false;0.7;false;0.1'. Items: 'kinematic viscosity (double); tolerance (double); min_iter (int); max_iter (int); backtrace_order (int, 1 or 2); mass_correction (true or false); mass_corr_alpha (double), verbose (true or false); surface roughness height [m], only if terrain/mode = 4 (OpenFoam ABL function).\nDefault is '1.511e-5;1e-4;1;10;2;false;0.7;false;0.01'", GH_ParamAccess.item, "1.511e-5;1e-4;1;10;2;false;0.7;false;0.01");
            pManager[7].Optional = true;

            //#8
            pManager.AddBooleanParameter("Run?", "Run?", "Run the solver. (Loop via Grasshopper timer component)", GH_ParamAccess.item);

            //#9
            pManager.AddBooleanParameter("Stop all?", "Stop all?", "stop all", GH_ParamAccess.item, false);
            pManager[9].Optional = true;

            //#10
            pManager.AddBooleanParameter("Stop one?", "Stop one?", "stop one", GH_ParamAccess.item, false);
            pManager[10].Optional = true;



            //#
            //pManager.AddBooleanParameter("Residuals?", "Residuals?", "Calculate residuals for convergence analysis? Writes text file to 'C:\residuals.txt'.", GH_ParamAccess.item);
            //pManager[10].Optional = true;

            //#11
            //pManager.AddIntegerParameter("_mean_dt", "_mean_dt", "m*dt for outputting mean flow field (instead of snapshot). m should be identified by observing the residuals. Default is m=10.", GH_ParamAccess.item, 10);
            //pManager[11].Optional = true;


            //#
            //pManager.AddBooleanParameter("_Results?", "_Results?", "Output Data Extractor class? E.g. for Cp calculation, or flow visualization.", GH_ParamAccess.item, true);
            //pManager[7].Optional = true;

            //#
            //pManager.AddBooleanParameter("_Export VTK", "_ExpVTK", "Export Results to VTK. Also writes VTK geometry file.", GH_ParamAccess.item, false);
            //pManager[8].Optional = true;

        }


        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ffdSolvers", "ffdSolvesr", "Connect to Results Viewer", GH_ParamAccess.list);

        }

        FFDSolver oldFFDSolver;
        List<FFDSolver> ffdSolvers = new List<FFDSolver>();
        FFDSolver ffdSolver = new FFDSolver();

        double[,,] pstagResults;
        bool skipSolution;
        bool componentBusy;


        bool RunAll()
        {
            //Rhino.RhinoApp.WriteLine($"== STARTALL == (ffdSolver count = {ffdSolvers.Count})");
            stopAll = false;
            for (int i = 0; i < ffdSolvers.Count; i++)
            {
                currentRun = i;
                if (stopAll)
                    break;
                Rhino.RhinoApp.WriteLine($"\n[{i+1}/{ffdSolvers.Count}] starting");
                ffdSolvers[i].Run();
            }
            return true;

        }

        bool CreateAll(string filepath, List<int> Nxyz, List<double> xyzsize, double t_end, double dt, int meanDt, double Vmet = 10, int terrain = 0, string strparam = "")
        {

            StopAll();

            ffdSolvers = new List<FFDSolver>();
            FFDSolver.ID = 0;

            for (int i = 0; i < geoms.Count; i++)
            {
                
                Rhino.RhinoApp.WriteLine($"[{1+i}/{geoms.Count}] creating domain and starting");
                ffdSolvers.Add(new FFDSolver(
                    this.OnPingDocument().FilePath,
                    Nxyz,
                    xyzsize,
                    geoms[i].myListOfCubes,
                    t_end,
                    dt,
                    meanDt,
                    Vmet,
                    terrain,
                    strparam,
                    maxSolvers: geoms.Count
                  ));


            }
            return true;

        }

        bool StopAll()
        {
            for (int i = 0; i < ffdSolvers.Count; i++)
            {
                ffdSolvers[i].run = false;
                ffdSolvers[i].StopRun();
            }
            stopAll = true;
            return true;

        }

        bool StopOne()
        {
            ffdSolvers[currentRun].run = false;
            ffdSolvers[currentRun].StopRun();
            return true;

        }


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        /// 
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            string filepath;
            filepath = Path.GetDirectoryName(this.OnPingDocument().FilePath);
            string residualstxt = filepath + @"\\residual.txt";


            geoms = new List<DiscretizedGeometry>();
            if (!DA.GetDataList(0, geoms)) { return; };

            List<double> xyzsize = new List<double>();
            if (!DA.GetDataList(1, xyzsize)) { return; };

            List<int> Nxyz = new List<int>();
            if (!DA.GetDataList(2, Nxyz)) { return; };
            int Nx = Nxyz[0];
            int Ny = Nxyz[1];
            int Nz = Nxyz[2];

            
            

            //terrain type
            int terrain = 3;
            if (!DA.GetData(3, ref terrain)) { return; }

            // horizon
            double t_end = 1;
            if (!DA.GetData(4, ref t_end)) { return; }

            // time step
            double dt = 0.1;
            if (!DA.GetData(5, ref dt)) { return; }

            // wind speed
            double Vmet = 5;
            if (!DA.GetData(6, ref Vmet)) { return; }



            string strparam = null;
            DA.GetData(7, ref strparam);
            string[] str_params = null;
            if (strparam != null) str_params = strparam.Split(';');


            bool run = false;
            if (!DA.GetData(8, ref run)) { return; }

            int meanDt =15;
            //DA.GetData(9, ref meanDt);

            bool stop = false;
            DA.GetData(9, ref stop);

            bool stopOne = false;
            DA.GetData(10, ref stopOne);



            if (stop)
                StopAll();

            if (stopOne)
                StopOne();



            if (skipSolution && (run == false))
            {
                skipSolution = false;
                DA.IncrementIteration();

                DA.SetDataList(0, ffdSolvers);

                Rhino.RhinoApp.WriteLine("Updated all outputs to GH");
                Grasshopper.Instances.RedrawAll();
            }
            else if (!componentBusy)
            {
                DA.DisableGapLogic();

                bool ReturnSomething()
                {
                    return true;
                }

                Task<bool> computingTask = new Task<bool>(() => ReturnSomething()); //to create the scope .. 


                if (run)
                {

                        Rhino.RhinoApp.WriteLine("..createall");
                        CreateAll(
                            this.OnPingDocument().FilePath,
                            Nxyz,
                            xyzsize,
                            t_end,
                            dt,
                            meanDt,
                            Vmet,
                            terrain,
                            strparam
                        );

                    computingTask = new Task<bool>(() => RunAll());
                }


                computingTask.ContinueWith(r =>
                {
                    if (r.Status == TaskStatus.RanToCompletion)
                    {
                        bool result = computingTask.Result;
                        if (result == true)
                        {
                            NickName = "GS_GH_Wind - Finished!";
                            skipSolution = true;

                            pstagResults = ffdSolver.pstag;



                            p = ffdSolver.p;
                            veloutCen = ffdSolver.veloutCen;
                            veloutStag = ffdSolver.veloutStag;
                            pstag = ffdSolver.pstag;
                            de = ffdSolver.de;
                            obstacle_cells = ffdSolver.obstacle_cells;


                            ExpireSolution(false);
                            Grasshopper.Instances.ActiveCanvas.Document.NewSolution(false);
                        }
                        else
                        {
                            Rhino.RhinoApp.WriteLine("failed");
                            NickName = "GS_GH_Wind - Failed.";
                            Grasshopper.Instances.RedrawAll();
                        }
                        componentBusy = false;

                    }
                    else if (r.Status == TaskStatus.Faulted)
                    {
                        NickName = "GS_GH_Wind - Faulted.";
                        Grasshopper.Instances.RedrawAll();
                        componentBusy = false;

                    }


                },
                TaskScheduler.FromCurrentSynchronizationContext()


                );

                computingTask.Start();
                if (run)
                    NickName = "GS_GH_Wind - Processing...";
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
            get { return new Guid("8c39b979-2aa8-44ed-a134-85d0e909fcde"); }
        }
    }
}