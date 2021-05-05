using System;
using System.Net.Http;
using System.Threading.Tasks;
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


        public WidgetsTrackerController(
            IPermissionService permissionService, ISettingService settingService,
            ILocalizationService localizationService, IStoreContext storeContext, 
            INotificationService notificationService,
            IHttpClientFactory httpClientFactory,
            ILogger<WidgetsTrackerController> logger)
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
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageWidgets))
                return AccessDeniedView();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var googleAnalyticsPageViewTrackerSettings = await _settingService.LoadSettingAsync<TrackerSettings>(storeScope);

            var model = new ConfigurationModel
            {
                ActivationKey = googleAnalyticsPageViewTrackerSettings.ActivationKey,
                TrackingScript = googleAnalyticsPageViewTrackerSettings.TrackingScript,
                ConversionScript = googleAnalyticsPageViewTrackerSettings.ConversionScript,
                RemarketingScript = googleAnalyticsPageViewTrackerSettings.RemarketingScript,
            };

            if (storeScope > 0)
            {
                model.ActivationKey_OverrideForStore =await _settingService.SettingExistsAsync(googleAnalyticsPageViewTrackerSettings, x => x.ActivationKey, storeScope);
                model.TrackingScript_OverrideForStore = await _settingService.SettingExistsAsync(googleAnalyticsPageViewTrackerSettings, x => x.TrackingScript, storeScope);
                model.ConversionScript_OverrideForStore = await _settingService.SettingExistsAsync(googleAnalyticsPageViewTrackerSettings, x => x.ConversionScript, storeScope);
                model.RemarketingScript_OverrideForStore = await _settingService.SettingExistsAsync(googleAnalyticsPageViewTrackerSettings, x => x.RemarketingScript, storeScope);
            }

            return View("~/Plugins/Widgets.TrackerScript/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            string hostNamewithScheme = string.Format("{0}", Url.ActionContext.HttpContext.Request.Host);
            string url = "https://signupapi.aroopatech.com/api/PluginValidator/DataValidation?ProductDetails=";
            url += "{'userDomain':'" + hostNamewithScheme + "', 'randomId':'" + model.ActivationKey + "','ProductName':'plugintr44' }";

            bool value = false;
            try
            {
                var httpClient = _httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    value = Convert.ToBoolean(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Tracker Script Plugin Activation Error.", ex);
            }

            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageWidgets))
                return AccessDeniedView();

            var storeScope =await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var googleAnalyticsPageViewTrackerSettings = await _settingService.LoadSettingAsync<TrackerSettings>(storeScope);
            if (value)
            {
                model.ValidKey = true;
                googleAnalyticsPageViewTrackerSettings.ActivationKey = model.ActivationKey;
                googleAnalyticsPageViewTrackerSettings.TrackingScript = model.TrackingScript;
                googleAnalyticsPageViewTrackerSettings.ConversionScript = model.ConversionScript;
                googleAnalyticsPageViewTrackerSettings.RemarketingScript = model.RemarketingScript;
                googleAnalyticsPageViewTrackerSettings.ValidKey = model.ValidKey;


                await _settingService.SaveSettingOverridablePerStoreAsync(googleAnalyticsPageViewTrackerSettings, x => x.ActivationKey, model.ActivationKey_OverrideForStore, storeScope, false);
                await _settingService.SaveSettingOverridablePerStoreAsync(googleAnalyticsPageViewTrackerSettings, x => x.TrackingScript, model.TrackingScript_OverrideForStore, storeScope, false);
                await _settingService.SaveSettingOverridablePerStoreAsync(googleAnalyticsPageViewTrackerSettings, x => x.ConversionScript, model.ConversionScript_OverrideForStore, storeScope, false);
                await _settingService.SaveSettingOverridablePerStoreAsync(googleAnalyticsPageViewTrackerSettings, x => x.RemarketingScript, model.RemarketingScript_OverrideForStore, storeScope, false);
                await _settingService.SaveSettingOverridablePerStoreAsync(googleAnalyticsPageViewTrackerSettings, x => x.ValidKey, model.ValidKey_OverrideForStore, storeScope, false);
                await _settingService.ClearCacheAsync();
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
                return await Configure();
            }
            else
            {
                model.ValidKey = false;
                googleAnalyticsPageViewTrackerSettings.ValidKey = model.ValidKey;
                await _settingService.SaveSettingOverridablePerStoreAsync(googleAnalyticsPageViewTrackerSettings, x => x.ValidKey, model.ValidKey_OverrideForStore, storeScope, false);
                await _settingService.ClearCacheAsync();
                _notificationService.ErrorNotification("Please Enter Valid Activation Key");
                return await Configure();
            }
        }

    }
}
