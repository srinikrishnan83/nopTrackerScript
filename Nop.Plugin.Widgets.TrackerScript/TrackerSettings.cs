using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.TrackerScript
{
    public class TrackerSettings : ISettings
    {
        public string ActivationKey { get; set; }
        public string TrackingScript { get; set; }
        public string ConversionScript { get; set; }
        public string RemarketingScript { get; set; }
        public bool ValidKey { get; set; }
    }
}
