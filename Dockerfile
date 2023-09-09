FROM alpine:latest as build

RUN apk add dotnet7-sdk

WORKDIR /airroute
COPY . .
RUN dotnet build -c Release

FROM alpine:latest as production

RUN apk add dotnet7-runtime dotnet7-sdk

WORKDIR /airroute

COPY --from=build /airroute .
#COPY --from=build /airroute/Main/bin/Release/net7.0 /airroute/
#COPY --from=build /airroute/Main/wwwroot /airroute/Main/wwwroot
#COPY --from=build /airroute/Main/obj/Release /airroute/Main/obj/Release