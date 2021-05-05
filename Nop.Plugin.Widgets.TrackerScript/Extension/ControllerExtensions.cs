using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Security;
using Nop.Services.Tax;

namespace Nop.Plugin.Widgets.TrackerScript.Extension
{
    //here we have some methods shared between controllers
    public static class ControllerExtensions
    {
        public static async Task<string> PreparePrice(this Product product,
            IWorkContext workContext,
            IStoreContext storeContext,
            IProductService productService,
            IPriceCalculationService priceCalculationService,
            IPermissionService permissionService,
            ITaxService taxService,
            ICurrencyService currencyService)
        {
            if (product == null)
                throw new ArgumentNullException("product");

            const string zeroPrice = "0";
            string price = zeroPrice;

            #region Prepare product price
            switch (product.ProductType)
            {
                case ProductType.GroupedProduct:
                    {
                        #region Grouped product

                        var associatedProducts =await productService.GetAssociatedProductsAsync(product.Id, storeContext.GetCurrentStoreAsync().Id);

                        switch (associatedProducts.Count)
                        {
                            case 0:
                                {
                                    price = zeroPrice;
                                }
                                break;
                            default:
                                {

                                    if (await permissionService.AuthorizeAsync(StandardPermissionProvider.DisplayPrices))
                                    {
                                        //find a minimum possible price
                                        decimal? minPossiblePrice = null;
                                        Product minPriceProduct = null;
                                        foreach (var associatedProduct in associatedProducts)
                                        {
                                            //calculate for the maximum quantity (in case if we have tier prices)
                                            var tmpPrice = priceCalculationService.GetFinalPriceAsync(associatedProduct,
                                                await workContext.GetCurrentCustomerAsync(), decimal.Zero, true, int.MaxValue).Result.finalPrice;
                                            if (!minPossiblePrice.HasValue || tmpPrice < minPossiblePrice.Value)
                                            {
                                                minPriceProduct = associatedProduct;
                                                minPossiblePrice = tmpPrice;
                                            }
                                        }
                                        if (minPriceProduct != null && !minPriceProduct.CustomerEntersPrice)
                                        {
                                            if (minPriceProduct.CallForPrice)
                                            {
                                                //price = localizationService.GetResource("Products.CallForPrice");
                                                price = zeroPrice;
                                            }
                                            else if (minPossiblePrice.HasValue)
                                            {
                                                //calculate prices
                                                var finalPriceBase = await taxService.GetProductPriceAsync(minPriceProduct, minPossiblePrice.Value);
                                                decimal finalPrice = await currencyService.ConvertToPrimaryStoreCurrencyAsync(finalPriceBase.price, await workContext.GetWorkingCurrencyAsync());

                                                price = string.Concat(finalPrice);

                                            }
                                            else
                                            {
                                                //Actually it's not possible (we presume that minimalPrice always has a value)
                                                //We never should get here
                                                Debug.WriteLine("Cannot calculate minPrice for product #{0}", product.Id);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //hide prices
                                        price = zeroPrice;
                                    }
                                }
                                break;
                        }

                        #endregion
                    }
                    break;
                case ProductType.SimpleProduct:
                default:
                    {
                        #region Simple product
                        //prices
                        if (await permissionService.AuthorizeAsync(StandardPermissionProvider.DisplayPrices))
                        {
                            if (!product.CustomerEntersPrice)
                            {
                                if (product.CallForPrice)
                                {
                                    //call for price
                                    //price = localizationService.GetResource("Products.CallForPrice");
                                    price = zeroPrice;
                                }
                                else
                                {
                                   
                                    //calculate for the maximum quantity (in case if we have tier prices)
                                    decimal minPossiblePrice = priceCalculationService.GetFinalPriceAsync(product,
                                          await workContext.GetCurrentCustomerAsync(), decimal.Zero, true, int.MaxValue).Result.finalPrice;

                                   // decimal taxRate;
                                    var oldPriceBase = await taxService.GetProductPriceAsync(product, product.OldPrice);
                                   
                                    var finalPriceBase =await taxService.GetProductPriceAsync(product, minPossiblePrice);

                                    decimal oldPrice = await currencyService.ConvertFromPrimaryStoreCurrencyAsync(oldPriceBase.price,await workContext.GetWorkingCurrencyAsync());
                                    decimal finalPrice = await currencyService.ConvertFromPrimaryStoreCurrencyAsync(finalPriceBase.price, await workContext.GetWorkingCurrencyAsync());

                                    //do we have tier prices configured?
                                    var tierPrices = new List<TierPrice>();
                                    if (product.HasTierPrices)
                                    {
                                        var tierprices = await productService.GetTierPricesAsync(product, await workContext.GetCurrentCustomerAsync(), storeContext.GetCurrentStoreAsync().Id);

                                        tierPrices.AddRange(tierprices
                                            .OrderBy(tp => tp.Quantity)
                                            .ToList()
                                            .FilterByStore(storeContext.GetCurrentStoreAsync().Id)
                                            .RemoveDuplicatedQuantities());
                                    }
                                    //When there is just one tier (with  qty 1), 
                                    //there are no actual savings in the list.
                                    bool displayFromMessage = tierPrices.Count > 0 &&
                                        !(tierPrices.Count == 1 && tierPrices[0].Quantity <= 1);
                                    if (displayFromMessage)
                                    {
                                        price = string.Concat(finalPrice);
                                    }
                                    else
                                    {
                                        if (finalPriceBase.price != oldPriceBase.price && oldPriceBase.price != decimal.Zero)
                                        {
                                            price = string.Concat(finalPrice);
                                        }
                                        else
                                        {
                                            price = string.Concat(finalPrice);
                                        }
                                    }

                                }
                            }
                        }
                        else
                        {
                            price = zeroPrice;
                        }
                        #endregion
                    }
                    break;
            }
            #endregion
            return price;
        }
    }
}