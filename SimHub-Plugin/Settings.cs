using System;

namespace InternalWindMachinePlugin
{
    public class InternalWindMachineSettings
    {
        public bool Use3DWind { get; set; } = false;
        public bool EnableCenter { get; set; } = true;
        public bool EnableLeft { get; set; } = false;
        public bool EnableRight { get; set; } = false;

        public string PropCenter { get; set; } = "ShakeItWindPlugin.OutputCenter";
        public string PropLeft { get; set; } = "ShakeItWindPlugin.OutputLeft";
        public string PropRight { get; set; } = "ShakeItWindPlugin.OutputRight";

        public string SensorDirectory { get; set; } = @"InternalWindMachineOutput";

        // Individual Fan Power Overrides
        public bool OverL { get; set; } = false;
        public bool OverC { get; set; } = false;
        public bool OverR { get; set; } = false;

        public double PowerL { get; set; } = 100.0;
        public double PowerC { get; set; } = 100.0;
        public double PowerR { get; set; } = 100.0;
    }
}
