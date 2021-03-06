# Asynchronous version!
I've played around with this tool to make it work asynchronously.


#Download plugin here (work in progress):

https://github.com/Sonderwoods/GH_Wind/Release/GHWind.gha
https://github.com/Sonderwoods/GH_Wind/Release/FastFluidSolverMT.dll



(Source files changed)

https://github.com/Sonderwoods/GH_Wind/tree/master/Tutorials/Asynchronous_test.gh
https://github.com/Sonderwoods/GH_Wind/GHWind/GHFFDSolver.cs
https://github.com/Sonderwoods/GH_Wind/GHWind/GHFFDSolverAsync.cs





All credit goes to GH_Wind by Christoph Waibel
https://github.com/christophwaibel/GH_Wind


# GH_Wind
Wind Simulation (FFD) plugin for Rhinoceros Grasshopper.

Please refer to this publication for citation: [Waibel et al. (2017)](http://www.ibpsa.org/proceedings/BS2017/BS2017_582.pdf)

<br><br>

As a user, just get [FastFluidSolverMT.dll](https://github.com/christophwaibel/GH_Wind/blob/master/GHWind/bin/FastFluidSolverMT.dll) and [GHWind.gha](https://github.com/christophwaibel/GH_Wind/blob/master/GHWind/bin/GHWind.gha) and put both into your Rhino Grasshopper components folder.

Try out the *.gh and *.3dm files in the [Tutorials](https://github.com/christophwaibel/GH_Wind/tree/master/Tutorials) folder as examples.

<br><br>

![alt text](https://github.com/christophwaibel/GH_Wind/blob/master/Documentation/slide0005_image017.gif "Image from Rhino")

*Capture from Rhino. Serves only as an illustrative example, don't make such a small domain in your simulations!*


![alt text](https://github.com/christophwaibel/GH_Wind/blob/master/Documentation/image23.gif "Image from Rhino")

*Capture from Rhino, 50x50x40 mesh, 10 times speed up*
