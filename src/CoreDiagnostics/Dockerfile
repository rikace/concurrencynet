FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
RUN dotnet tool install --tool-path /tools dotnet-sos
RUN dotnet tool install --tool-path /tools dotnet-trace
RUN dotnet tool install --tool-path /tools dotnet-dump
RUN dotnet tool install --tool-path /tools dotnet-counters

RUN apt-get update \
  && apt-get upgrade -y \
  && apt-get install -y \
     file \
     lldb \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY ./CoreDiagnostics/*.csproj ./
RUN dotnet restore
COPY ./CoreDiagnostics ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
COPY --from=build /tools /tools

ENV PATH="/tools:${PATH}"
