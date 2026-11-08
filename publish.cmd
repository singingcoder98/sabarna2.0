@echo off
setlocal
title Sabarna - build a publish folder
cd /d "%~dp0"

echo ============================================================
echo   SABARNA - building the folder you upload to the host
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [X] The dotnet command was not found.
    echo     Install the .NET 8 SDK, or run this from a "Developer PowerShell".
    pause
    exit /b 1
)

echo [1/2] Cleaning old output...
if exist "publish" rmdir /s /q "publish"

echo [2/2] Publishing Release build to .\publish ...
echo.
dotnet publish Sabarna.Web.csproj -c Release -o publish
if errorlevel 1 (
    echo.
    echo [X] Publish failed - read the message above.
    pause
    exit /b 1
)

echo.
rem saptarshi: added 08-Nov-2026 - belt and braces. The csproj already keeps the local
rem database out of publish\, but if one ever slips through it is deleted here, so it can
rem never be uploaded over the live shop's App_Data\app.db.
if exist "publish\App_Data\app.db" (
    echo [X] Found publish\App_Data\app.db - deleting it so it cannot overwrite the live database.
    del /q "publish\App_Data\app.db"
)
if exist "publish\App_Data\app.db-wal" del /q "publish\App_Data\app.db-wal"
if exist "publish\App_Data\app.db-shm" del /q "publish\App_Data\app.db-shm"

echo ============================================================
echo   Done.  Folder: %CD%\publish
echo ============================================================
echo.
echo   Next: upload EVERYTHING INSIDE this folder (not the
echo   folder itself) to /wwwroot with FileZilla, then open
echo   https://sabarna.runasp.net
echo.
echo   Keep these together in the root:
echo     Sabarna.Web.dll   web.config   appsettings.json
echo     App_Data\         wwwroot\     (views are inside the dll)
echo.
echo   Never tick "remove files at destination" style options and
echo   never delete App_Data on the server: that is the database.
echo.
pause
