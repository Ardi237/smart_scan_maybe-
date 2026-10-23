FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN useradd --create-home --uid 10001 --shell /usr/sbin/nologin runner

COPY run-case.sh /opt/smartscanner/run-case.sh
RUN chmod 0555 /opt/smartscanner/run-case.sh

USER runner
ENV HOME=/home/runner \
    DOTNET_CLI_HOME=/tmp/dotnet \
    NUGET_PACKAGES=/home/runner/.nuget/packages \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

ENTRYPOINT ["/opt/smartscanner/run-case.sh"]
