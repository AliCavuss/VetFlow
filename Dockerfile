# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/VetFlow.Domain/VetFlow.Domain.csproj src/VetFlow.Domain/
COPY src/VetFlow.Application/VetFlow.Application.csproj src/VetFlow.Application/
COPY src/VetFlow.Infrastructure/VetFlow.Infrastructure.csproj src/VetFlow.Infrastructure/
COPY src/VetFlow.Web/VetFlow.Web.csproj src/VetFlow.Web/
RUN dotnet restore src/VetFlow.Web/VetFlow.Web.csproj
COPY src/ src/
RUN dotnet publish src/VetFlow.Web/VetFlow.Web.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "VetFlow.Web.dll"]
