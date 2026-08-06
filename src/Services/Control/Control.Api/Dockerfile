# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "src/Services/Control/Control.Api/Control.Api.csproj" -c Release -o /app --no-self-contained

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Least privilege: la imagen oficial trae un usuario no-root desde .NET 8
# (docs.microsoft.com/dotnet/core/whats-new/dotnet-8/containers#non-root-user).
USER $APP_UID
ENTRYPOINT ["dotnet", "Control.Api.dll"]
