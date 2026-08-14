using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;

namespace Bulky.DataAccess.Repository;

public class CompanyRepository(ApplicationDbContext db) : Repository<Company>(db), ICompanyRepository
{
    public void Update(Company company)
    {
        db.Companies.Update(company);
    }
}
