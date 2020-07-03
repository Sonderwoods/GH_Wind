using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHWind
{
    public class GHDominantWinds : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GHAnnualStatsNew class.
        /// </summary>
        public GHDominantWinds()
         : base("Dominant Winds", "DomWinds",
             "Find predominant wind directions",
             "GreenScenario", "01 | Toolkit")
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
            pManager.AddNumberParameter("No wind dirs", "No wind dirs", "int,  no of wind directions. Typically 8, 12 or 16.", GH_ParamAccess.item);

            //3
            pManager.AddNumberParameter("Threshold", "Threshold", "number between 0.0 and 5.0 to fine tune how many ", GH_ParamAccess.item);


            //3
            //pManager.AddBooleanParameter("debug", "debug", "debug", GH_ParamAccess.item, false);
            //pManager[3].Optional = true;
            
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            
            //0
            pManager.AddNumberParameter("All Directions", "All Dirs", "x", GH_ParamAccess.list);

            //1
            pManager.AddNumberParameter("All Speed per Direction", "All SPD", "x", GH_ParamAccess.tree);


            //2
            pManager.AddNumberParameter("Selected Directions", "SelDirs", "x", GH_ParamAccess.list);

            //3
            pManager.AddNumberParameter("Selected Speed per Direction", "Sel SPD", "x", GH_ParamAccess.tree);

            //4
            pManager.AddNumberParameter("rounded directions", "rounded directions", "x", GH_ParamAccess.list);




        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {



            List<double> inWindVelocities = new List<double>();
            DA.GetDataList(0, inWindVelocities);

            List<double> inWindDirections = new List<double>();
            DA.GetDataList(1, inWindDirections);

            double inNoWindDirections = 0.0;


            DA.GetData(2, ref inNoWindDirections);
            
            int noWindDirections = (int)Math.Round(inNoWindDirections);

            bool debug = false;
            //DA.GetData(3, ref debug);

            double threshold = 1;
            DA.GetData(3, ref threshold);

            int noHours = inWindVelocities.Count;
            
            


            // each hour * speed . To find "most dominant" directions.
            List<double> allAccumulatedHourSpeeds = new List<double>(new double[noWindDirections]);

            

            // at 16 dirs, each angle is 22.5   this means +/-11.25° to each side.
            double angleTol = 360.0 / noWindDirections / 2.0;

            // list of doubles containing the picked directions
            List<double> outAllDirections = new List<double>();
            List<double> outSelDirections = new List<double>();

            //setting default list [  0, 45, 90, etc..]
            for (int i = 0; i < noWindDirections; i++)
            {
                outAllDirections.Add(360.0 * ((double)i / noWindDirections));
            }

            //if (debug) Rhino.RhinoApp.WriteLine($"{outAllDirections.Count}");

            // speeds per direction. will be sorted decreaasingly
            List<List<double>> outWindVelocitiesPerDirection = new List<List<double>>(noWindDirections);

            GH_Structure<GH_Number> outAllSpeedsPerDirection = new GH_Structure<GH_Number>();
            GH_Structure<GH_Number> outSelSpeedsPerDirection = new GH_Structure<GH_Number>();

            List<double> outAllRoundedDirections = new List<double>();

            //if (debug) Rhino.RhinoApp.WriteLine($"winddir 001");

            //if (debug)
            //{
            //    for (int i = 0; i < outAllDirections.Count; i++)
            //    {
            //        Rhino.RhinoApp.WriteLine($"- {outAllDirections[i]}");
            //    }
            //}


            // empty lists
            for (int i = 0; i < noWindDirections; i++) 
            {
                outWindVelocitiesPerDirection.Add(new List<double>());

            }

            List<double> thresholds = Utilities.GetThresholds(outAllDirections);

            for (int h = 0; h < noHours; h++)
            {
                int thisWindDir = Utilities.GetClosestDirection(inWindDirections[h], thresholds);
                if (debug && h > 300 && h < 310) Rhino.RhinoApp.WriteLine($"thisWindDir =  {inWindDirections[h]} Rounded to  {outAllDirections[thisWindDir]}");
                // distribute velocities per direction
                outWindVelocitiesPerDirection[thisWindDir].Add(inWindVelocities[h]);
                outAllRoundedDirections.Add(outAllDirections[thisWindDir]);
                allAccumulatedHourSpeeds[thisWindDir] += inWindVelocities[h];

            }

            //if (debug)
            //{
            //    Rhino.RhinoApp.WriteLine($"\nRaw data:");

            //    for (int i = 0; i < noWindDirections; i++)
            //    {
            //        Rhino.RhinoApp.WriteLine($"[{outAllDirections[i]}]  Hours * Average = {allAccumulatedHourSpeeds[i]:0.0},      ({allAccumulatedHourSpeeds[i]/ allAccumulatedHourSpeeds.Sum()*100.0:0.0}%)");

            //    }

            //    Rhino.RhinoApp.WriteLine($"\n");
            //}




            //if (debug)
            //{
            //    Rhino.RhinoApp.WriteLine($"\nMost relevant directions:");

            //    for (int i = 0; i < noWindDirections; i++)
            //    {
            //        if (allAccumulatedHourSpeeds[i] > allAccumulatedHourSpeeds.Sum()/noWindDirections)
            //            if (debug) Rhino.RhinoApp.WriteLine($"[{outAllDirections[i]}] Rounded dir: {outAllRoundedDirections[i]},    Hours * Average = {allAccumulatedHourSpeeds[i]:0.0},      ({allAccumulatedHourSpeeds[i] / allAccumulatedHourSpeeds.Sum() * 100.0:0.0}%)");

            //    }

            //    Rhino.RhinoApp.WriteLine($"\n");
            //}




            // Exporting unsorted data:
            if (debug) Rhino.RhinoApp.WriteLine($"\n\nAll directions:");
            List<GH_Number> ghVelocitiesThisDirection = new List<GH_Number>();

            for (int i = 0; i < noWindDirections; i++)
            {
                ghVelocitiesThisDirection = new List<GH_Number>();
                for (int j = 0; j < outWindVelocitiesPerDirection[i].Count; j++)
                    ghVelocitiesThisDirection.Add(new GH_Number(outWindVelocitiesPerDirection[i][j]));

                //outAllDirections.Add(inWindDirections[i]);
                outAllSpeedsPerDirection.AppendRange(ghVelocitiesThisDirection, new GH_Path(i));

                if (debug) Rhino.RhinoApp.WriteLine($"[{outAllDirections[i]}] Hours: {outWindVelocitiesPerDirection[i].Count},       Hours * Average = {allAccumulatedHourSpeeds[i]:0.0},      ({allAccumulatedHourSpeeds[i] / allAccumulatedHourSpeeds.Sum() * 100.0:0.0}%)");
            }


            //exporting sorted data:
            int countOut = 0;
            if (debug) Rhino.RhinoApp.WriteLine($"\n\nMost relevant directions:");
            for (int i = 0; i < noWindDirections; i++)
            {
                ghVelocitiesThisDirection = new List<GH_Number>();
                if (allAccumulatedHourSpeeds[i] > allAccumulatedHourSpeeds.Sum() / noWindDirections / threshold)
                {
                    for (int j = 0; j < outWindVelocitiesPerDirection[i].Count; j++)
                        ghVelocitiesThisDirection.Add(new GH_Number(outWindVelocitiesPerDirection[i][j]));

                    outSelDirections.Add(outAllDirections[i]);
                    outSelSpeedsPerDirection.AppendRange(ghVelocitiesThisDirection, new GH_Path(countOut));

                    countOut++;

                    if (debug) Rhino.RhinoApp.WriteLine($"[{outAllDirections[i]}] Hours: {outWindVelocitiesPerDirection[i].Count},       Hours * Average = {allAccumulatedHourSpeeds[i]:0.0},      ({allAccumulatedHourSpeeds[i] / allAccumulatedHourSpeeds.Sum() * 100.0:0.0}%)");

                }

            }





            //// MUCH FUSS OF JUST SORTING TWO LISTS TOGETHER LOL.
            //List <AccumulatedHourAndWindDirection> accHourWindDirPairs = new List<AccumulatedHourAndWindDirection>();
            //for (int i = 0; i < allAccumulatedHourSpeeds.Count; i++)
            //    accHourWindDirPairs.Add(new AccumulatedHourAndWindDirection(allAccumulatedHourSpeeds[i], outAllDirections[i], outWindVelocitiesPerDirection[i]));

            //accHourWindDirPairs.Sort((data1, data2) => data1.AccumulatedHours.CompareTo(data2.AccumulatedHours));

            //List<double> sortedDirections = new List<double>();
            //List<List<double>> outSortedWindVelocitiesPerDirection = new List<List<double>>();
            //List<double> sortedAccumulatedHourSpeeds = new List<double>();


            ////if (debug) Rhino.RhinoApp.WriteLine($"accPairs {accHourWindDirPairs.Count}");


            //for (int i = 0; i < accHourWindDirPairs.Count; i++)
            //{
            //    sortedAccumulatedHourSpeeds.Add(accHourWindDirPairs[i].AccumulatedHours);
                
            //    sortedDirections.Add(accHourWindDirPairs[i].WindDirection);
                
            //    outSortedWindVelocitiesPerDirection.Add(new List<double>(accHourWindDirPairs[i].Velocities));
            //}

            //allAccumulatedHourSpeeds.Reverse();
            //sortedDirections.Reverse();
            //outSortedWindVelocitiesPerDirection.Reverse();







            //List<GH_Number> ghVelocitiesThisDirection = new List<GH_Number>();

            //if (debug) Rhino.RhinoApp.WriteLine($"All {noWindDirections} directions:");

            //for (int i = 0; i < noWindDirections; i++)
            //{
            //    if (debug) Rhino.RhinoApp.WriteLine($"[{sortedDirections[i]}] --> {allAccumulatedHourSpeeds[i]} hourspeeds added... no of entries: {outSortedWindVelocitiesPerDirection[i].Count}");
                
            //}








            //    for (int i = 0; i < noWindDirections; i++)
            //{
            //    List<double> velocitiesThisDirection = new List<double>(outWindVelocitiesPerDirection[i]);


                


            //    if (allAccumulatedHourSpeeds[i] > allAccumulatedHourSpeeds.Sum() * 0.05)
            //    {
            //        if (debug) Rhino.RhinoApp.WriteLine($"[{sortedDirections[i]}] --> {allAccumulatedHourSpeeds[i]} hourspeeds added");

            //        for (int j = 0; j < velocitiesThisDirection.Count; j++)
            //            ghVelocitiesThisDirection.Add(new GH_Number(velocitiesThisDirection[j]));


            //        outSelDirections.Add(sortedDirections[i]);
            //        outSelSpeedsPerDirection.AppendRange(ghVelocitiesThisDirection, new GH_Path(outSelSpeedsPerDirection.PathCount));
            //    }

            //    outAllDirections.Add(inWindDirections[i]);
            //    outAllSpeedsPerDirection.AppendRange(ghVelocitiesThisDirection, new GH_Path(i));
            //}


            












            //for (int i = 0; i < sortedDirections.Count; i++)
            //{
            //    if (debug) Rhino.RhinoApp.WriteLine($"{sortedDirections[i]}");
            //}


            

            //// adding accumulated hour/speeds to each direction
            //for (int i = 0; i < noWindDirections; i++)
            //{

            //    //if (debug) Rhino.RhinoApp.WriteLine($"dimwinds 002a");

            //    List<double> velocitiesThisDirection = new List<double>(outWindVelocitiesPerDirection[i]);


            //    velocitiesThisDirection.Sort();
            //    velocitiesThisDirection.Reverse(); // largest first

            //    for (int j = 0; j < velocitiesThisDirection.Count; j++)
            //    {
                    
            //        ghVelocitiesThisDirection.Add(new GH_Number(velocitiesThisDirection[j]));
            //        //if (debug) Rhino.RhinoApp.WriteLine($"dimwinds 002d{accumulatedHourSpeeds.Count} .. {velocitiesThisDirection.Count}");
            //        allAccumulatedHourSpeeds[i] += velocitiesThisDirection[j];
            //    }

            //    if (debug) Rhino.RhinoApp.WriteLine($"[dir {i}] velocitiescount {outWindVelocitiesPerDirection[i].Count} .. accspeeds :   {allAccumulatedHourSpeeds[i]:0.0}");
            //}



            //if(debug) Rhino.RhinoApp.WriteLine($"dimwinds 003");


            //// sorting the accumulated hour/speeds and adding to gh_outputs
            //for (int i = 0; i < noWindDirections; i++)
            //{
            //    //if (debug) Rhino.RhinoApp.WriteLine($"dimwinds 003a");
            //    if (allAccumulatedHourSpeeds[i] > allAccumulatedHourSpeeds.Sum() * 0.05)
            //    {
            //        outSelDirections.Add(inWindDirections[i]);
            //        outSelSpeedsPerDirection.AppendRange(ghVelocitiesThisDirection, new GH_Path(outSelSpeedsPerDirection.PathCount));

            //    }

            //    outAllDirections.Add(inWindDirections[i]);
            //    outAllSpeedsPerDirection.AppendRange(ghVelocitiesThisDirection, new GH_Path(i));
            //}

            //if (debug) Rhino.RhinoApp.WriteLine($"dimwinds 004");



            DA.SetDataList(0, outAllDirections);
            DA.SetDataTree(1, outAllSpeedsPerDirection);

            DA.SetDataList(2, outSelDirections);
            DA.SetDataTree(3, outSelSpeedsPerDirection);

            DA.SetDataList(4, outAllRoundedDirections);


            return;
  

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
            get { return new Guid("3e07eede-5406-434b-be5a-effa799600a4"); }
        }

        [System.Serializable] //This allows you to modify objects of this class in the inspector
        public class AccumulatedHourAndWindDirection
        {

            public double AccumulatedHours;
            public double WindDirection;
            public List<double> Velocities;
            
            public AccumulatedHourAndWindDirection(double accumulatedHour, double windDir, List<double> velocities)
            {
                this.AccumulatedHours = accumulatedHour;
                this.WindDirection = windDir;
                this.Velocities = velocities;
            }
        }
    }
}