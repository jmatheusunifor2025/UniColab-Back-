FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY UniColab.Api.csproj ./
RUN dotnet restore UniColab.Api.csproj
COPY . .
RUN dotnet publish UniColab.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5080
COPY --from=build /out .
EXPOSE 5080
ENTRYPOINT ["dotnet", "UniColab.Api.dll"]
