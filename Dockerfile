FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# PORT is injected at runtime by Railway — Program.cs reads it via Environment.GetEnvironmentVariable("PORT")
EXPOSE 8080
ENTRYPOINT ["dotnet", "CookWatch.API.dll"]
