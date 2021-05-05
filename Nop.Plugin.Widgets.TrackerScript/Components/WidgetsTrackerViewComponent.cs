using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Plugin.Widgets.TrackerScript.Extension;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Tax;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.TrackerScript.Components
{
    [ViewComponent(Name = "WidgetsTracker")]
    public class WidgetsTrackerViewComponent : NopViewComponent
    {
        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IWorkContext _workContext;
        private readonly IOrderService _orderService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly CultureInfo _usCulture;
        private readonly IProductService _productService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly ITaxService _taxService;
        private readonly ICurrencyService _currencyService;
        private readonly IPermissionService _permissionService;
        private readonly IAddressService _addressService;



        public WidgetsTrackerViewComponent(IStoreContext storeContext, ISettingService settingService, IProductService productService, IWorkContext workContext, IOrderService orderService, IOrderTotalCalculationService orderTotalCalculationService, IPriceCalculationService priceCalculationService, IPermissionService permissionService,
            ITaxService taxService,
            ICurrencyService currencyService,
            IAddressService addressService)
        {
            _storeContext = storeContext;
            _settingService = settingService;
            _workContext = workContext;
            _orderService = orderService;
            _usCulture = new CultureInfo("en-US");
            _productService = productService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _permissionService = permissionService;
            _priceCalculationService = priceCalculationService;
            _taxService = taxService;
            _currencyService = currencyService;
            _addressService = addressService;
        }
        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var googleAnalyticsPageViewTrackerSettings = await _settingService.LoadSettingAsync<TrackerSettings>(store.Id);
            if (googleAnalyticsPageViewTrackerSettings.ValidKey == false)
            {
                return Content("");
            }
            string globalScript = "";
            if (widgetZone.Equals("body_end_html_tag_before", StringComparison.InvariantCultureIgnoreCase))
            {
                var routeData = Url.ActionContext.RouteData;
                var controller = routeData.Values["controller"].ToString().ToLowerInvariant();
                var action = routeData.Values["action"].ToString().ToLowerInvariant();

                switch (controller)
                {
                    case "product":
                        if (action.Equals("productdetails", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //current product ID
                            var currentProductIdStr = routeData.Values["productId"].ToString();
                            var script = await GetRemarketingScript("product", currentProductIdStr, null, null); ;
                            globalScript += script;
                        }
                        break;

                    case "checkout":
                        if (action.Equals("completed", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //We are in last step of checkout, we can use order total for conversion value                            
                            var customer = await _workContext.GetCurrentCustomerAsync();
                            var lastOrder = await _orderService.SearchOrdersAsync(storeId: store.Id, customerId: customer.Id, pageSize: 1);
                            var script = await GetConversionScript(lastOrder.FirstOrDefault());
                            globalScript += script;
                        }
                        break;
                }
            }

            if (widgetZone.Equals("head_html_tag", StringComparison.InvariantCultureIgnoreCase))
            {
                var analyticsTrackingScript = googleAnalyticsPageViewTrackerSettings.TrackingScript + "\n";
                if (analyticsTrackingScript.Length > 0)
                    globalScript += analyticsTrackingScript.ToString();
            }
            if (globalScript.Length > 0)
                return View("~/Plugins/Widgets.TrackerScript/Views/PublicInfo.cshtml", globalScript);
            else
                return Content("");
        }

        private async Task<string> GetRemarketingScript(string pageType, string productId, string categoryName, IList<ShoppingCartItem> cart)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            TrackerSettings googleAnalyticsPageViewTrackerSettings = await _settingService.LoadSettingAsync<TrackerSettings>(store.Id);
            var script = await InjectValuesInScript(googleAnalyticsPageViewTrackerSettings.RemarketingScript, pageType, productId, categoryName, null, cart);
            return script;
        }
        private async Task<string> InjectValuesInScript(string script, string pageType, string productId, string categoryName, Order order, IList<ShoppingCartItem> cart)
        {
			//Set default or empty values
			var totalFormated = ToScriptFormat(0);
            var prodIdsFormated = productId;
            var country = "";
            var datetime = "";
            var email = "";
            var orderId = "";
            var orderTotal = "";
            var currency = "";
            var gtin = "";
            if (productId != null)
            {
                var id = int.Parse(productId);
                var product = await _productService.GetProductByIdAsync(id);
                prodIdsFormated = await _productService.FormatSkuAsync(product, null);
                var price = product.PreparePrice(_workContext, _storeContext, _productService, _priceCalculationService, _permissionService, _taxService, _currencyService);
                decimal value = 0;
                try
                {
                    value = Convert.ToDecimal(price);
                }
                catch (Exception)
                {
                    value = 0;
                }
                totalFormated = ToScriptFormat(value);
            }
            //In case we have an order
            if (order != null)
            {
                totalFormated = ToScriptFormat(order.OrderTotal);
                var orderitems =await _orderService.GetOrderItemsAsync(order.Id);

                prodIdsFormated = ToScriptFormat((from c in orderitems select (_productService.GetProductByIdAsync(c.ProductId).Result.Sku??"")).ToArray());

                var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
                country = billingAddress.County;
                datetime = order.CreatedOnUtc.ToString();
                datetime = Convert.ToDateTime(datetime).AddDays(14).ToString("yyyy-MM-dd");
                email = billingAddress.Email;
                orderId = order.Id.ToString();
                orderTotal = order.OrderTotal.ToString();
                currency = order.CustomerCurrencyCode.ToString();
                var gtins = new List<string>();
                foreach (var c in orderitems)
                {
                    var products = await _productService.GetProductByIdAsync(c.ProductId);
                    if (products.Gtin != null)
                    {
                        gtins.Add("\"{gtin}\":\"" + products.Gtin + "\"");

                    }
                }
                gtin = "[" + string.Join(",", gtins.ToArray()) + "]";
            }

            //In case we have a cart
            if (cart != null)
            {
                decimal subTotalWithoutDiscountBase = decimal.Zero;
                if (cart.Count > 0)
                {
                    var subTotalIncludingTax =await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax;
                    
                    await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, subTotalIncludingTax);
                    
                    totalFormated = ToScriptFormat(subTotalWithoutDiscountBase);
                    prodIdsFormated = ToScriptFormat((from c in cart select (_productService.GetProductByIdAsync(c.ProductId).Result.Sku??"")).ToArray());
                }
            }
            script = script.Replace("{PAGETYPE}", pageType);
            script = script.Replace("{PRODID}", prodIdsFormated);
            script = script.Replace("{CATEGORYNAME}", categoryName);
            script = script.Replace("{VALUE}", totalFormated);
          
            script = script.Replace("{ORDER_ID}", orderId);
            script = script.Replace("{CUSTOMER_EMAIL}", email);
            script = script.Replace("{COUNTRY_CODE}", country);
            script = script.Replace("{GTIN}", gtin);
            script = script.Replace("{YYYY-MM-DD}", datetime);

            script = script.Replace("{ORDERTOTAL}", orderTotal);
            script = script.Replace("{CURRENCY}", currency);

            //Remove optional and empty values
            script = script.Replace("\r\n        ecomm_category: '',", "");
            script = script.Replace("\r\n        ecomm_prodid: ,", "");
            script = script.Replace("\r\n        ecomm_totalvalue: 0.00", "");


            if (HttpContext.Request.Scheme == "https")
                script = script.Replace("http://", "https://");
            return script + "\n";
        }

		
        private async Task<string> GetConversionScript(Order order)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            TrackerSettings googleAnalyticsPageViewTrackerSettings = await _settingService.LoadSettingAsync<TrackerSettings>(store.Id);
            var script = await InjectValuesInScript(googleAnalyticsPageViewTrackerSettings.ConversionScript, "purchase", null, null, order, null);
            return script;
        }
        private string ToScriptFormat(decimal value)
        {
            return value.ToString("0.00", _usCulture);
        }
        private string ToScriptFormat(string[] strings)
        {
            if (strings == null || strings.Length == 0)
            {
                return "''";
            }
            else
            {
                StringBuilder strBuilder = new StringBuilder(strings.Length * 3 + 5);
                strBuilder.Append(strings[0]);
                if (strings.Length > 1)
                {
                    strBuilder.Insert(0, "[");
                    for (int i = 1; i < strings.Length; i++)
                    {
                        strBuilder.Append(",");
                        strBuilder.Append(strings[i]);
                    }
                    strBuilder.Append("]");
                }
                return strBuilder.ToString();
            }
        }
        private string RemoveLastComma(string value)
        {
            int index = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == ',')
                    index = i;
            }
            if (index > 0)
            {
                value = value.Remove(index, 1);
            }

            return value;
        }

    }
}
