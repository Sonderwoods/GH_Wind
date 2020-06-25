using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace GHWind
{
    internal class DiscretizedGeometry
    {

        public List<double[]> myListOfCubes { get; set; }
        public DiscretizedGeometry(List<double[]> geometry)
        {
            myListOfCubes = geometry;

        }

        public DiscretizedGeometry()
        {

        }

    }
}
