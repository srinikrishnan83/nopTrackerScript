using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.Widgets.TrackerScript.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Net.Http;

namespace Nop.Plugin.Widgets.TrackerScript.Controllers
{
    [Area(AreaNames.Admin)]
    public class WidgetsTrackerController : BasePluginController
    {
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly INotificationService _notificationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WidgetsTrackerController> _logger;

        public WidgetsTrackerController( IPermissionService permissionService, 
            ISettingService settingService, 
            IHttpClientFactory httpClientFactory, 
            ILogger<WidgetsTrackerController> logger,
            ILocalizationService localizationService, 
            IStoreContext storeContext, 
            INotificationService notificationService)
        {
            _permissionService = permissionService;
            _settingService = settingService;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _notificationService = notificationService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }
        [AuthorizeAdmin]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageWidgets))
                return AccessDeniedView();

            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var GoogleAnalyticsPageViewTrackerSettings = _settingService.LoadSetting<TrackerSettings>(storeScope);

            var model = new ConfigurationModel
            {
                ActivationKey = GoogleAnalyticsPageViewTrackerSettings.ActivationKey,
                TrackingScript = GoogleAnalyticsPageViewTrackerSettings.TrackingScript,
                ConversionScript = GoogleAnalyticsPageViewTrackerSettings.ConversionScript,
                RemarketingScript = GoogleAnalyticsPageViewTrackerSettings.RemarketingScript,
            };

            if (storeScope > 0)
            {
                model.ActivationKey_OverrideForStore = _settingService.SettingExists(GoogleAnalyticsPageViewTrackerSettings, x => x.ActivationKey, storeScope);
                model.TrackingScript_OverrideForStore = _settingService.SettingExists(GoogleAnalyticsPageViewTrackerSettings, x => x.TrackingScript, storeScope);
                model.ConversionScript_OverrideForStore = _settingService.SettingExists(GoogleAnalyticsPageViewTrackerSettings, x => x.ConversionScript, storeScope);
                model.RemarketingScript_OverrideForStore = _settingService.SettingExists(GoogleAnalyticsPageViewTrackerSettings, x => x.RemarketingScript, storeScope);
            }

            return View("~/Plugins/Widgets.TrackerScript/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        public IActionResult Configure(ConfigurationModel model)
        {
            string hostNamewithScheme = string.Format("{0}", Url.ActionContext.HttpContext.Request.Host);
            string url = "https://signupapi.aroopatech.com/api/PluginValidator/DataValidation?ProductDetails=";
            url += "{'userDomain':'" + hostNamewithScheme + "', 'randomId':'" + model.ActivationKey + "','ProductName':'plugintr43' }";

            bool value = false;
            try
            {
                var httpClient = _httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
                var response =  httpClient.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    var data =  response.Content.ReadAsStringAsync().Result;
                    value = Convert.ToBoolean(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Tracker Script Plugin Activation Error.", ex);
            }
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageWidgets))
                return AccessDeniedView();

            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var GoogleAnalyticsPageViewTrackerSettings = _settingService.LoadSetting<TrackerSettings>(storeScope);
            if (value)
            {
                model.ValidKey = true;
                GoogleAnalyticsPageViewTrackerSettings.ActivationKey = model.ActivationKey;
                GoogleAnalyticsPageViewTrackerSettings.TrackingScript = model.TrackingScript;
                GoogleAnalyticsPageViewTrackerSettings.ConversionScript = model.ConversionScript;
                GoogleAnalyticsPageViewTrackerSettings.RemarketingScript = model.RemarketingScript;
                GoogleAnalyticsPageViewTrackerSettings.ValidKey = model.ValidKey;


                _settingService.SaveSettingOverridablePerStore(GoogleAnalyticsPageViewTrackerSettings, x => x.ActivationKey, model.ActivationKey_OverrideForStore, storeScope, false);
                _settingService.SaveSettingOverridablePerStore(GoogleAnalyticsPageViewTrackerSettings, x => x.TrackingScript, model.TrackingScript_OverrideForStore, storeScope, false);
                _settingService.SaveSettingOverridablePerStore(GoogleAnalyticsPageViewTrackerSettings, x => x.ConversionScript, model.ConversionScript_OverrideForStore, storeScope, false);
                _settingService.SaveSettingOverridablePerStore(GoogleAnalyticsPageViewTrackerSettings, x => x.RemarketingScript, model.RemarketingScript_OverrideForStore, storeScope, false);
                _settingService.SaveSettingOverridablePerStore(GoogleAnalyticsPageViewTrackerSettings, x => x.ValidKey, model.ValidKey_OverrideForStore, storeScope, false);
                _settingService.ClearCache();
                _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
                return Configure();
            }
            else
            {
                model.ValidKey = false;
                GoogleAnalyticsPageViewTrackerSettings.ValidKey = model.ValidKey;
                _settingService.SaveSettingOverridablePerStore(GoogleAnalyticsPageViewTrackerSettings, x => x.ValidKey, model.ValidKey_OverrideForStore, storeScope, false);
                _settingService.ClearCache();
                _notificationService.ErrorNotification("Please Enter Valid Activation Key");
                return Configure();
            }
        }
    }
}
