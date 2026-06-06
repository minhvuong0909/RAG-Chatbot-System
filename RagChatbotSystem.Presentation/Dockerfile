FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restoring
COPY RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj RagChatbotSystem.Presentation/
COPY RagChatbotSystem.DataAccess/RagChatbotSystem.DataAccess.csproj RagChatbotSystem.DataAccess/
COPY RagChatbotSystem.Business/RagChatbotSystem.Business.csproj RagChatbotSystem.Business/

RUN dotnet restore RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj

# Copy all source files and publish
COPY RagChatbotSystem.Presentation/ RagChatbotSystem.Presentation/
COPY RagChatbotSystem.Business/ RagChatbotSystem.Business/
COPY RagChatbotSystem.DataAccess/ RagChatbotSystem.DataAccess/

RUN dotnet publish RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj -c Release -o /app/publish --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5259

ENV ASPNETCORE_URLS=http://+:5259
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "RagChatbotSystem.Presentation.dll"]
