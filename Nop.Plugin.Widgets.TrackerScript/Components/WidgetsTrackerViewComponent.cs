using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Discounts;
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Nop.Plugin.Widgets.TrackerScript.Components
{
    [ViewComponent(Name = "WidgetsTracker")]
    public class WidgetsTrackerViewComponent : NopViewComponent
    {
        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IWorkContext _workContext;
        private readonly IOrderService _orderService;
        private readonly IPermissionService _permissionService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly CultureInfo _usCulture;
        private readonly IProductService _productService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly ITaxService _taxService;
        private readonly ICurrencyService _currencyService;
        private readonly IAddressService _addressService;


        public WidgetsTrackerViewComponent(IStoreContext storeContext, 
            ISettingService settingService, 
            IProductService productService, 
            IWorkContext workContext, 
            IOrderService orderService, 
            IPermissionService permissionService, 
            IOrderTotalCalculationService orderTotalCalculationService, 
            IPriceCalculationService priceCalculationService,
            ITaxService taxService, 
            IAddressService addressService,
            ICurrencyService currencyService)
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
            _addressService = addressService;
            _currencyService = currencyService;
        }
        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            var GoogleAnalyticsPageViewTrackerSettings = _settingService.LoadSetting<TrackerSettings>(_storeContext.CurrentStore.Id);
            if (GoogleAnalyticsPageViewTrackerSettings.ValidKey == false)
            {
                return Content("");
            }
            string globalScript = "";
            if (widgetZone.Equals("body_end_html_tag_before", StringComparison.InvariantCultureIgnoreCase))
            {
                var routeData = Url.ActionContext.RouteData;
                string controller = routeData.Values["controller"].ToString().ToLowerInvariant();
                string action = routeData.Values["action"].ToString().ToLowerInvariant();

                switch (controller)
                {
                    case "product":
                        if (action.Equals("productdetails", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //current product ID
                            string currentProductIdStr = routeData.Values["productId"].ToString();
                            globalScript += GetRemarketingScript("product", currentProductIdStr, null, null);
                        }
                        break;

                    case "checkout":
                        if (action.Equals("completed", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //We are in last step of checkout, we can use order total for conversion value
                            Order lastOrder = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id, customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
                            globalScript += GetConversionScript(lastOrder);
                        }
                        break;
                }
            }

            if (widgetZone.Equals("head_html_tag", StringComparison.InvariantCultureIgnoreCase))
            {

                var analyticsTrackingScript = GoogleAnalyticsPageViewTrackerSettings.TrackingScript + "\n";
                if (analyticsTrackingScript.Length > 0)
                    globalScript += analyticsTrackingScript.ToString();
            }
            if (globalScript.Length > 0)
                return View("~/Plugins/Widgets.TrackerScript/Views/PublicInfo.cshtml", globalScript);
            else
                return Content("");
        }

        private string GetRemarketingScript(string pageType, string productId, string categoryName, IList<ShoppingCartItem> cart)
        {
            TrackerSettings GoogleAnalyticsPageViewTrackerSettings = _settingService.LoadSetting<TrackerSettings>(_storeContext.CurrentStore.Id);
            return InjectValuesInScript(GoogleAnalyticsPageViewTrackerSettings.RemarketingScript, pageType, productId, categoryName, null, cart);
        }
        private string InjectValuesInScript(string script, string pageType, string productId, string categoryName, Order order, IList<ShoppingCartItem> cart)
        {
			//Set default or empty values
			string totalFormated = ToScriptFormat(0);
            string prodIdsFormated = productId;
            string country = "";
            string datetime = "";
            string email = "";
            string orderId = "";
            string orderTotal = "";
            string Currency = "";
            string gtin = "";
            if (productId != null)
            {
                int id = int.Parse(productId);
                var product = _productService.GetProductById(id);
                prodIdsFormated = _productService.FormatSku(product, null);
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
                var orderitems = _orderService.GetOrderItems(order.Id);
                prodIdsFormated = ToScriptFormat((from c in orderitems select (_productService.GetProductById(c.ProductId).Sku ?? "")).ToArray());
                var orderbillingadd = _addressService.GetAddressById(order.BillingAddressId);
                country = orderbillingadd.County;
                datetime = order.CreatedOnUtc.ToString();
                datetime = Convert.ToDateTime(datetime).AddDays(14).ToString("yyyy-MM-dd");
                email = orderbillingadd.Email;
                orderId = order.Id.ToString();
                orderTotal = order.OrderTotal.ToString();
                Currency = order.CustomerCurrencyCode.ToString();
                List<string> Gtins = new List<string>();
                foreach (var c in orderitems)
                {
                    var products = _productService.GetProductById(c.ProductId);
                    if (products.Gtin != null)
                    {
                        Gtins.Add("\"{gtin}\":\"" + products.Gtin + "\"");

                    }
                }
                gtin = "[" + string.Join(",", Gtins.ToArray()) + "]";
            }

            //In case we have a cart
            if (cart != null)
            {
                decimal subTotalWithoutDiscountBase = decimal.Zero;
                if (cart.Count > 0)
                {
                    decimal orderSubTotalDiscountAmountBase = decimal.Zero;
                    List<Discount> orderSubTotalAppliedDiscounts = null;
                    decimal subTotalWithDiscountBase = decimal.Zero;
                    var subTotalIncludingTax = _workContext.TaxDisplayType == TaxDisplayType.IncludingTax;
                    _orderTotalCalculationService.GetShoppingCartSubTotal(cart, subTotalIncludingTax, out orderSubTotalDiscountAmountBase, out orderSubTotalAppliedDiscounts, out subTotalWithoutDiscountBase, out subTotalWithDiscountBase);
                    
                    totalFormated = ToScriptFormat(subTotalWithoutDiscountBase);
                    prodIdsFormated = ToScriptFormat((from c in cart select _productService.GetProductById(c.ProductId).Sku ?? "").ToArray());
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
            script = script.Replace("{CURRENCY}", Currency);

            //Remove optional and empty values
            script = script.Replace("\r\n        ecomm_category: '',", "");
            script = script.Replace("\r\n        ecomm_prodid: ,", "");
            script = script.Replace("\r\n        ecomm_totalvalue: 0.00", "");


            if (this.HttpContext.Request.Scheme == "https")
                script = script.Replace("http://", "https://");
            return script + "\n";
        }

		private string GetConversionScript(Order order)
        {
            TrackerSettings GoogleAnalyticsPageViewTrackerSettings = _settingService.LoadSetting<TrackerSettings>(_storeContext.CurrentStore.Id);
            return InjectValuesInScript(GoogleAnalyticsPageViewTrackerSettings.ConversionScript, "purchase", null, null, order, null);
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
      

    }
}
