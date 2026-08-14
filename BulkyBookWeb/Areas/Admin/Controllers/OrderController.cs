using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers;
[Area("Admin")]
[Authorize]
public class OrderController(IUnitOfWork unit) : Controller
{
    [BindProperty]
    public OrderVM OrderVM { get; set; }
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Details(int orderId)
    {
        var orderVM = new OrderVM()
        {
            OrderHeader = unit.OrderHeader.Get(u => u.Id == orderId, includeProperties: "ApplicationUser"),
            orderDetail = unit.OrderDetail.GetAll(includeProperties: "Product")
                                          .Where(u => u.OrderHeaderId == orderId)
        };

        return View(orderVM);
    }

    [HttpPost]
    [ActionName("Details")]
    public IActionResult Details_PAY_NOW()
    {
        OrderVM.OrderHeader = unit.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id, includeProperties: "ApplicationUser");
        OrderVM.orderDetail = unit.OrderDetail.GetAll(includeProperties: "Product")
                                          .Where(u => u.OrderHeaderId == OrderVM.OrderHeader.Id);

        var domain = Request.Scheme + "://" + Request.Host.Value + "/";
        var options = new SessionCreateOptions
        {
            SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}",
            CancelUrl = domain + $"admin/order/details?orderHeaderId={OrderVM.OrderHeader.Id}",
            LineItems = new List<SessionLineItemOptions>(),
            Mode = "payment",
        };

        foreach (var item in OrderVM.orderDetail)
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
        unit.OrderHeader.UpdateStripePaymentId(OrderVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
        unit.Save();
        Response.Headers.Add("Location", session.Url);
        return new StatusCodeResult(303);

    }

    public IActionResult PaymentConfirmation(int orderHeaderId)
    {
        var orderHeader = unit.OrderHeader.Get(u => u.Id == orderHeaderId);
        if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
        {
            //this is a company order
            var service = new SessionService();
            var session = service.Get(orderHeader.SessionId);

            if (session.PaymentStatus.ToLower() == "paid")
            {
                unit.OrderHeader.UpdateStripePaymentId(orderHeaderId, session.Id, session.PaymentIntentId);
                unit.OrderHeader.UpdateStatus(orderHeaderId, orderHeader.OrderStatus, SD.PaymentStatusApproved);
                unit.Save();
            }

        }

        return View(orderHeader.Id);
    }

    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult UpdateOrderDetail()
    {
        var orderHeader = unit.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
        orderHeader.Name = OrderVM.OrderHeader.Name;
        orderHeader.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
        orderHeader.StreetAddress = OrderVM.OrderHeader.StreetAddress;
        orderHeader.City = OrderVM.OrderHeader.City;
        orderHeader.State = OrderVM.OrderHeader.State;
        orderHeader.PostalCode = OrderVM.OrderHeader.PostalCode;

        if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
        {
            orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
        }
        if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
        {
            orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
        }

        unit.OrderHeader.Update(orderHeader);
        unit.Save();

        TempData["success"] = "Order Details Updated Successfully";

        return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
    }

    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult StartProcessing()
    {
        unit.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
        unit.Save();
        TempData["success"] = "Order Processing Started Successfully";
        return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
    }

    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult ShipOrder()
    {
        var orderHeader = unit.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);

        orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
        orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
        orderHeader.OrderStatus = OrderVM.OrderHeader.OrderStatus;
        orderHeader.ShippingDate = DateTime.Now;

        if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
        {
            orderHeader.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
        }

        unit.OrderHeader.Update(orderHeader);

        unit.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusShipped);
        unit.Save();
        TempData["success"] = "Order Shipped successfully";
        return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
    }

    [HttpPost]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public IActionResult CancelOrder()
    {
        var orderHeader = unit.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);

        if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
        {
            var options = new RefundCreateOptions
            {
                Reason = RefundReasons.RequestedByCustomer,
                PaymentIntent = orderHeader.PaymentIntentId
            };

            var service = new RefundService();
            var refund = service.Create(options);

            unit.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
        }
        else
        {
            unit.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
        }

        unit.Save();
        TempData["success"] = "Order Cancelled successfully";
        return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
    }


    #region APICALLS
    [HttpGet]
    public IActionResult GetAll(string status)
    {
        var orderHeaders = unit.OrderHeader.GetAll(includeProperties: "ApplicationUser");

        if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
        {
            orderHeaders = unit.OrderHeader.GetAll(includeProperties: "ApplicationUser");
        }
        else
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            orderHeaders = unit.OrderHeader
                .GetAll(includeProperties: "ApplicationUser")
                .Where(u => u.ApplicationUserId == userId);
        }

        switch (status)
        {
            case "pending":
                orderHeaders = orderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                break;
            case "inprocess":
                orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusInProcess);
                break;
            case "completed":
                orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusShipped);
                break;
            case "approved":
                orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusApproved);
                break;
            default:
                break;
        }

        return Json(new { data = orderHeaders });
    }
    #endregion
}
