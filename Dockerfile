FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

WORKDIR /app

COPY ["OnlineChat/OnlineChat.csproj", "OnlineChat/"]
RUN dotnet restore "OnlineChat/OnlineChat.csproj"

COPY . .
RUN dotnet publish "OnlineChat/OnlineChat.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnlineChat.dll"]
