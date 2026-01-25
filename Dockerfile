# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Cedeva.sln", "./"]
COPY ["src/Cedeva.Core/Cedeva.Core.csproj", "src/Cedeva.Core/"]
COPY ["src/Cedeva.Infrastructure/Cedeva.Infrastructure.csproj", "src/Cedeva.Infrastructure/"]
COPY ["src/Cedeva.Website/Cedeva.Website.csproj", "src/Cedeva.Website/"]

# Restore dependencies
RUN dotnet restore "src/Cedeva.Website/Cedeva.Website.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/Cedeva.Website"
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cedeva.Website.dll"]
