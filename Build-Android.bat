@echo off
setlocal
cd /d "%~dp0"

REM Укажите путь к Unity 2022.3.20f1, если Unity установлена не в стандартной папке.
set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe"

if not exist "%UNITY_EXE%" (
  echo Unity 2022.3.20f1 не найдена по стандартному пути:
  echo %UNITY_EXE%
  echo.
  echo Измените UNITY_EXE в этом BAT-файле на фактический путь к Unity.exe.
  pause
  exit /b 1
)

echo Запускается автоматическая настройка и сборка двух Android APK...
"%UNITY_EXE%" -batchmode -quit -projectPath "%CD%" -executeMethod ProjectBuilder.BuildAllCli -logFile "%CD%\build.log"

if errorlevel 1 (
  echo.
  echo Сборка завершилась с ошибкой. Подробности: build.log
  pause
  exit /b 1
)

echo.
echo Сборка завершена. Проверьте папку Builds:
echo   Builds\AR-Mebel.apk
echo   Builds\AR-Monety.apk
pause
