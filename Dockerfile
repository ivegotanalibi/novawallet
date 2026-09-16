# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY NovaWallet.Ledger.slnx .
COPY src/NovaWallet.Ledger.Domain/NovaWallet.Ledger.Domain.csproj src/NovaWallet.Ledger.Domain/
COPY src/NovaWallet.Ledger.Application/NovaWallet.Ledger.Application.csproj src/NovaWallet.Ledger.Application/
COPY src/NovaWallet.Ledger.Infrastructure/NovaWallet.Ledger.Infrastructure.csproj src/NovaWallet.Ledger.Infrastructure/
COPY src/NovaWallet.Ledger.Api/NovaWallet.Ledger.Api.csproj src/NovaWallet.Ledger.Api/

# Restore
RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet publish src/NovaWallet.Ledger.Api -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "NovaWallet.Ledger.Api.dll"]
