using System;
using System.Collections.Generic;
using FastFluidSolverMT;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHResultsviewer : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHResultsviewer class.
        /// </summary>
        public GHResultsviewer()
          : base("Wind results viewer", "Wind Results",
              "Connect me to the CFD simulation to get outputs.",
              "GH_Wind", "07 | Preview")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("ffdSolver", "ffdSolver", "input ffdSolver from the Asynch component", GH_ParamAccess.list);
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
            pManager.AddNumberParameter("values", "values", "values", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            FFDSolver ffdSolver;
            List < FFDSolver > ffdSolvers = new List<FFDSolver>();
            
            
            DA.GetDataList(0, ffdSolvers);

            if (ffdSolvers.Count == 0)
                return;
            ffdSolver = ffdSolvers[0];

            Domain omega = ffdSolver.omega ;
            FluidSolver ffd = ffdSolver.ffd;
            DataExtractor de = new DataExtractor(ffdSolver.omega, ffdSolver.ffd);


            //double[,,] pstagResults = ffdSolver.pstag;



            //double[,,] p = ffdSolver.p;
            //List<double[,,]> veloutCen = ffdSolver.veloutCen;
            //List<double[,,]> veloutStag = ffdSolver.veloutStag;
            //double[,,] pstag = ffdSolver.pstag;
            //DataExtractor de = ffdSolver.de;
            //int[,,] obstacle_cells = ffdSolver.obstacle_cells;


            //DA.SetDataList(0, veloutCen);
            //DA.SetData(1, p);
            //DA.SetDataList(2, veloutStag);
            //DA.SetData(3, pstag);
            ////DA.SetData(3, pstagResults);
            //DA.SetData(4, de);
            //DA.SetData(5, obstacle_cells);
            //DA.SetData(6, ffdSolver);







            int Nx = ffdSolver.Nx;
            int Ny = ffdSolver.Ny;
            int Nz = ffdSolver.Nz;
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // I could move all this away, an only output de data extractor
            double[,,] p = new double[Nx, Ny, Nz];
            double[,,] vu = new double[Nx, Ny, Nz];
            double[,,] vv = new double[Nx, Ny, Nz];
            double[,,] vw = new double[Nx, Ny, Nz];

            double[,,] pstag = new double[Nx + 1, Ny + 1, Nz + 1];
            double[,,] vustag = new double[Nx + 1, Ny + 1, Nz + 1];
            double[,,] vvstag = new double[Nx + 1, Ny + 1, Nz + 1];
            double[,,] vwstag = new double[Nx + 1, Ny + 1, Nz + 1];

            for (int i = 0; i < Nx; i++)
            {
                for (int j = 0; j < Ny; j++)
                {
                    for (int k = 0; k < Nz; k++)
                    {
                        if (omega.obstacle_cells[i + 1, j + 1, k + 1] != 1)
                        {
                            p[i, j, k] = de.get_pressure(i * omega.hx + 0.5 * omega.hx, j * omega.hy + 0.5 * omega.hy, k * omega.hz + 0.5 * omega.hz);
                            double[] vel = de.get_velocity(i * omega.hx + 0.5 * omega.hx, j * omega.hy + 0.5 * omega.hy, k * omega.hz + 0.5 * omega.hz);
                            vu[i, j, k] = vel[0];
                            vv[i, j, k] = vel[1];
                            vw[i, j, k] = vel[2];
                        }
                        else
                        {
                            p[i, j, k] = 0;
                            vu[i, j, k] = 0;
                            vv[i, j, k] = 0;
                            vw[i, j, k] = 0;

                        }
                        pstag[i, j, k] = de.get_pressure(i * omega.hx, j * omega.hy, k * omega.hz);
                        double[] velcen = de.get_velocity(i * omega.hx, j * omega.hy, k * omega.hz);
                        vustag[i, j, k] = velcen[0];
                        vvstag[i, j, k] = velcen[1];
                        vwstag[i, j, k] = velcen[2];
                    }
                }
            }

            //last x slice
            for (int j = 0; j < Ny + 1; j++)
            {
                for (int k = 0; k < Nz + 1; k++)
                {
                    pstag[Nx, j, k] = de.get_pressure((Nx) * omega.hx, j * omega.hy, k * omega.hz);
                    double[] vcen = de.get_velocity((Nx) * omega.hx, j * omega.hy, k * omega.hz);
                    vustag[Nx, j, k] = vcen[0];
                    vvstag[Nx, j, k] = vcen[1];
                    vwstag[Nx, j, k] = vcen[2];
                }
            }

            //last y slice
            for (int i = 0; i < Nx + 1; i++)
            {
                for (int k = 0; k < Nz + 1; k++)
                {
                    pstag[i, Ny, k] = de.get_pressure(i * omega.hx, (Ny) * omega.hy, k * omega.hz);
                    double[] vcen = de.get_velocity(i * omega.hx, (Ny) * omega.hy, k * omega.hz);
                    vustag[i, Ny, k] = vcen[0];
                    vvstag[i, Ny, k] = vcen[1];
                    vwstag[i, Ny, k] = vcen[2];
                }
            }

            //last z slice
            for (int i = 0; i < Nx + 1; i++)
            {
                for (int j = 0; j < Ny + 1; j++)
                {
                    pstag[i, j, Nz] = de.get_pressure(i * omega.hx, j * omega.hy, (Nz) * omega.hz);
                    double[] vcen = de.get_velocity(i * omega.hx, j * omega.hy, (Nz) * omega.hz);
                    vustag[i, j, Nz] = vcen[0];
                    vvstag[i, j, Nz] = vcen[1];
                    vwstag[i, j, Nz] = vcen[2];
                }
            }

            List<double[,,]> veloutCen = new List<double[,,]> { };
            veloutCen.Add(vu);
            veloutCen.Add(vv);
            veloutCen.Add(vw);

            List<double[,,]> veloutStag = new List<double[,,]> { };
            veloutStag.Add(vustag);
            veloutStag.Add(vvstag);
            veloutStag.Add(vwstag);



            DA.SetDataList(0, veloutCen);
            DA.SetData(1, p);
            DA.SetDataList(2, veloutStag);
            DA.SetData(3, pstag);
            DA.SetData(4, de);

            DA.SetData(5, omega.obstacle_cells);






            
            GH_Structure<GH_Number> outNumbers = new GH_Structure<GH_Number>();

            for (int j = 0; j < veloutStag[0].GetLength(0); j++)
            {
                for (int k = 0; k < veloutStag[0].GetLength(1); k++)
                {
                    double velocity = Math.Sqrt(veloutStag[0][j, k, 1] * veloutStag[0][j, k, 1] + veloutStag[1][j, k, 1] * veloutStag[1][j, k, 1] + veloutStag[2][j, k, 1] * veloutStag[2][j, k, 1]);

                    outNumbers.Append(new GH_Number(velocity), new GH_Path(j, k));
                }

            }

            DA.SetDataTree(7, outNumbers);

            
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
                return GHWind.Properties.Resources.gs_wind;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("1201d906-7c46-4f7c-ab2d-fd113c7563d2"); }
        }
    }
}