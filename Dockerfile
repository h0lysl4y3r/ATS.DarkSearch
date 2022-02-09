# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
LABEL author="Igor Demovic <demovic@atsecurity.net>"

WORKDIR ./app

# Copy csproj and restore as distinct layers
COPY ./*.csproj ./
# Restore from nuget.org & custom feed with ATS.xxx projects
RUN dotnet restore -s https://www.myget.org/F/ats/auth/0401e349-75c8-4f9f-a828-5a0f43ec4dcf/api/v3/index.json -s https://api.nuget.org/v3/index.json

# Copy everything else and build
COPY ./ ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as runtime
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "/app/ATS.DarkSearch.dll", "--environment=Development"]