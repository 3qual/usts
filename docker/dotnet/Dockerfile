# Use the official .NET image as a base image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 12345

# # Use the SDK image to build the app
# FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
# # WORKDIR /src
# # COPY ["src/USTS_Server/USTS_Server/USTS_Server.csproj", "src/USTS_Server/"]
# # RUN dotnet restore "src/USTS_Server/USTS_Server.csproj"
# # COPY . .
# # WORKDIR "/src/src/USTS_Server"
# RUN dotnet build "/app/USTS_Server/USTS_Server/USTS_Server.csproj" -c Release -o /app/build

# FROM build AS publish
# RUN dotnet publish "/app/USTS_Server/USTS_Server/USTS_Server.csproj" -c Release -o /app/publish

# # Copy the build app to the base image
# FROM base AS final
# WORKDIR /app
# COPY --from=publish /app/publish .
# ENTRYPOINT ["dotnet", "USTS_Server.dll"]
#ENTRYPOINT ["dotnet /app/USTS_Server/USTS_Server/bin/debug/net7.0/USTS_Server.dll"]
ENTRYPOINT ["ls"]