# 1. Aşama: Build (Derleme) ortamı
# Projeniz .NET 10 olduğu için SDK 10.0 imajını kullanıyoruz
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Sadece proje dosyalarını kopyalayıp paketleri (NuGet) yüklüyoruz (Önbellekleme avantajı sağlar)
COPY ["KahootClone.Api/KahootClone.Api.csproj", "KahootClone.Api/"]
COPY ["KahootClone.Application/KahootClone.Application.csproj", "KahootClone.Application/"]
COPY ["KahootClone.Domain/KahootClone.Domain.csproj", "KahootClone.Domain/"]
COPY ["KahootClone.Infrastructure/KahootClone.Infrastructure.csproj", "KahootClone.Infrastructure/"]
RUN dotnet restore "KahootClone.Api/KahootClone.Api.csproj"

# Tüm kodları kopyalayıp projeyi Release modunda derleyip yayınlıyoruz (Publish)
COPY . .
WORKDIR "/src/KahootClone.Api"
RUN dotnet publish "KahootClone.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Aşama: Runtime (Çalışma) ortamı
# İçinde sadece .NET 10 Runtime olan daha hafif bir imaj kullanıyoruz
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KahootClone.Api.dll"]