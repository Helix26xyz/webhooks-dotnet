# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["webhooks.Web/webhooks.Web.csproj", "webhooks.Web/"]
COPY ["webhooks.ApiService/webhooks.ApiService.csproj", "webhooks.ApiService/"]
COPY ["webhooks.ServiceDefaults/webhooks.ServiceDefaults.csproj", "webhooks.ServiceDefaults/"]
RUN dotnet restore "./webhooks.Web/webhooks.Web.csproj"
COPY . .
WORKDIR "/src/webhooks.Web"
RUN dotnet build "./webhooks.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# docker build complains about multiple publish profiles, so removing the others.
# probably bad but #insertshrugemoji
RUN rm /src/webhooks.ApiService/appsettings.Development.json /src/webhooks.Web/appsettings.Development.json /src/webhooks.ApiService/appsettings.json
RUN dotnet publish "./webhooks.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "webhooks.Web.dll"]