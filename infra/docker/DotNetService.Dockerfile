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

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

ARG DLL_NAME
ENV APP_DLL="${DLL_NAME}"

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]

