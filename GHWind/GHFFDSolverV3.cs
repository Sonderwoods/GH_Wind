using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHFFDSolverV3 : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHFFDSolverV3 class.
        /// </summary>
        public GHFFDSolverV3()
          : base("GHFFDSolverV3", "GHFFDSolverV3",
              "Description",
              "GreenScenario", "Wind")
        {
        }

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
            pManager.AddGenericParameter("Geometry", "Geometry", "Geometry as list of doubles [6] {xmin, xmax, ymin, ymax, zmin, zmax}, representing the obstacle cubes.", GH_ParamAccess.list);

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
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //#0,1
            pManager.AddGenericParameter("v centred", "v centred", "velocities, cell centred", GH_ParamAccess.list);
            pManager.AddGenericParameter("p centred", "p centred", "pressure, cell centred", GH_ParamAccess.item);

            //#2,3
            pManager.AddGenericParameter("v staggered", "v staggered", "velocities, on staggered grid", GH_ParamAccess.list);
            pManager.AddGenericParameter("p staggered", "p staggered", "pressure, on staggered grid", GH_ParamAccess.item);

            //#4
            pManager.AddGenericParameter("DE", "DE", "Data Extractor, containing omega and FFD classes", GH_ParamAccess.item);
            //#5
            pManager.AddGenericParameter("obst domain", "obst domain", "Boolean array indicating obstacle cell (1) or fluid cell (0) of the entire domain.", GH_ParamAccess.item);

            //#6
            pManager.AddGenericParameter("ffdSolver", "ffdSolver", "ffdSolver", GH_ParamAccess.item);

            //#7
            pManager.AddGenericParameter("domain", "domain", "domain", GH_ParamAccess.item);

            //#8
            pManager.AddGenericParameter("fs", "fs", "fs", GH_ParamAccess.item);

            //#9
            pManager.AddNumberParameter("values", "values", "values", GH_ParamAccess.tree);

            // pManager.AddTextParameter("VTK path", "VTK path", "Output path of VTK results file", GH_ParamAccess.item);
        }


        FFDSolver ffdSolver = new FFDSolver();


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {



            List<double> xyzsize = new List<double>();
            if (!DA.GetDataList(0, xyzsize)) { return; };

            List<int> Nxyz = new List<int>();
            if (!DA.GetDataList(1, Nxyz)) { return; };
            int Nx = Nxyz[0];
            int Ny = Nxyz[1];
            int Nz = Nxyz[2];



            List<double[]> geom = new List<double[]>();
            if (!DA.GetDataList(2, geom)) { return; };


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



            bool writeresults = false;
            DA.GetData(8, ref writeresults);

            bool writeVTK = false;
            DA.GetData(9, ref writeVTK);


            //DA.GetData(10, ref resetFFD);


            bool calcres = false;
            DA.GetData(12, ref calcres);

            int m = 10;
            DA.GetData(13, ref m);

            string strparam = null;
            DA.GetData(11, ref strparam);

            string[] str_params = null;
            if (strparam != null) str_params = strparam.Split(';');


            bool addResiduals = true;
            DA.GetData(12, ref addResiduals);

            int meanDt = 10;
            DA.GetData(13, ref meanDt);



            


            if (run)
            {




                ffdSolver.run = false;

                ffdSolver = new FFDSolver(
                this.OnPingDocument().FilePath,
                Nxyz,
                xyzsize,
                geom,
                t_end,
                dt,
                meanDt,
                Vmet,
                terrain,
                strparam
                );



                ffdSolver.Run();
            }

            DA.SetData(6, ffdSolver);

            Rhino.RhinoApp.WriteLine($"so far xx");

            GH_Structure<GH_Number> outNumbers = new GH_Structure<GH_Number>();

            Rhino.RhinoApp.WriteLine($"veloutstag count {ffdSolver.veloutStag.Count}");



            for (int j = 0; j < ffdSolver.veloutStag[0].GetLength(0); j++)
            {
                //Rhino.RhinoApp.WriteLine($"outputting numbers from {j}");


                for (int k = 0; k < ffdSolver.veloutStag[0].GetLength(1); k++)
                {

                    double velocity = Math.Sqrt(ffdSolver.veloutStag[0][j, k, 1] * ffdSolver.veloutStag[0][j, k, 1] + ffdSolver.veloutStag[1][j, k, 1] * ffdSolver.veloutStag[1][j, k, 1] + ffdSolver.veloutStag[2][j, k, 1] * ffdSolver.veloutStag[2][j, k, 1]);

                    outNumbers.Append(new GH_Number(velocity), new GH_Path(j, k));
                }

            }


            DA.SetDataList(0, ffdSolver.veloutCen);
            DA.SetData(1, ffdSolver.p);
            DA.SetDataList(2, ffdSolver.veloutStag);
            DA.SetData(3, ffdSolver.pstag);
            DA.SetData(4, ffdSolver.de);

            DA.SetData(5, ffdSolver.omega.obstacle_cells);



            DA.SetDataTree(9, outNumbers);


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
            get { return new Guid("029a2c27-2d2d-45db-9b89-12043bca13d5"); }
        }
    }
}