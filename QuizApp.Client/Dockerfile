FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["QuizApp.Client/QuizApp.Client.csproj", "QuizApp.Client/"]
COPY ["QuizApp.Shared/QuizApp.Shared.csproj", "QuizApp.Shared/"]
RUN dotnet restore "QuizApp.Client/QuizApp.Client.csproj"

COPY . .
WORKDIR /src/QuizApp.Client
RUN dotnet publish "QuizApp.Client.csproj" -c Release -o /app/publish

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=build /app/publish/wwwroot .
COPY QuizApp.Client/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
CMD ["sh", "-c", "nginx -g 'daemon off;' -c /etc/nginx/nginx.conf"]
