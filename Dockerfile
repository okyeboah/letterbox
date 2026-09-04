FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Letterbox/Letterbox.csproj ./Letterbox/
RUN dotnet restore Letterbox/Letterbox.csproj
COPY src/ ./
RUN dotnet publish Letterbox/Letterbox.csproj --configuration Release --output /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV LETTERBOX_BIND=0.0.0.0
EXPOSE 4600 2525
ENTRYPOINT ["dotnet", "Letterbox.dll"]
