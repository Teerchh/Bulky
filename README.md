# Bulky

A full-featured **ASP.NET Core 10 MVC** e-commerce web application built with the classic "Bulky Book" architecture. Designed as a portfolio project demonstrating a complete production-style .NET web stack.

## ✨ Features

- 🛍️ Customer storefront with product catalog, categories, and search
- 🛒 Session-based shopping cart
- 💳 **Stripe** payment integration (checkout + payment confirmation)
- 🔐 **ASP.NET Identity** authentication & authorization (Customer / Employee / Admin / Company roles)
- 🔑 **Google OAuth** single sign-on
- 👑 Admin area: manage products, categories, companies, orders, and users (with DataTables)
- 🏢 Company + employee shopping workflow
- 📄 Repository pattern with Unit of Work, auto database migrations & seed data

## 🧱 Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10 MVC, Razor Pages |
| Data | Entity Framework Core 10, PostgreSQL (Npgsql) |
| Auth | ASP.NET Identity + Google OAuth |
| Payments | Stripe |
| Frontend | Razor Views, Bootstrap 5, jQuery, DataTables |
| Structure | Multi-project solution (`BulkyBookWeb`, `Bulky.DataAccess`, `Bulky.Models`, `Bulky.Utility`) |

## 🚀 Local Setup

1. **Install** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [PostgreSQL](https://www.postgresql.org/download/) (16+).
2. **Clone** the repo, then open `Bulky.sln` in Visual Studio or VS Code.
3. **Create local secrets** — copy the sample below into `BulkyBookWeb/appsettings.Development.json`
   (this file is git-ignored and must **not** be committed):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=BulkyIM;Username=postgres;Password=YOUR_PASSWORD"
     },
     "Stripe": {
       "SecretKey": "YOUR_STRIPE_SECRET_KEY",
       "PublishableKey": "YOUR_STRIPE_PUBLISHABLE_KEY"
     },
     "Google": {
       "ClientId": "YOUR_GOOGLE_CLIENT_ID",
       "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
     },
     "Admin": {
       "Email": "admin@admin.com",
       "Password": "Admin1234!"
     }
   }
   ```

   > 💡 If you don't have Stripe / Google keys yet, the app still runs — those features are gracefully disabled until configured.

4. **Run** the app — `dotnet run --project BulkyBookWeb`. The database is created & seeded automatically on startup.

   **Default admin login:** `admin@admin.com` / `Admin1234!` (change this before deploying publicly!)

## 🔐 Configuration (all via environment variables)

Everything sensitive is read from configuration, so you never commit secrets. Use these env var names on your host (Azure App Service uses `__` for nesting):

| Environment variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Stripe__SecretKey` | Stripe secret key |
| `Stripe__PublishableKey` | Stripe publishable key |
| `Google__ClientId` | Google OAuth client id |
| `Google__ClientSecret` | Google OAuth client secret |
| `Admin__Email` | Seeded admin email |
| `Admin__Password` | Seeded admin password |

## ☁️ Deploying to Azure App Service

### Option A — GitHub Actions (recommended, free CI/CD)

1. **Create an Azure App Service** (Linux, .NET 10 runtime):
   ```bash
   az group create --name bulky-rg --location eastus
   az appservice plan create --name bulky-plan --resource-group bulky-rg --sku F1 --is-linux
   az webapp create --resource-group bulky-rg --plan bulky-plan --name YOUR-APP-NAME --runtime "DOTNETCORE:10.0"
   ```
2. **Download the publish profile** (Portal → your Web App → *Overview* → *Get publish profile*) and add it as a GitHub secret named `AZURE_WEBAPP_PUBLISH_PROFILE`.
3. **Set your app name** in `.github/workflows/deploy-azure.yml` (`AZURE_WEBAPP_NAME`).
4. **Push to `main`** — the workflow builds and deploys automatically.
5. **Add your secrets** in the Azure Portal under *Settings → Configuration → Application settings* using the env var names above.

### Option B — Docker container

A `Dockerfile` is included. Push it to Azure Container Registry (or Docker Hub) and point the App Service at it:
```bash
docker build -t bulky .
```

> 💡 The app runs with any PostgreSQL host. For free managed Postgres, try [Neon](https://neon.tech), [Render](https://render.com), [Railway](https://railway.com), or **Azure Database for PostgreSQL** (free tier). Just put that connection string in the `ConnectionStrings__DefaultConnection` setting.

### After deploying — required App Settings

| Setting | Example |
|---|---|
| `ConnectionStrings__DefaultConnection` | Your hosted **PostgreSQL** connection string (e.g. Azure/Neon/Render) |
| `Stripe__SecretKey` / `Stripe__PublishableKey` | Your Stripe keys |
| `Google__ClientId` / `Google__ClientSecret` | Your Google OAuth keys |
| `Admin__Password` | A **strong** password (never use the default) |

> ⚠️ For the Google OAuth login to work in production, add your deployed domain (e.g. `https://yourapp.azurewebsites.net`) as an authorized redirect URI in the [Google Cloud Console](https://console.cloud.google.com/).

## 📁 Project Structure

```
Bulky.sln
├── BulkyBookWeb/       # ASP.NET Core MVC web app (areas: Customer, Admin, Identity)
├── Bulky.DataAccess/   # EF Core DbContext, migrations, repositories (Unit of Work)
├── Bulky.Models/       # Domain models & view models
└── Bulky.Utility/      # Shared constants, helpers (SD, EmailSender, StripeSettings)
```

## 📄 License

See [LICENSE.txt](LICENSE.txt).
