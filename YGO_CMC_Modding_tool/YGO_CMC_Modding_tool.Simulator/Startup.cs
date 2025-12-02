using System;
using OpenSilver.Simulator;

namespace YGO_CMC_Modding_tool.Simulator
{
    internal static class Startup
    {
        [STAThread]
        static int Main(string[] args)
        {
            return SimulatorLauncher.Start(typeof(App));
        }
    }
}
