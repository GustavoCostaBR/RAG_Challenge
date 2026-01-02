# .NET 10 ASP.NET minimal API
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RAG_Challenge.sln ./
COPY RAG_Challenge/RAG_Challenge.csproj RAG_Challenge/
COPY RAG_Challenge.Domain/RAG_Challenge.Domain.csproj RAG_Challenge.Domain/
COPY RAG_Challenge.Application/RAG_Challenge.Application.csproj RAG_Challenge.Application/
COPY RAG_Challenge.Infrastructure/RAG_Challenge.Infrastructure.csproj RAG_Challenge.Infrastructure/
RUN dotnet restore
COPY . .
RUN dotnet publish RAG_Challenge/RAG_Challenge.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "RAG_Challenge.dll"]

