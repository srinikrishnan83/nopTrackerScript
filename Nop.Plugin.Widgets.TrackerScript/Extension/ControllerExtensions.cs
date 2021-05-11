using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public static string PreparePrice(this Product product,
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

                        var associatedProducts = productService.GetAssociatedProducts(product.Id, storeContext.CurrentStore.Id);

                        switch (associatedProducts.Count)
                        {
                            case 0:
                                {
                                    price = zeroPrice;
                                }
                                break;
                            default:
                                {

                                    if (permissionService.Authorize(StandardPermissionProvider.DisplayPrices))
                                    {
                                        //find a minimum possible price
                                        decimal? minPossiblePrice = null;
                                        Product minPriceProduct = null;
                                        foreach (var associatedProduct in associatedProducts)
                                        {
                                            //calculate for the maximum quantity (in case if we have tier prices)
                                            var tmpPrice = priceCalculationService.GetFinalPrice(associatedProduct,
                                                workContext.CurrentCustomer, decimal.Zero, true, int.MaxValue);
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
                                                decimal taxRate;
                                                decimal finalPriceBase = taxService.GetProductPrice(minPriceProduct, minPossiblePrice.Value, out taxRate);
                                                decimal finalPrice = currencyService.ConvertFromPrimaryStoreCurrency(finalPriceBase, workContext.WorkingCurrency);

                                                price = String.Concat(finalPrice);

                                            }
                                            else
                                            {
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
                        if (permissionService.Authorize(StandardPermissionProvider.DisplayPrices))
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
                                    decimal minPossiblePrice = priceCalculationService.GetFinalPrice(product,
                                        workContext.CurrentCustomer, decimal.Zero, true, int.MaxValue);

                                    decimal taxRate;
                                    decimal oldPriceBase = taxService.GetProductPrice(product, product.OldPrice, out taxRate);
                                    decimal finalPriceBase = taxService.GetProductPrice(product, minPossiblePrice, out taxRate);

                                    decimal oldPrice = currencyService.ConvertFromPrimaryStoreCurrency(oldPriceBase, workContext.WorkingCurrency);
                                    decimal finalPrice = currencyService.ConvertFromPrimaryStoreCurrency(finalPriceBase, workContext.WorkingCurrency);

                                    //do we have tier prices configured?
                                    var tierPrices = new List<TierPrice>();
                                    if (product.HasTierPrices)
                                    {
                                        var tierprices = productService.GetTierPrices(product, workContext.CurrentCustomer, storeContext.CurrentStore.Id);

                                        tierPrices.AddRange(tierprices
                                            .OrderBy(tp => tp.Quantity)
                                            .ToList()
                                            .FilterByStore(storeContext.CurrentStore.Id)
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
                                        if (finalPriceBase != oldPriceBase && oldPriceBase != decimal.Zero)
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
                            //hide prices
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