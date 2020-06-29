using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;

/*
 * GHVisualizerField.cs
 * Copyright 2017 Christoph Waibel <chwaibel@student.ethz.ch>
 * 
 * This work is licensed under the GNU GPL license version 3.
*/

namespace GHWind
{
    public class GHVisualizerFieldComfort : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHVisualizerField class.
        /// </summary>
        public GHVisualizerFieldComfort()
            : base("Field Visualizer Comfort", "Field Visualizer Comfort",
                "Dynamic field visualizer for the FFD solver. Draws and updates velocity and pressure values on a field at every timestep.",
                "GreenScenario", "Thermal")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            //0
            pManager.AddPointParameter("origin", "origin", "origin", GH_ParamAccess.item);

            //1
            pManager.AddIntegerParameter("section position", "section", "where exactly to draw section", GH_ParamAccess.item);

            //2
            pManager.AddNumberParameter("min", "min", "minimum value. needed for colour gradient", GH_ParamAccess.item);

            //3
            pManager.AddNumberParameter("max", "max", "maximum value. needed for colour gradient", GH_ParamAccess.item);

            //4
            pManager.AddGenericParameter("velocity", "velocity", "velocity", GH_ParamAccess.list);

            //5
            pManager.AddGenericParameter("pressure", "pressure", "pressure", GH_ParamAccess.item);

            //6
            pManager.AddNumberParameter("hx", "hx", "hx", GH_ParamAccess.item);

            //7
            pManager.AddNumberParameter("hy", "hy", "hy", GH_ParamAccess.item);

            //8
            pManager.AddNumberParameter("hz", "hz", "hz", GH_ParamAccess.item);

            //9
            pManager.AddIntegerParameter("colour sheme", "colours", "Colour sheme. 0: Blue (min) - Red - Yellow (max); 1: Blue (min) - Green - Red (max); 2: Black only.", GH_ParamAccess.item, 1);
            pManager[9].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("value field", "value field", "section showing pressure or velocity values", GH_ParamAccess.item);
            pManager.AddPointParameter("pts", "pts", "pts", GH_ParamAccess.list);
            pManager.AddNumberParameter("vel", "vel", "vel", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d origin = Point3d.Unset;
            if (!DA.GetData(0, ref origin)) { origin = new Point3d(0, 0, 0); }

            int sectionheight = 1;
            if (!DA.GetData(1, ref sectionheight)) { sectionheight = 1; }

            double low = double.NaN;
            if (!DA.GetData(2, ref low)) { low = 0; }

            double top = double.NaN;
            if (!DA.GetData(3, ref top)) { top = 15; }

            double[,,] vu, vv, vw;
            List<double[,,]> vel = new List<double[,,]> { };
            DA.GetDataList(4, vel);
            vu = vel[0];
            vv = vel[1];
            vw = vel[2];

            double[,,] p = new double[,,] { };
            DA.GetData(5, ref p);

            double hx = double.NaN;
            double hy = double.NaN;
            double hz = double.NaN;
            if (!DA.GetData(6, ref hx)) { return; }
            if (!DA.GetData(7, ref hy)) { return; }
            if (!DA.GetData(8, ref hz)) { return; }


            int colourSheme = 0;
            DA.GetData(9, ref colourSheme);


            //min max pressure values
            double minp = double.MaxValue;
            double maxp = double.MinValue;
            for (int i = 0; i < p.GetLength(0); i++)
            {
                for (int j = 0; j < p.GetLength(1); j++)
                {
                    for (int k = 0; k < p.GetLength(2); k++)
                    {
                        if (minp > p[i, j, k]) minp = p[i, j, k];
                        if (maxp < p[i, j, k]) maxp = p[i, j, k];
                    }
                }
            }


            Point3f[][] MVert = new Point3f[vu.GetLength(0)][];
            Color[][] Cols = new Color[vu.GetLength(0)][];
            int[][] index = new int[vu.GetLength(0)][];
            int counter = 0;
            double[,] output_vu = new double[1, 1];
            double[,] output_vv = new double[1, 1];
            double[,] output_vw = new double[1, 1];
            double[,] output_p = new double[1, 1];

            Mesh MshColSection = new Mesh();

            List<Point3d> velocityPoints = new List<Point3d>();
            List<double> values2d = new List<double>();
                

//z
            //8---9---10--11
            //| x | x | x |
            //4---5---6---7
            //| x | x | x |
            //0---1---2---3
            //

            output_vu = new double[vu.GetLength(0), vu.GetLength(1)];
            output_vv = new double[vu.GetLength(0), vu.GetLength(1)];
            output_vw = new double[vu.GetLength(0), vu.GetLength(1)];

            //- loop through and get colours for all vertices
            //- make vertices as point3d
            MVert = new Point3f[vu.GetLength(0)][];
            Cols = new Color[vu.GetLength(0)][];
            index = new int[vu.GetLength(0)][];
            counter = 0;


            for (int i = 0; i < vu.GetLength(0); i++)
            {
                MVert[i] = new Point3f[vv.GetLength(1)];
                Cols[i] = new Color[vv.GetLength(1)];
                index[i] = new int[vv.GetLength(1)];

                for (int j = 0; j < vv.GetLength(1); j++)
                {
                    //MVert[i][j] = new Point3f((float)(i * hx + origin[0]), (float)(j * hy + origin[1]), (float)(sectionheight * hz + origin[2]));
                        

                    Cols[i][j] = new Color();
                    index[i][j] = counter;
                    counter++;

                    double quantity = 0;

                        quantity = p[i, j, sectionheight];
                        //if (minp <= 0) quantity += Math.Abs(minp);
                        //low = 0;
                        //top = maxp + Math.Abs(minp);
                        //third = (top - low) / 5;
                        output_p[i, j] = quantity;

                        Line arrowlines = new Line(new Point3d(i * hx + origin[0], j * hy + origin[1], sectionheight * hz + origin[2]),
                                new Vector3d(vu[i, j, sectionheight], vv[i, j, sectionheight], vw[i, j, sectionheight]));
                        quantity = arrowlines.Length;
                        output_vu[i, j] = vu[i, j, sectionheight];
                        output_vv[i, j] = vv[i, j, sectionheight];
                        output_vw[i, j] = vw[i, j, sectionheight];


                MVert[i][j] = new Point3f((float)(i * hx + origin[0]), (float)(j * hy + origin[1]), (float)(quantity));
                MshColSection.Vertices.Add(MVert[i][j]);

                velocityPoints.Add(new Point3d(i * hx + origin[0], j * hy + origin[1], origin[2]));
                values2d.Add(quantity);


                Cols[i][j] = Utilities.GetRGB(colourSheme, quantity, top, low);
                    MshColSection.VertexColors.SetColor(index[i][j], Cols[i][j]);
                }
            }



            for (int i = 0; i < vu.GetLength(0) - 1; i++)
            {
                for (int j = 0; j < vv.GetLength(1) - 1; j++)
                {
                    MshColSection.Faces.AddFace(index[i][j], index[i + 1][j], index[i + 1][j + 1], index[i][j + 1]);
                }
            }


            DA.SetData(0, MshColSection);
            DA.SetDataList(1, velocityPoints);
            DA.SetDataList(2, values2d);




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
                return GHWind.Properties.Resources.visu_field;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("4b6847c9-9651-45d6-8875-9fa1fdcfc308"); }
        }
    }
}