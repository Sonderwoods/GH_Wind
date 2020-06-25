﻿using System;
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
          : base("GHFFDSolverAsyncMulti", "GHFFDSolverAsyncMulti",
              "Description",
              "EnergyHubs", "Wind Simulation")
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



        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            //#0, #1
            pManager.AddNumberParameter("Domain size", "Domain size", "Domain x,y,z size in [m].", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Domain discretization", "Nx,Ny,Nz", "Domain discretization Nx, Ny, Nz, i.e. how many fluid cells in each direction.", GH_ParamAccess.list);
            //later make another input for defining more precisely the domain. like, internal flow, external flow, inflows, outflows...)

            //#2
            pManager.AddGenericParameter("Geometry", " Geometry", "Geometry as list of doubles [6] {xmin, xmax, ymin, ymax, zmin, zmax}, representing the obstacle cubes.", GH_ParamAccess.list);
            //pManager[2].Optional = true;

            //#3
            pManager.AddNumberParameter("Time Step", "dt", "Calculation time step dt.", GH_ParamAccess.item);

            //#4
            pManager.AddNumberParameter("Horizon", "Horizon", "Calculation time horizon, i.e. sum of dt. until termination. Should be sufficient for domain to converge, but shouldn't be too much, otherwise wasted time. Numeric convergence indicators would help here.", GH_ParamAccess.item);

            //#5
            pManager.AddNumberParameter("Wind Speed", "Vmet", "Wind Speed [m/s] at meteorological station at 10 m height above ground.", GH_ParamAccess.item);

            //#6
            pManager.AddIntegerParameter("Terrain/Mode", "Terrain/Mode", "Terrain coefficients for wind speed. 0 = Ocean; 1 = Flat, open country; 2 = Rough, wooded country, urban, industrial, forest; 3 = Towns and Cities. Or OpenFoam profile = 4.", GH_ParamAccess.item);

            //#7
            pManager.AddBooleanParameter("Run?", "Run?", "Run the solver. (Loop via Grasshopper timer component)", GH_ParamAccess.item);

            //#8
            pManager.AddBooleanParameter("Results?", "Results?", "Output Data Extractor class? E.g. for Cp calculation, or flow visualization.", GH_ParamAccess.item);
            pManager[8].Optional = true;

            //#9
            pManager.AddBooleanParameter("Export VTK", "ExpVTK", "Export Results to VTK. Also writes VTK geometry file.", GH_ParamAccess.item);
            pManager[9].Optional = true;

            //#10
            pManager.AddBooleanParameter("Reset", "Reset", "Reset domain", GH_ParamAccess.item);
            pManager[10].Optional = true;

            //#11
            pManager.AddTextParameter("Solver Parameters", "params", "FFD solver parameters. Provide a semicolon-separated string, e.g. '1.511e-5;1e-4;1;30;2;false;0.7;false;0.1'. Items: 'kinematic viscosity (double); tolerance (double); min_iter (int); max_iter (int); backtrace_order (int, 1 or 2); mass_correction (true or false); mass_corr_alpha (double), verbose (true or false); surface roughness height [m], only if terrain/mode = 4 (OpenFoam ABL function).", GH_ParamAccess.item);
            pManager[11].Optional = true;

            //#12
            pManager.AddBooleanParameter("Residuals?", "Residuals?", "Calculate residuals for convergence analysis? Writes text file to 'C:\residuals.txt'.", GH_ParamAccess.item);
            pManager[12].Optional = true;

            //#13
            pManager.AddIntegerParameter("mean_dt", "mean_dt", "m*dt for outputting mean flow field (instead of snapshot). m should be identified by observing the residuals. Default is m=10.", GH_ParamAccess.item);
            pManager[13].Optional = true;

            //#14
            pManager.AddBooleanParameter("stop", "stop", "stop", GH_ParamAccess.item, false);
            pManager[14].Optional = true;

            //#15
            pManager.AddBooleanParameter("update", "update", "update", GH_ParamAccess.item, false);
            pManager[15].Optional = true;


            //#17
            pManager.AddPointParameter("origin", "origin", "origin point ", GH_ParamAccess.item);
            //pManager[16].Optional = true;
        }


        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //#0,1
            pManager.AddGenericParameter("v centred", "v centred", "velocities, cell centred", GH_ParamAccess.list);
            pManager.AddGenericParameter("p centred", "p centred", "pressure, cell centred", GH_ParamAccess.item);
            //((IGH_PreviewObject)pManager[0]).Hidden = true;


            //#2,3
            pManager.AddGenericParameter("v staggered", "v staggered", "velocities, on staggered grid", GH_ParamAccess.list);
            pManager.AddGenericParameter("p staggered", "p staggered", "pressure, on staggered grid", GH_ParamAccess.item);

            //#4
            pManager.AddGenericParameter("DE", "DE", "Data Extractor, containing omega and FFD classes", GH_ParamAccess.item);
            //#5
            pManager.AddGenericParameter("obst domain", "obst domain", "Boolean array indicating obstacle cell (1) or fluid cell (0) of the entire domain.", GH_ParamAccess.item);
            //#6
            pManager.AddGenericParameter("ffdSolver", "ffdSolver", "ffdSolver", GH_ParamAccess.list);
            //#7
            pManager.AddGenericParameter("domain", "domain", "domain", GH_ParamAccess.item);
            //#8
            pManager.AddGenericParameter("fs", "fs", "fs", GH_ParamAccess.item);

            //#9
            pManager.AddBoxParameter("fs", "fs", "fs", GH_ParamAccess.list);

            // pManager.AddTextParameter("VTK path", "VTK path", "Output path of VTK results file", GH_ParamAccess.item);
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
                if (stopAll)
                    break;
                Rhino.RhinoApp.WriteLine($"[{i}] STARTING");
                ffdSolvers[i].Run();
            }
            return true;

        }

        bool CreateAll(string filepath, List<int> Nxyz, List<double> xyzsize, double t_end, double dt, int meanDt, double Vmet = 10, int terrain = 0, string strparam = "")
        {
            Rhino.RhinoApp.WriteLine($"== CREATE ALL == (allGeometryDoubles.Count = {geoms.Count})");

            

            StopAll();

            ffdSolvers = new List<FFDSolver>();
            FFDSolver.ID = 0;

            for (int i = 0; i < geoms.Count; i++)
            {

                Rhino.RhinoApp.WriteLine($"== creating == {i} (allGeometryDoubles.Count = {geoms.Count})");
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
                    strparam
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


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        /// 
        protected override void SolveInstance(IGH_DataAccess DA)
        {




            string filepath;

            //Domain omega;
            //FluidSolver ffd;
            //DataExtractor de;

            //double t;
            bool resetFFD = false;


            // current filepath
            filepath = Path.GetDirectoryName(this.OnPingDocument().FilePath);
            string residualstxt = filepath + @"\\residual.txt";


            // *********************************************************************************
            // Inputs
            // *********************************************************************************


            geoms = new List<DiscretizedGeometry>();
            
            if (!DA.GetDataList(2, geoms)) { return; };





            List<double> xyzsize = new List<double>();
            if (!DA.GetDataList(0, xyzsize)) { return; };

            List<int> Nxyz = new List<int>();
            if (!DA.GetDataList(1, Nxyz)) { return; };
            int Nx = Nxyz[0];
            int Ny = Nxyz[1];
            int Nz = Nxyz[2];
            

            // time step
            double dt = 0.1;
            if (!DA.GetData(3, ref dt)) { return; }

            // horizon
            double t_end = 1;
            if (!DA.GetData(4, ref t_end)) { return; }

            // wind speed
            double Vmet = 10;
            if (!DA.GetData(5, ref Vmet)) { return; }

            //terrain type
            int terrain = 0;
            if (!DA.GetData(6, ref terrain)) { return; }


            bool run = false;
            if (!DA.GetData(7, ref run)) { return; }



            //List<Mesh> mshCp = new List<Mesh>();
            //DA.GetDataList(10, mshCp);
            bool writeresults = false;
            DA.GetData(8, ref writeresults);

            bool writeVTK = false;
            DA.GetData(9, ref writeVTK);


            DA.GetData(10, ref resetFFD);

            string strparam = null;
            DA.GetData(11, ref strparam);


            //bool calcres = false;
            //DA.GetData(12, ref calcres);

            //int m = 10;
            //DA.GetData(13, ref m);



            string[] str_params = null;
            if (strparam != null) str_params = strparam.Split(';');


            bool addResiduals = true;
            DA.GetData(12, ref addResiduals);

            int meanDt = 10;
            DA.GetData(13, ref meanDt);

            bool stop = false;
            DA.GetData(14, ref stop);

            bool update = false;
            DA.GetData(15, ref update);


            DA.GetData(16, ref origin);


            if (stop)
            {

                //ffdSolver.StopRun();
                StopAll();
                //running[0] = false;
                //Rhino.RhinoApp.WriteLine("stoprun()");

                //computingTask.Dispose();
                //Rhino.RhinoApp.WriteLine("dispose()");
            }



            if (skipSolution && (run == false || update))
            {
                skipSolution = false;
                DA.IncrementIteration();

                //DA.SetDataList(0, veloutCen);
                //DA.SetData(1, p);
                //DA.SetDataList(2, veloutStag);
                //DA.SetData(3, pstag);
                //DA.SetData(3, pstagResults);
                //DA.SetData(4, de);
                //DA.SetData(5, obstacle_cells);
                DA.SetDataList(6, ffdSolvers);
                //DA.SetData(7, ffdSolver.omega);
                //DA.SetData(8, ffdSolver.ffd);
                //Rhino.RhinoApp.WriteLine("trying to update outputs");
                Grasshopper.Instances.RedrawAll();
            }
            else if (!componentBusy)
            {
                DA.DisableGapLogic();

                //if (resetFFD)
                //{
                //    ffdSolver.run = false;
                //    ffdSolver = new FFDSolver(
                //        this.OnPingDocument().FilePath,
                //        Nxyz,
                //        xyzsize,
                //        geom,
                //        t_end,
                //        Vmet,
                //        terrain,
                //        strparam
                //        );

                //}

                bool ReturnSomething()
                {
                    return true;
                }

                Task<bool> computingTask = new Task<bool>(() => ReturnSomething());


                if (resetFFD)
                {


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

                }

                if (run)
                {

                    if (ffdSolvers.Count == 0)
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

                    }

                    //ffdSolver.run = false;
                    //if (ffdSolver[0].dt == 0.0) //we know it's empty.
                    //{
                    //    ffdSolver = new FFDSolver(
                    //   this.OnPingDocument().FilePath,
                    //   Nxyz,
                    //   xyzsize,
                    //   geom,
                    //   t_end,
                    //   dt,
                    //   meanDt,
                    //   Vmet,
                    //   terrain,
                    //   strparam
                    //   );
 

                   //RunAll();
                    //}



                    //computingTask = new Task<bool>(() => ffdSolver.Run());
                    computingTask = new Task<bool>(() => RunAll());
                }




                computingTask.ContinueWith(r =>
                {
                    if (r.Status == TaskStatus.RanToCompletion)
                    {
                        bool result = computingTask.Result;
                        if (result == true)
                        {
                            //Rhino.RhinoApp.WriteLine("outputting");
                            NickName = "Asynch - Task Finished!";
                            skipSolution = true;

                            //pstagResults = ffdSolver.pstag;



                            //p = ffdSolver.p;
                            //veloutCen = ffdSolver.veloutCen;
                            //veloutStag = ffdSolver.veloutStag;
                            //pstag = ffdSolver.pstag;
                            de = ffdSolver.de;
                            obstacle_cells = ffdSolver.obstacle_cells;



                            ExpireSolution(false);
                            Grasshopper.Instances.ActiveCanvas.Document.NewSolution(false);
                        }
                        else
                        {
                            Rhino.RhinoApp.WriteLine("failed");
                            NickName = "Asynch - Task Failed.";
                            Grasshopper.Instances.RedrawAll();
                        }
                        componentBusy = false;

                    }
                    else if (r.Status == TaskStatus.Faulted)
                    {
                        NickName = "Asynch - Task Faulted.";
                        Grasshopper.Instances.RedrawAll();
                        componentBusy = false;

                    }


                },
                TaskScheduler.FromCurrentSynchronizationContext()


                );

                computingTask.Start();
                if (run)
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
            get { return new Guid("8c39b979-2aa8-44ed-a134-85d0e909fcde"); }
        }
    }
}