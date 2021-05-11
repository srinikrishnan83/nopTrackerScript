using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.TrackerScript.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.TrackerScript.ActivationKey")]
        [Required]
        public string ActivationKey { get; set; }
        public bool ActivationKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.TrackerScript.TrackingScript")]
        public string TrackingScript { get; set; }
        public bool TrackingScript_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.TrackerScript.ConversionScript")]
        public string ConversionScript { get; set; }
        public bool ConversionScript_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.TrackerScript.RemarketingScript")]
        public string RemarketingScript { get; set; }
        public bool RemarketingScript_OverrideForStore { get; set; }

        public bool ValidKey { get; set; }
        public bool ValidKey_OverrideForStore { get; set; }



    }
}