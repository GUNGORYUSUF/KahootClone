@echo off
:: NOT: Ilk kullanimdan once terminali (CMD) acip tokeninizi sisteme kaydetmek icin su komutu calistirin:
:: set SONAR_TOKEN=sonar_qube_den_aldigin_YENI_token_buraya


echo ===================================================
echo SONARQUBE ANALIZI VE TESTLER BASLIYOR...
echo ===================================================

if "%SONAR_TOKEN%"=="" (
    echo HATA: SONAR_TOKEN ortam degiskeni bulunamadi!
    echo Guvenlik icin tokeninizi koda yazmak yerine terminale su komutu girerek kaydedin:
    echo set SONAR_TOKEN=sonar_qube_den_aldigin_YENI_token_buraya
    pause
    exit /b 1
)

echo [1/3] SonarScanner baslatiliyor...
dotnet sonarscanner begin /k:"KahootProjesi" /d:sonar.host.url="http://localhost:9000" /d:sonar.login="%SONAR_TOKEN%" /d:sonar.cs.opencover.reportsPaths="**/*.opencover.xml"

echo.
echo [2/3] Proje derleniyor ve Unit Testler calistiriliyor...
dotnet build
dotnet test --collect:"XPlat Code Coverage;Format=opencover"

echo.
echo [3/3] Sonuclar SonarQube sunucusuna gonderiliyor...
dotnet sonarscanner end /d:sonar.login="%SONAR_TOKEN%"

echo ===================================================
echo ANALIZ TAMAMLANDI! (http://localhost:9000 adresinden kontrol edebilirsiniz)
echo ===================================================
pause