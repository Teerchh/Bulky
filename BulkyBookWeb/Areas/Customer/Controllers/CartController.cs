using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers;

[Area(nameof(Customer))]
[Authorize]
public class CartController(IUnitOfWork unit) : Controller
{
    public IActionResult Index()
    {
        var claimsIdentity = (ClaimsIdentity)User.Identity;
        var userid = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

        var shoppingCartVM = new ShoppingCartVM()
        {
            ShoppingCartList = unit.ShoppingCart.GetAll(includeProperties: "Product").Where(u => u.ApplicationUserId == userid),
            OrderHeader = new()
        };

        var productImages = unit.ProductImage.GetAll();

        foreach (var cart in shoppingCartVM.ShoppingCartList)
        {
            cart.Product.ProductImages = productImages.Where(u => u.ProductId == cart.Product.Id).ToList();
            cart.Price = GetPriceBasedOnQuantity(cart);
            shoppingCartVM.OrderHeader.OrderTotal += cart.Price * cart.Count;
        }

        return View(shoppingCartVM);
    }

    public IActionResult Summary()
    {
        var claimsIdentity = (ClaimsIdentity)User.Identity;
        var userid = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

        var shoppingCartVM = new ShoppingCartVM()
        {
            ShoppingCartList = unit.ShoppingCart.GetAll(includeProperties: "Product").Where(u => u.ApplicationUserId == userid),
            OrderHeader = new()
        };

        shoppingCartVM.OrderHeader.ApplicationUser = unit.ApplicationUser.Get(u => u.Id == userid);

        shoppingCartVM.OrderHeader.Name = shoppingCartVM.OrderHeader.ApplicationUser.Name;
        shoppingCartVM.OrderHeader.PhoneNumber = shoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
        shoppingCartVM.OrderHeader.StreetAddress = shoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
        shoppingCartVM.OrderHeader.City = shoppingCartVM.OrderHeader.ApplicationUser.City;
        shoppingCartVM.OrderHeader.State = shoppingCartVM.OrderHeader.ApplicationUser.State;
        shoppingCartVM.OrderHeader.PostalCode = shoppingCartVM.OrderHeader.ApplicationUser.PostalCode;



        foreach (var cart in shoppingCartVM.ShoppingCartList)
        {
            cart.Price = GetPriceBasedOnQuantity(cart);
            shoppingCartVM.OrderHeader.OrderTotal += cart.Price * cart.Count;
        }

        return View(shoppingCartVM);
    }
    [HttpPost]
    [ActionName(nameof(Summary))]
    public IActionResult SummaryPOST(ShoppingCartVM shoppingCartVM)
    {
        var claimsIdentity = (ClaimsIdentity)User.Identity;
        var userid = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

        shoppingCartVM.ShoppingCartList = unit.ShoppingCart.GetAll(includeProperties: "Product").Where(u => u.ApplicationUserId == userid);

        shoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
        shoppingCartVM.OrderHeader.ApplicationUserId = userid;

        var applicationUser = unit.ApplicationUser.Get(u => u.Id == userid);

        foreach (var cart in shoppingCartVM.ShoppingCartList)
        {
            cart.Price = GetPriceBasedOnQuantity(cart);
            shoppingCartVM.OrderHeader.OrderTotal += cart.Price * cart.Count;
        }

        if (applicationUser.CompanyId.GetValueOrDefault() == 0)
        {
            //regular customer account
            shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            shoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
        }
        else
        {
            //COMPANY ACCOUNT
            shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
            shoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
        }

        unit.OrderHeader.Add(shoppingCartVM.OrderHeader);
        unit.Save();

        foreach (var cart in shoppingCartVM.ShoppingCartList)
        {
            OrderDetail orderDetail = new()
            {
                ProductId = cart.ProductId,
                OrderHeaderId = shoppingCartVM.OrderHeader.Id,
                Price = cart.Price,
                Count = cart.Count
            };
            unit.OrderDetail.Add(orderDetail);
            unit.Save();
        };

        if (applicationUser.CompanyId.GetValueOrDefault() == 0)
        {
            //regular customer account and we need to capture payment
            //stripe logic

            var domain = Request.Scheme + "://" + Request.Host.Value + "/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={shoppingCartVM.OrderHeader.Id}",
                CancelUrl = domain + "customer/cart/index",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
            };

            foreach (var item in shoppingCartVM.ShoppingCartList)
            {
                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100),//#20.50 => 2050
                        Currency = "NGN",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title
                        }
                    },
                    Quantity = item.Count
                };
                options.LineItems.Add(sessionLineItem);
            }

            var service = new SessionService();
            var session = service.Create(options);
            unit.OrderHeader.UpdateStripePaymentId(shoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
            unit.Save();
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);



        }

        return RedirectToAction(nameof(OrderConfirmation), new { id = shoppingCartVM.OrderHeader.Id });
    }

    public IActionResult OrderConfirmation(int id)
    {
        var orderHeader = unit.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");
        if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
        {
            //this is a non company order
            var service = new SessionService();
            var session = service.Get(orderHeader.SessionId);

            if (session.PaymentStatus.ToLower() == "paid")
            {
                unit.OrderHeader.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
                unit.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                unit.Save();
            }
            HttpContext.Session.Clear();

        }
        var shoppingCarts = unit.ShoppingCart.GetAll()
            .Where(u => u.ApplicationUserId == orderHeader.ApplicationUserId);

        unit.ShoppingCart.RemoveRange(shoppingCarts);
        unit.Save();


        return View(id);
    }

    public IActionResult Plus(int cartId)
    {
        var cart = unit.ShoppingCart.Get(u => u.Id == cartId);
        cart.Count += 1;
        unit.ShoppingCart.Update(cart);
        unit.Save();
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Minus(int cartId)
    {
        var cart = unit.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
        if (cart.Count <= 1)
        {
            //remove from cart
            HttpContext.Session.SetInt32(SD.SessionCart,
          unit.ShoppingCart.GetAll().Where(u => u.ApplicationUserId == cart.ApplicationUserId).Count() - 1);
            unit.ShoppingCart.Remove(cart);
        }
        else
        {
            cart.Count -= 1;
            unit.ShoppingCart.Update(cart);
        }

        unit.Save();
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Remove(int cartId)
    {
        var cart = unit.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
        //remove from cart
        HttpContext.Session.SetInt32(SD.SessionCart,
           unit.ShoppingCart.GetAll().Where(u => u.ApplicationUserId == cart.ApplicationUserId).Count() - 1);

        unit.ShoppingCart.Remove(cart);


        unit.Save();
        return RedirectToAction(nameof(Index));
    }

    private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
    {
        if (shoppingCart.Count <= 50)
        {
            return shoppingCart.Product.Price;
        }
        else
        {
            if (shoppingCart.Count <= 100)
            {
                return shoppingCart.Product.Price50;
            }
            else
            {
                return shoppingCart.Product.Price100;
            }
        }
    }
}
