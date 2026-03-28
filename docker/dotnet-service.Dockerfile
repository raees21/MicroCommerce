FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
ARG PROJECT_PATH
WORKDIR /src

COPY . .
RUN dotnet restore "$PROJECT_PATH"
RUN dotnet publish "$PROJECT_PATH" -c Release -o /app/publish /p:UseAppHost=false
RUN PROJECT_DLL="$(basename "$PROJECT_PATH" .csproj).dll" && \
    printf '#!/bin/sh\nset -e\ndotnet %s\n' "$PROJECT_DLL" > /tmp/run.sh && \
    chmod +x /tmp/run.sh

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /tmp/run.sh /run.sh
ENTRYPOINT ["/bin/sh", "/run.sh"]
