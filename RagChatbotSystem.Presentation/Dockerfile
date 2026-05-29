FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj", "RagChatbotSystem.Presentation/"]
COPY ["RagChatbotSystem.Business/RagChatbotSystem.Business.csproj", "RagChatbotSystem.Business/"]
COPY ["RagChatbotSystem.DataAccess/RagChatbotSystem.DataAccess.csproj", "RagChatbotSystem.DataAccess/"]
RUN dotnet restore "RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj"

COPY . .
WORKDIR "/src/RagChatbotSystem.Presentation"
RUN dotnet publish "RagChatbotSystem.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RagChatbotSystem.Presentation.dll"]
