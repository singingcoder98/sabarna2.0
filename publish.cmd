@echo off
setlocal
title Sabarna - build a publish folder
cd /d "%~dp0"

rem ---------------------------------------------------------------------------------------------
rem saptarshi: added 12-Nov-2026 - a COPY of this file on the Desktop is a classic mistake:
rem %~dp0 then points at the Desktop, Sabarna.Web.csproj is not there, and dotnet answers with
rem "MSBUILD : error MSB1009: Project file does not exist". So: if the project is not here, look in
rem the usual place before giving up, and if that fails too, say exactly what to do in plain words.
rem ---------------------------------------------------------------------------------------------
if not exist "Sabarna.Web.csproj" (
    if exist "D:\MVCDotNet\sabarna2.0\Sabarna.Web.csproj" (
        cd /d "D:\MVCDotNet\sabarna2.0"
        echo [i] This file was opened from somewhere else - running in D:\MVCDotNet\sabarna2.0 instead.
        echo.
    )
)

if not exist "Sabarna.Web.csproj" (
    echo [X] The project file Sabarna.Web.csproj is not in this folder.
    echo     This happens when publish.cmd is COPIED to the Desktop instead of shortcut-ed.
    echo.
    echo     Do this instead:
    echo       1. Delete this copy from the Desktop.
    echo       2. Open D:\MVCDotNet\sabarna2.0 in File Explorer.
    echo       3. Right-click publish.cmd  =^>  Send to  =^>  Desktop ^(create shortcut^).
    echo     A shortcut has a little arrow in its corner; a copy does not.
    echo.
    pause
    exit /b 1
)

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
