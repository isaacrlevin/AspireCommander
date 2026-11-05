FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY AspireCommander.Console.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish --no-restore -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AspireCommander.Console.dll"]
