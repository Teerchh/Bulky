using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;

namespace Bulky.DataAccess.Repository;

public class OrderDetailRepository(ApplicationDbContext db) : Repository<OrderDetail>(db), IOrderDetailRepository
{
    public void Update(OrderDetail orderDetail)
    {
        db.OrderDetails.Update(orderDetail);
    }
}
