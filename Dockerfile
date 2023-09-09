# Build
FROM alpine:latest as build
RUN apk add dotnet7-sdk
WORKDIR /airroute
COPY . .
RUN dotnet publish -c Release -o /build

# Production
FROM alpine:latest as production

EXPOSE 8080

RUN apk add dotnet7-sdk
WORKDIR /app
COPY --from=build /build /app