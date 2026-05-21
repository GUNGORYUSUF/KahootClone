# sonar-docker-analiz.ps1
# ============================================================
# Kahoot Clone - Docker Tabanli SonarQube Analiz Scripti
# Hicbir global arac kurulumu gerektirmez.
# Onkosul: Docker Desktop kurulu ve calisiyor olmali.
#
# Kullanim:
#   $env:SONAR_TOKEN = "sqp_..."
#   .\sonar-docker-analiz.ps1
# ============================================================

# --- 0. SONAR_TOKEN Kontrolu ---
if (-not $env:SONAR_TOKEN) {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "  HATA: SONAR_TOKEN ortam degiskeni tanimlanmamis!" -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Yapilmasi gerekenler:" -ForegroundColor Yellow
    Write-Host "  1. Tarayicinizda http://localhost:9000 adresine gidin"
    Write-Host "     (Eger SonarQube henuz calismiyorsa bu scripti token olmadan da"
    Write-Host "      calistirabilirsiniz; sunucu baslar, sonra token olusturup tekrar calistirun.)"
    Write-Host "  2. admin / admin ile giris yapin ve sifreyi degistirin"
    Write-Host "  3. Sag ust: My Account > Security > Generate Tokens"
    Write-Host "  4. Asagidaki komutu PowerShell'e girin:"
    Write-Host ""
    Write-Host '     $env:SONAR_TOKEN = "sqp_buraya_tokeninizi_yapistirin"' -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Ardindan bu scripti tekrar calistirin."
    Write-Host ""

    Write-Host "SonarQube baslatiliyor (token olmadan sadece sunucu baslatilir)..." -ForegroundColor Gray
    docker compose -f docker-compose.sonar.yml up -d
    Write-Host ""
    Write-Host "SonarQube hazir oldugunda http://localhost:9000 adresine gidin." -ForegroundColor Cyan
    exit 1
}

$SonarUrl        = "http://localhost:9000"
$SonarInternal   = "http://host.docker.internal:9000"
$ProjectRoot     = $PWD.Path

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  KAHOOT CLONE - SONARQUBE ANALIZI BASLIYOR" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. SonarQube'u Baslat ---
Write-Host "[1/4] SonarQube konteyneri baslatiliyor..." -ForegroundColor Yellow
docker compose -f docker-compose.sonar.yml up -d

if ($LASTEXITCODE -ne 0) {
    Write-Host "HATA: docker compose komutu basarisiz oldu." -ForegroundColor Red
    exit 1
}

# --- 2. SonarQube Hazir Olana Kadar Bekle ---
Write-Host ""
Write-Host "[2/4] SonarQube hazir olana kadar bekleniyor..." -ForegroundColor Yellow
Write-Host "      (Ilk kurulumda 60-120 saniye surebilir)" -ForegroundColor Gray

$maxAttempts = 24
$interval    = 5
$attempt     = 0
$isReady     = $false

while ($attempt -lt $maxAttempts) {
    $attempt++
    Start-Sleep -Seconds $interval
    try {
        $resp = Invoke-RestMethod -Uri "$SonarUrl/api/system/status" -TimeoutSec 5 -ErrorAction Stop
        if ($resp.status -eq "UP") { $isReady = $true; break }
        Write-Host ("      Deneme {0}/{1} - Durum: {2} - Bekleniyor..." -f $attempt, $maxAttempts, $resp.status) -ForegroundColor Gray
    } catch {
        Write-Host ("      Deneme {0}/{1} - Sunucu henuz hazir degil..." -f $attempt, $maxAttempts) -ForegroundColor Gray
    }
}

if (-not $isReady) {
    Write-Host ""
    Write-Host "HATA: SonarQube $($maxAttempts * $interval) saniye icinde hazir olmadi." -ForegroundColor Red
    Write-Host "      Detaylar icin: docker logs kahoot_sonarqube" -ForegroundColor Yellow
    exit 1
}

Write-Host "      SonarQube hazir! ($SonarUrl)" -ForegroundColor Green
Write-Host ""

# --- Yardimci fonksiyon: Windows path'i Docker mount formatina cevirir ---
function ConvertTo-DockerPath([string]$winPath) {
    $p = $winPath.Replace("\", "/")
    if ($p -match "^([A-Za-z]):(.*)$") {
        return ("/" + $Matches[1].ToLower() + $Matches[2])
    }
    return $p
}

# --- 3. Backend Analizi (.NET / C#) ---
Write-Host "[3/4] Backend analizi baslatiliyor (kahoot-backend)..." -ForegroundColor Yellow
Write-Host "      Konteyner icinde dotnet-sonarscanner kurulup analiz yapilacak." -ForegroundColor Gray
Write-Host "      (Ilk calistirildiginda NuGet restore nedeniyle 3-5 dakika surebilir)" -ForegroundColor Gray
Write-Host ""

# Bash scriptini geçici dosyaya yaz (zorunlu: Unix LF satir sonlari)
$tempScript = Join-Path $env:TEMP "kahoot-sonar-backend.sh"

# Degerleri bash script icine gomuyoruz (env var injection yerine)
# cunku dotnet sonarscanner /d: parametreleri shell quoting'e duyarlidir.
$bashContent = @"
#!/bin/bash
set -e

echo "--- dotnet-sonarscanner kuruluyor ---"
dotnet tool install --global dotnet-sonarscanner 2>/dev/null || true
export PATH="`$PATH:/root/.dotnet/tools"

echo "--- SonarScanner BEGIN ---"
dotnet sonarscanner begin \
  /k:"kahoot-backend" \
  /n:"Kahoot Clone - Backend" \
  /d:sonar.host.url="$SonarInternal" \
  /d:sonar.token="$($env:SONAR_TOKEN)" \
  /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/*.opencover.xml" \
  /d:sonar.exclusions="**/Migrations/**,**/obj/**,**/bin/**"

echo "--- Cozum derleniyor ---"
dotnet build KahootClone.slnx --no-incremental

echo "--- Unit testler ve kod kapsami ---"
dotnet test KahootClone.slnx --no-build \
  --collect:"XPlat Code Coverage;Format=opencover" \
  --results-directory TestResults \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

echo "--- SonarScanner END ---"
dotnet sonarscanner end \
  /d:sonar.token="$($env:SONAR_TOKEN)"

echo "--- Backend analizi tamamlandi ---"
"@

# Unix satirsonlari zorunlu: CRLF Linux konteynerde "bad interpreter" hatasina yol acar
[System.IO.File]::WriteAllText($tempScript, $bashContent.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))

$rootDocker   = ConvertTo-DockerPath $ProjectRoot
$scriptDocker = ConvertTo-DockerPath $tempScript

docker run --rm `
    -v "${rootDocker}:/src" `
    -v "${scriptDocker}:/kahoot-sonar-backend.sh" `
    -w /src `
    mcr.microsoft.com/dotnet/sdk:10.0 `
    bash /kahoot-sonar-backend.sh

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "HATA: Backend analizi basarisiz oldu." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  Backend analizi tamamlandi." -ForegroundColor Green
Write-Host ""

# --- 4. Frontend Analizi (React / TypeScript) ---
Write-Host "[4/4] Frontend analizi baslatiliyor (kahoot-frontend)..." -ForegroundColor Yellow

$frontendDocker = "${rootDocker}/kahoot-frontend"

docker run --rm `
    -v "${frontendDocker}:/usr/src" `
    -w /usr/src `
    -e SONAR_HOST_URL=$SonarInternal `
    -e SONAR_TOKEN=$env:SONAR_TOKEN `
    sonarsource/sonar-scanner-cli:latest

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "HATA: Frontend analizi basarisiz oldu." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  Frontend analizi tamamlandi." -ForegroundColor Green
Write-Host ""

# --- Tamamlandi ---
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  ANALIZ BASARIYLA TAMAMLANDI!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Sonuclari goruntulemek icin:" -ForegroundColor White
Write-Host ""
Write-Host "  Backend  : $SonarUrl/dashboard?id=kahoot-backend" -ForegroundColor Cyan
Write-Host "  Frontend : $SonarUrl/dashboard?id=kahoot-frontend" -ForegroundColor Cyan
Write-Host ""
Write-Host "  SonarQube'u durdurmak icin:" -ForegroundColor Gray
Write-Host "  docker compose -f docker-compose.sonar.yml down" -ForegroundColor Gray
Write-Host ""
