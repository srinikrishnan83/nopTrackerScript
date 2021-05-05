using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.TrackerScript.Models
{
    public record PublicInfoModel : BaseNopModel
    {
        public string ActivationKey { get; set; }
        public string TrackingScript { get; set; }
        public string ConversionScript { get; set; }
        public string RemarketingScript { get; set; }
        public string ValidKey { get; set; }


    }
}
