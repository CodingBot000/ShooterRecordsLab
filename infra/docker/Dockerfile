FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS validation
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1
ADD --checksum=sha256:90a6f1a7978a1b1b096dca6c57511ca48d3b8cf052f8e704baf358f34ea2f21b https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem /tmp/rds-bundle.pem
RUN awk '/BEGIN CERTIFICATE/{n++} n {print > ("/usr/local/share/ca-certificates/rds-" n ".crt")}' /tmp/rds-bundle.pem && update-ca-certificates >/dev/null && rm /tmp/rds-bundle.pem
WORKDIR /src
COPY . .
RUN dotnet restore --locked-mode && dotnet build -c Release --no-restore -warnaserror
ENTRYPOINT ["dotnet"]

FROM validation AS publish
RUN dotnet publish src/ShooterRecordsLab.Api -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS runtime
ADD --checksum=sha256:90a6f1a7978a1b1b096dca6c57511ca48d3b8cf052f8e704baf358f34ea2f21b https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem /tmp/rds-bundle.pem
RUN awk '/BEGIN CERTIFICATE/{n++} n {print > ("/usr/local/share/ca-certificates/rds-" n ".crt")}' /tmp/rds-bundle.pem && update-ca-certificates >/dev/null && rm /tmp/rds-bundle.pem
WORKDIR /app
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 CMD ["dotnet", "ShooterRecordsLab.Api.dll", "--health-probe"]
ENTRYPOINT ["dotnet", "ShooterRecordsLab.Api.dll"]
