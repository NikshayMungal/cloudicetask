# syntax=docker/dockerfile:1

# ---------- Build stage: restore, compile, publish ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached until dependencies change.
COPY LogisticsTracker.Api/LogisticsTracker.Api.csproj LogisticsTracker.Api/
RUN dotnet restore LogisticsTracker.Api/LogisticsTracker.Api.csproj

COPY LogisticsTracker.Api/ LogisticsTracker.Api/
RUN dotnet publish LogisticsTracker.Api/LogisticsTracker.Api.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime stage: small image, non-root user ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The "app" user is built into the .NET 8 images. Never run as root.
USER $APP_UID

# Render injects $PORT; locally we fall back to 8080.
ENV PORT=8080
EXPOSE 8080

# Shell form so ${PORT} is expanded at container start.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} exec dotnet LogisticsTracker.Api.dll"]
