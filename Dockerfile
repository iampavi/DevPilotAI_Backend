FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["DevPilotAI.Shared/DevPilotAI.Shared.csproj", "DevPilotAI.Shared/"]
COPY ["DevPilotAI.Domain/DevPilotAI.Domain.csproj", "DevPilotAI.Domain/"]
COPY ["DevPilotAI.Application/DevPilotAI.Application.csproj", "DevPilotAI.Application/"]
COPY ["DevPilotAI.Infrastructure/DevPilotAI.Infrastructure.csproj", "DevPilotAI.Infrastructure/"]
COPY ["DevPilotAI.Api/DevPilotAI.Api.csproj", "DevPilotAI.Api/"]
RUN dotnet restore "DevPilotAI.Api/DevPilotAI.Api.csproj"

# Copy the rest of the files and build the app
COPY . .
WORKDIR "/src/DevPilotAI.Api"
RUN dotnet build "DevPilotAI.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DevPilotAI.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DevPilotAI.Api.dll"]
