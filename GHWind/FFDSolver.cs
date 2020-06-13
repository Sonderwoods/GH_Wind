using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

using FastFluidSolverMT;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;

// GH_Wind by Christoph Waibel.
// https://github.com/christophwaibel/GH_Wind

namespace GHWind
{
    public class FFDSolver
    {

        public string filepath = String.Empty;
        public double dt = 0.0;
        public List<double> xyzsize = new List<double>();
        public List<int> Nxyz  = new List<int>();
        public List<double[]> geom  = new List<double[]>();
        public double t_end;
        public double Vmet;
        public int terrain;
        public bool run;

        private FluidSolver[] ffd_old;




        public string residualstxt;
        public DataExtractor de;
        public List<double[,,]> veloutCen = new List<double[,,]> { };
        public List<double[,,]> veloutStag = new List<double[,,]> { };
        public double[,,] p;
        public double[,,] pstag;
        public Domain omega;
        public int[,,] obstacle_cells;

        PostProcessor pp;




        double[,,] u0;
        double[,,] v0;
        double[,,] w0;

        // Create empty arrays for body forces
        double[,,] f_x;
        double[,,] f_y;
        double[,,] f_z;




        public int Nx;
        public int Ny;
        public int Nz;

        bool writeresults = false;
        bool writeVTK = false;
        bool calcres = false;
        int m = 10;
        string strparam = null;
        string[] str_params = null;


        int counter = 0;
        int timestep = 0;


        public FluidSolver ffd;


        double t;
        bool resetFFD;

        public FFDSolver()
        {
        }

        public FFDSolver(string filepath, List<int> Nxyz, List<double> xyzsize, List<double[]> geom,  double t_end, double dt, int meanDt, double Vmet = 10, int terrain = 0, string strparam = "")
        {
            m = meanDt;
            Rhino.RhinoApp.WriteLine("established with long overload");
            this.filepath = filepath;
            residualstxt = filepath + @"\\residual.txt";
            if (Nxyz.Count == 0)
            {
                Rhino.RhinoApp.WriteLine("returned count");
                return;
            } else
            {
                this.Nxyz = Nxyz;
            }
            if (xyzsize.Count != 3)
            {
                Rhino.RhinoApp.WriteLine("returned count xyz");
                return;
            }
            Rhino.RhinoApp.WriteLine("{0}", xyzsize.Count);
            Nx = Nxyz[0];
            Ny = Nxyz[1];
            Nz = Nxyz[2];
 
            this.dt = dt;
            this.geom = geom;
            this.t_end = t_end;
            this.Vmet = Vmet;
            this.terrain = terrain;
            this.xyzsize = xyzsize;
            if (strparam != null) str_params = strparam.Split(';');
        }

        public void StopRun()
        {
            run = false;
            //Rhino.RhinoApp.WriteLine("pickedup the stop!");
            
            

        }

        public bool Run()
        {

            if (xyzsize.Count == 0)
            {
                Rhino.RhinoApp.WriteLine("Error");
                return false;
            }

                
            run = true;

            counter = 0;
            timestep = 0;

            



            double nu = 1.511e-5;       // increase viscosity to impose turbulence. the higher velocity, the higher visc., 1e-3
            FluidSolver.solver_struct solver_params = new FluidSolver.solver_struct();
            double z0;
            if (str_params != null)
            {
                nu = Convert.ToDouble(str_params[0]);
                solver_params.tol = Convert.ToDouble(str_params[1]);
                solver_params.min_iter = Convert.ToInt16(str_params[2]);
                solver_params.max_iter = Convert.ToInt16(str_params[3]);
                solver_params.backtrace_order = Convert.ToInt16(str_params[4]);
                solver_params.mass_correction = str_params[5].Equals("false") ? false : true;
                solver_params.mass_corr_alpha = Convert.ToDouble(str_params[6]);
                solver_params.verbose = str_params[7].Equals("false") ? false : true;
                z0 = Convert.ToDouble(str_params[8]);
            }
            else
            {
                solver_params.tol = 1e-4;
                solver_params.min_iter = 1;
                solver_params.max_iter = 30;
                solver_params.backtrace_order = 2;
                solver_params.mass_correction = false;
                solver_params.mass_corr_alpha = 0.7;
                solver_params.verbose = false;
                z0 = 0.1;
            }

            




            // *********************************************************************************
            // Set-up FFD Solver
            // *********************************************************************************
            // Set initial velocity conditions
            u0 = new double[Nx + 1, Ny + 2, Nz + 2];
            v0 = new double[Nx + 2, Ny + 1, Nz + 2];
            w0 = new double[Nx + 2, Ny + 2, Nz + 1];

            // Create empty arrays for body forces
            f_x = new double[Nx + 1, Ny + 2, Nz + 2];
            f_y = new double[Nx + 2, Ny + 1, Nz + 2];
            f_z = new double[Nx + 2, Ny + 2, Nz + 1];

            


            // Create FFD solver and domain
            //if (ffd == null || resetFFD)
            //{
            Rhino.RhinoApp.WriteLine("{0}, {1}, {2}", xyzsize[0], xyzsize[1], xyzsize[2]);
            if (terrain == 4)
            {
                omega = new WindInflowOpenFoam(Nx + 2, Ny + 2, Nz + 2, xyzsize[0], xyzsize[1], xyzsize[2], Vmet, z0);
            }
            else
            {
                omega = new WindInflow(Nx + 2, Ny + 2, Nz + 2, xyzsize[0], xyzsize[1], xyzsize[2], Vmet, terrain);
            }

            //Rhino.RhinoApp.WriteLine("M0005");

            foreach (double[] geo in geom)
            {
                
                omega.add_obstacle(geo[0], geo[1], geo[2], geo[3], geo[4], geo[5]);
            }

            //Rhino.RhinoApp.WriteLine("M0005a");

            ffd = new FluidSolver(omega, dt, nu, u0, v0, w0, solver_params);
            de = new DataExtractor(omega, ffd);
            t = 0;

            //Rhino.RhinoApp.WriteLine("M0006");

            pp = new PostProcessor(ffd, omega);


            //if (resetFFD) resetFFD = false;            //reset FFD solver and domain

            Rhino.RhinoApp.WriteLine("GRASSHOPPER FFD Air Flow Simulation.");
            Rhino.RhinoApp.WriteLine("GH Plug-in: https://github.com/christophwaibel/GH_Wind");
            Rhino.RhinoApp.WriteLine("FFD Solver: https://github.com/lukasbystricky/GSoC_FFD");
            Rhino.RhinoApp.WriteLine("________________________________________________________");
            Rhino.RhinoApp.WriteLine("...Domain initialized");
            Rhino.RhinoApp.WriteLine("________________________________________________________");
            //}


            if (run)
            {
                if (writeVTK) pp.export_geometry_vtk(filepath + @"\\vtk_geometry.vtk", 0);

                counter = 0;
                timestep = 0;
                ffd_old = new FluidSolver[m];

                if (calcres) File.AppendAllText(residualstxt, "pmin; pmax; pavg; umin; umax; uavg; vmin; vmax; vavg; wmin; wmax; wavg;\n");


                while (t < t_end)
                {
                    
                    if (GH_Document.IsEscapeKeyDown())
                    {
                        Rhino.RhinoApp.WriteLine("Cancelled by user");
                        //GH_Document GHDocument = OnPingDocument();
                        //GHDocument.RequestAbortSolution();
                        break;
                    }

                    if (run != true)
                        break;


                    RunStep(ref timestep, ref counter);

                }


                //averaging results 
                FluidSolver ffd_mean = new FluidSolver(ffd);
                ffd_mean.p = new double[ffd.p.GetLength(0), ffd.p.GetLength(1), ffd.p.GetLength(2)];
                ffd_mean.u = new double[ffd.u.GetLength(0), ffd.u.GetLength(1), ffd.u.GetLength(2)];
                ffd_mean.v = new double[ffd.v.GetLength(0), ffd.v.GetLength(1), ffd.v.GetLength(2)];
                ffd_mean.w = new double[ffd.w.GetLength(0), ffd.w.GetLength(1), ffd.w.GetLength(2)];
                for (int i = 0; i < ffd_mean.p.GetLength(0); i++)
                {
                    for (int j = 0; j < ffd_mean.p.GetLength(1); j++)
                    {
                        for (int k = 0; k < ffd_mean.p.GetLength(2); k++)
                        {
                            for (int u = 0; u < counter; u++)
                            {
                                ffd_mean.p[i, j, k] += ffd_old[u].p[i, j, k];
                            }
                            ffd_mean.p[i, j, k] /= counter;
                        }
                    }
                }

                for (int i = 0; i < ffd_mean.u.GetLength(0); i++)
                {
                    for (int j = 0; j < ffd_mean.u.GetLength(1); j++)
                    {
                        for (int k = 0; k < ffd_mean.u.GetLength(2); k++)
                        {
                            for (int u = 0; u < counter; u++)
                            {
                                ffd_mean.u[i, j, k] += ffd_old[u].u[i, j, k];
                            }
                            ffd_mean.u[i, j, k] /= counter;
                        }
                    }
                }

                for (int i = 0; i < ffd_mean.v.GetLength(0); i++)
                {
                    for (int j = 0; j < ffd_mean.v.GetLength(1); j++)
                    {
                        for (int k = 0; k < ffd_mean.v.GetLength(2); k++)
                        {
                            for (int u = 0; u < counter; u++)
                            {
                                ffd_mean.v[i, j, k] += ffd_old[u].v[i, j, k];
                            }
                            ffd_mean.v[i, j, k] /= counter;
                        }
                    }
                }

                for (int i = 0; i < ffd_mean.w.GetLength(0); i++)
                {
                    for (int j = 0; j < ffd_mean.w.GetLength(1); j++)
                    {
                        for (int k = 0; k < ffd_mean.w.GetLength(2); k++)
                        {
                            for (int u = 0; u < counter; u++)
                            {
                                ffd_mean.w[i, j, k] += ffd_old[u].w[i, j, k];
                            }
                            ffd_mean.w[i, j, k] /= counter;
                        }
                    }
                }

                de = new DataExtractor(omega, ffd_mean);

            }
            writeresults = true;

            // *******************************************************************************************
            // Redraw on or off
            // *******************************************************************************************
            //return mean over m*dt, instead of only one snapshot
            if (writeresults)
            {
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                // I could move all this away, an only output de data extractor
                p = new double[Nx, Ny, Nz];
                double[,,] vu = new double[Nx, Ny, Nz];
                double[,,] vv = new double[Nx, Ny, Nz];
                double[,,] vw = new double[Nx, Ny, Nz];

                pstag = new double[Nx + 1, Ny + 1, Nz + 1];
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
                obstacle_cells = omega.obstacle_cells;


            }

            return true;

        }


        private void RunStep(ref int timestep, ref int counter)
        {


            Rhino.RhinoApp.WriteLine(Convert.ToString(t) + " of " + Convert.ToString(t_end));

            double[,,] p_t2 = new double[ffd.p.GetLength(0), ffd.p.GetLength(1), ffd.p.GetLength(2)];
            Array.Copy(ffd.p, 0, p_t2, 0, ffd.p.Length);
            double[,,] u_t2 = new double[ffd.u.GetLength(0), ffd.u.GetLength(1), ffd.u.GetLength(2)];
            Array.Copy(ffd.u, 0, u_t2, 0, ffd.u.Length);
            double[,,] v_t2 = new double[ffd.v.GetLength(0), ffd.v.GetLength(1), ffd.v.GetLength(2)];
            Array.Copy(ffd.v, 0, v_t2, 0, ffd.v.Length);
            double[,,] w_t2 = new double[ffd.w.GetLength(0), ffd.w.GetLength(1), ffd.w.GetLength(2)];
            Array.Copy(ffd.w, 0, w_t2, 0, ffd.w.Length);

            ffd.time_step(f_x, f_y, f_z);
            if (t > dt && calcres)
            {
                double[] p_residuals;
                double[,,] p_t1 = ffd.p;
                FastFluidSolverMT.Utilities.calculate_residuals(p_t1, p_t2, out p_residuals);
                Rhino.RhinoApp.WriteLine("p residuals: {0};{1};{2}", p_residuals[0], p_residuals[1], p_residuals[2]);
                double[] u_residuals;
                double[,,] u_t1 = ffd.u;
                FastFluidSolverMT.Utilities.calculate_residuals(u_t1, u_t2, out u_residuals);
                Rhino.RhinoApp.WriteLine("u residuals: {0};{1};{2}", u_residuals[0], u_residuals[1], u_residuals[2]);
                double[] v_residuals;
                double[,,] v_t1 = ffd.v;
                FastFluidSolverMT.Utilities.calculate_residuals(v_t1, v_t2, out v_residuals);
                Rhino.RhinoApp.WriteLine("v residuals: {0};{1};{2}", v_residuals[0], v_residuals[1], v_residuals[2]);
                double[] w_residuals;
                double[,,] w_t1 = ffd.w;
                FastFluidSolverMT.Utilities.calculate_residuals(w_t1, w_t2, out w_residuals);
                Rhino.RhinoApp.WriteLine("w residuals: {0};{1};{2}", w_residuals[0], w_residuals[1], w_residuals[2]);

                File.AppendAllText(residualstxt, Convert.ToString(p_residuals[0]) + ";" + Convert.ToString(p_residuals[1]) + ";" + Convert.ToString(p_residuals[2]) + ";" +
                    Convert.ToString(u_residuals[0]) + ";" + Convert.ToString(u_residuals[1]) + ";" + Convert.ToString(u_residuals[2]) + ";" +
                    Convert.ToString(v_residuals[0]) + ";" + Convert.ToString(v_residuals[1]) + ";" + Convert.ToString(v_residuals[2]) + ";" +
                    Convert.ToString(w_residuals[0]) + ";" + Convert.ToString(w_residuals[1]) + ";" + Convert.ToString(w_residuals[2]) + "\n");
            }

            if (t >= t_end - m * dt)
            {
                ffd_old[counter] = new FluidSolver(ffd);
                counter++;
            }
            if (writeVTK) pp.export_data_vtk(filepath + @"\\vtk_" + timestep + ".vtk", t, false);
            t += dt;

        }

    }
}