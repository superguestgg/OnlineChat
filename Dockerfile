FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

WORKDIR /app

COPY *.sln .
COPY ["OnlineChat/OnlineChat.csproj", "OnlineChat/"]
COPY . .
RUN dotnet restore "OnlineChat/OnlineChat.csproj"

RUN dotnet publish "OnlineChat/OnlineChat.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnlineChat.dll"]
