FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["QuizApp.Server/QuizApp.Server.csproj", "QuizApp.Server/"]
COPY ["QuizApp.Shared/QuizApp.Shared.csproj", "QuizApp.Shared/"]
RUN dotnet restore "QuizApp.Server/QuizApp.Server.csproj"

COPY . .
WORKDIR /src/QuizApp.Server
RUN dotnet publish "QuizApp.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet QuizApp.Server.dll --urls http://0.0.0.0:${PORT:-8080}"]
