using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;

namespace Bulky.DataAccess.Repository;

public class OrderHeaderRepository(ApplicationDbContext db) : Repository<OrderHeader>(db), IOrderHeaderRepository
{
    public void Update(OrderHeader orderHeader)
    {
        db.OrderHeaders.Update(orderHeader);
    }

    public void UpdateStatus(int id, string orderStatus, string? paymenStatus = null)
    {
        var order = db.OrderHeaders.FirstOrDefault(x => x.Id == id);
        if (order != null)
        {
            order.OrderStatus = orderStatus;
            if (!string.IsNullOrEmpty(paymenStatus))
            {
                order.PaymentStatus = paymenStatus;
            }
        }
    }

    public void UpdateStripePaymentId(int id, string sessionId, string? paymentIntentId)
    {
        var order = db.OrderHeaders.FirstOrDefault(x => x.Id == id);
        if (!string.IsNullOrEmpty(sessionId))
        {
            order.SessionId = sessionId;
        }
        if (!string.IsNullOrEmpty(paymentIntentId))
        {
            order.PaymentIntentId = paymentIntentId;
            order.PaymentDate = DateTime.Now;
        }
    }
}
