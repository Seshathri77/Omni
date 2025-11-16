FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy OmniFlow projects
COPY ["src/OmniFlow.Core/OmniFlow.Core.csproj", "src/OmniFlow.Core/"]
COPY ["src/OmniFlow.Messaging/OmniFlow.Messaging.csproj", "src/OmniFlow.Messaging/"]
COPY ["src/OmniFlow.Idempotency/OmniFlow.Idempotency.csproj", "src/OmniFlow.Idempotency/"]
COPY ["src/OmniFlow.Observability/OmniFlow.Observability.csproj", "src/OmniFlow.Observability/"]
COPY ["src/OmniFlow.Adapters.MongoDb/OmniFlow.Adapters.MongoDb.csproj", "src/OmniFlow.Adapters.MongoDb/"]

# Copy Yath projects
COPY ["Yath/Yath.Shared/Yath.Shared.csproj", "Yath/Yath.Shared/"]
COPY ["Yath/Yath.LocationService/Yath.LocationService/Yath.LocationService.csproj", "Yath/Yath.LocationService/Yath.LocationService/"]
RUN dotnet restore "Yath/Yath.LocationService/Yath.LocationService/Yath.LocationService.csproj"

# Copy source code
COPY src/ src/
COPY Yath/Yath.Shared/ Yath/Yath.Shared/
COPY Yath/Yath.LocationService/Yath.LocationService/ Yath/Yath.LocationService/Yath.LocationService/
WORKDIR /src/Yath/Yath.LocationService/Yath.LocationService
RUN dotnet build "Yath.LocationService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Yath.LocationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Yath.LocationService.dll"]
