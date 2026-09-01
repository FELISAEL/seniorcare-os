FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

ARG PROJECT_PATH
COPY . .
RUN dotnet restore "${PROJECT_PATH}"
RUN dotnet publish "${PROJECT_PATH}" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ARG DLL_NAME
ENV APP_DLL="${DLL_NAME}"
ENV ASPNETCORE_URLS="http://+:8080"

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]

