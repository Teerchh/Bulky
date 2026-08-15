using Bulky.DataAccess.Data;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bulky.DataAccess.DBInitializer;

public class DBInitializer(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db, IConfiguration configuration, ILogger<DBInitializer> logger) : IDBInitializer
{
    public void Initialize()
    {
        //migrations if they are not applied
        try
        {
            if (db.Database.GetPendingMigrations().Any())
            {
                db.Database.Migrate();
            }
        }
        catch (Exception) { }

        //create roles individually if they are missing
        if (!roleManager.RoleExistsAsync(SD.Role_Customer).GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole(SD.Role_Customer)).GetAwaiter().GetResult();
        if (!roleManager.RoleExistsAsync(SD.Role_Employee).GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole(SD.Role_Employee)).GetAwaiter().GetResult();
        if (!roleManager.RoleExistsAsync(SD.Role_Admin).GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole(SD.Role_Admin)).GetAwaiter().GetResult();
        if (!roleManager.RoleExistsAsync(SD.Role_Company).GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole(SD.Role_Company)).GetAwaiter().GetResult();

        //create admin user if it does not exist (independent of the roles above)
        //Credentials come from config: appsettings.Development.json locally, App Settings on Azure.
        var adminEmail = configuration["Admin:Email"] ?? "admin@admin.com";
        var adminPassword = configuration["Admin:Password"] ?? "Admin1234!";
        var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
        if (adminUser == null)
        {
            var user = new ApplicationUser { UserName = adminEmail, Email = adminEmail, Name = "Administrator", PhoneNumber = "1234567890", StreetAddress = "admin av", State = "OYO", PostalCode = "200005", City = "IB" };
            var createResult = userManager.CreateAsync(user, adminPassword).GetAwaiter().GetResult();
            //re-fetch the user from the DB so the role link uses the persisted row
            //(avoids the FK violation on AspNetUserRoles when seeding a brand-new user)
            if (createResult.Succeeded)
            {
                var persistedUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
                if (persistedUser != null)
                {
                    userManager.AddToRoleAsync(persistedUser, SD.Role_Admin).GetAwaiter().GetResult();
                }
            }
            else
            {
                logger.LogWarning("Failed to create admin user '{Email}': {Errors}",
                    adminEmail, string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }
        }

        return;
    }
}
