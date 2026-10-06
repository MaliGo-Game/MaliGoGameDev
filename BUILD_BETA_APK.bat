@echo off
setlocal
REM ============================================================
REM  MaliGo - Build Beta APK (headless)
REM  UNITY MUST BE CLOSED before running this, or it will fail
REM  with "another Unity instance is running with this project".
REM
REM  Success means BOTH: Unity exited with code 0 AND a fresh APK
REM  exists. The old APK is deleted first, so a stale file can
REM  never be reported as this build. Exit code: 0 = built, 1 = failed.
REM ============================================================

set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe"
REM The project is the folder this .bat lives in (trailing backslash removed).
set "PROJECT=%~dp0"
set "PROJECT=%PROJECT:~0,-1%"
set "LOG=%PROJECT%\build.log"
set "APK=%PROJECT%\Builds\Android\MaliGo-Beta.apk"

echo.
echo === MaliGo Beta APK build starting ===
echo First run switches platform to Android and reimports assets.
echo This can take 15-40 minutes. Do not close this window.
echo Live log: "%LOG%"
echo.

REM Delete the previous APK before Unity starts, so a Unity run that dies early
REM (and still exits 0) cannot leave an old APK looking like a success.
if exist "%APK%" (
  echo Deleting the previous APK: "%APK%"
  del /f /q "%APK%"
)
if exist "%APK%" (
  echo === BUILD FAILED ===
  echo Could not delete the previous APK. Close anything using it and try again.
  echo.
  pause
  exit /b 1
)

%UNITY% -batchmode -quit -projectPath "%PROJECT%" -buildTarget Android -executeMethod MaliGoBuildPipeline.BuildAndroidBeta -logFile "%LOG%"
set RC=%ERRORLEVEL%

echo.
if "%RC%"=="0" if exist "%APK%" goto success

echo === BUILD FAILED ===
echo Unity exit code: %RC%
if not exist "%APK%" echo No APK was produced at "%APK%".
echo Open build.log and search, in this order, for:
echo   "BUILD Failed", "error CS", "Build Finished, Result: Failed",
echo   "CommandInvokationFailure", "FAILURE: Build failed with an exception",
echo   "another Unity instance is running", "Incompatible Java version",
echo   "OutOfMemory" or "Java heap space".
echo.
pause
exit /b 1

:success
echo === BUILD SUCCEEDED ===
echo APK: "%APK%"
echo.
pause
exit /b 0
