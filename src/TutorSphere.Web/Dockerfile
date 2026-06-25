# TutorSphere Web (Blazor Server) — multi-stage build (.NET 10)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/TutorSphere.Web/TutorSphere.Web.csproj", "src/TutorSphere.Web/"]
COPY ["src/TutorSphere.Application/TutorSphere.Application.csproj", "src/TutorSphere.Application/"]
COPY ["src/TutorSphere.Domain/TutorSphere.Domain.csproj", "src/TutorSphere.Domain/"]
RUN dotnet restore "src/TutorSphere.Web/TutorSphere.Web.csproj"
COPY . .
WORKDIR /src/src/TutorSphere.Web
RUN dotnet build "TutorSphere.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TutorSphere.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TutorSphere.Web.dll"]
