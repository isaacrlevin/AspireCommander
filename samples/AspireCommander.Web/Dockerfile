FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY AspireCommander.Web.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish --no-restore -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AspireCommander.Web.dll"]
