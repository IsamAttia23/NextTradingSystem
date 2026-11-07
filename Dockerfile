FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NEXT.Domain/NEXT.Domain.csproj NEXT.Domain/
COPY NEXT.Application/NEXT.Application.csproj NEXT.Application/
COPY NEXT.Infrastructure/NEXT.Infrastructure.csproj NEXT.Infrastructure/
COPY NEXT.web/NEXT.web.csproj NEXT.web/

RUN dotnet restore NEXT.web/NEXT.web.csproj

COPY . .
RUN dotnet publish NEXT.web/NEXT.web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "NEXT.web.dll"]