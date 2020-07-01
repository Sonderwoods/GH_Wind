using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;


/*
 * Utilities.cs
 * Copyright 2017 Christoph Waibel <chwaibel@student.ethz.ch>
 * 
 * This work is licensed under the GNU GPL license version 3.
*/

namespace GHWind
{
    public static class Utilities
    {
        /// <summary>
        /// Get a colour gradient.
        /// </summary>
        /// <param name="colourSheme">0: Blue(min) -> Red -> Yellow(max). 1: Blue(min) -> Turquoise -> Red(max). 2: Just black. </param>
        /// <param name="quantity">value to colour</param>
        /// <param name="top">Max value</param>
        /// <param name="low">Min value</param>
        /// <returns></returns>
        public static Color GetRGB(int colourSheme, double quantity, double top, double low)
        {
            double RR = 0.0;
            double GG = 0.0;
            double BB = 0.0;

            //top += Math.Abs(low);
            //double third = (top - low) / 5;
            //quantity += Math.Abs(low);
            //low = 0;
            quantity = (quantity - low) / (top - low);
            double third = 1.0 / 5.0;

            switch (colourSheme)
            {
                case 0:
                    quantity = (quantity - low) / (top - low);
                    if (quantity > third && quantity <=  2.0 * third)
                    {
                        RR = (quantity - third) * (255.0 / third);
                        GG = 0.0;
                        BB = 255 - ((quantity - third) * (255.0 / third));
                    }
                    else if (quantity > 2.0 * third)
                    {
                        RR = 255.0;
                        GG = (quantity - 2.0 * third) * (255.0 / third);
                        BB = 0.0;
                    }
                    else
                    {
                        RR = 0.0;
                        GG = 0.0;
                        BB = 255.0;
                    }
                    break;
                case 1:
                    quantity = (quantity - low) / (top - low);
                    third = 1.0 / 3.0;
                    if (quantity > third && quantity <= 2.0 * third)
                    {
                        RR = (quantity - third) * (255.0 / third);
                        GG = 255.0;
                        BB = 255.0 - ((quantity - third) * (255.0 / third));
                    }
                    else if (quantity > 2.0 * third)
                    {
                        RR = 255.0;
                        GG = 255.0 - ((quantity - 2.0 * third) * (255.0 / third));
                        BB = 0.0;
                    }
                    else
                    {
                        RR = 0.0;
                        GG = quantity * (255.0 / third);
                        BB = 255.0;
                    }
                    break;
                case 2:
                    //quantity = quantity;
                    RR = quantity*255.0/2.0;
                    GG = quantity * 255.0 / 2.0;
                    BB = quantity * 255.0 / 2.0;
                    break;
            }

            if (RR > 255) RR = 255;
            else if (RR < 0) RR = 0;
            if (GG > 255) GG = 255;
            else if (GG < 0) GG = 0;
            if (BB > 255) BB = 255;
            else if (BB < 0) BB = 0;
            return Color.FromArgb((int)RR, (int)GG, (int)BB);

        }

        
        public static List<double> GetThresholds(List<double>directions)
        {

            List<double> myThresholds = new List<double>();

            if (directions.Count == 0)
                throw new Exception("no content in list");

            List<double> myDirections = new List<double>(directions);

            myDirections.Add(myDirections[0] - 360);
            myDirections.Add(myDirections[0] + 360); // adding lowest + 360  in the end.. ie 375.
            myDirections.Sort();

            for (int i = 0; i < myDirections.Count - 1; i++)
            {
                myThresholds.Add(0.5 * (myDirections[i] + myDirections[i + 1]));
            }

            return myThresholds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="angle"></param>
        /// <param name="myThresholds">List of thresholds, obtained by Utilities.GetThresholds.</param>
        /// <param name="debug"></param>
        /// <returns></returns>
        public static int GetClosestDirection(double angle, List<double> myThresholds, bool debug = false)
        {

            while (true)
            {
                if (angle > 360.0)
                {
                    angle -= 360.0;
                }
                else
                {
                    break;
                }
            }

            for (int i = 0; i < myThresholds.Count-1; i++)
            {

                if (angle < myThresholds[1])
                {
                    if (debug) Rhino.RhinoApp.WriteLine($"x");
                    double distRight = Math.Abs(angle + 360 - myThresholds.Max());
                    double distLeft = Math.Abs(angle - myThresholds[0]);

                    if (debug) Rhino.RhinoApp.WriteLine($"left: {distLeft}  right: {distRight}");

                    if (angle + 360 > myThresholds[myThresholds.Count - 1])
                        return i;
                    else
                        return myThresholds.Count-2;
                }

                if (angle >= myThresholds[i] && angle < myThresholds[i+1])
                    return i;
            }
            return 0;

        }



                /// <summary>
                /// Writes a csv file to the specified path
                /// </summary>
                /// <param name="full_path">Full path including filename, e.g. 'C:\results.csv'</param>
                /// <param name="output_data">output data</param>
                public static void WriteCSV(string full_path, double [,] output_data)
        {
            string[] lines;
            var list = new List<string>();

            int rows = output_data.GetLength(0);
            int columns = output_data.GetLength(1);

            for (int i=0; i<rows; i++)
            {
                string line = output_data[i, 0].ToString();
                for(int u=1; u<columns; u++)
                {
                    line = String.Concat(line, "," + output_data[i, u].ToString());
                }
                list.Add(line);
            }
            lines = list.ToArray();
            File.WriteAllLines(full_path, lines);
        }

    }
}
