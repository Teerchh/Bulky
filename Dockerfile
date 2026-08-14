# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore project dependencies (copy csproj files first for better layer caching)
COPY ["BulkyBookWeb/BulkyBookWeb.csproj", "BulkyBookWeb/"]
COPY ["Bulky.DataAccess/Bulky.DataAccess.csproj", "Bulky.DataAccess/"]
COPY ["Bulky.Models/Bulky.Models.csproj", "Bulky.Models/"]
COPY ["Bulky.Utility/Bulky.Utility.csproj", "Bulky.Utility/"]
RUN dotnet restore "BulkyBookWeb/BulkyBookWeb.csproj"

# Copy everything and publish
COPY . .
WORKDIR "/src/BulkyBookWeb"
RUN dotnet publish "BulkyBookWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BulkyBookWeb.dll"]
