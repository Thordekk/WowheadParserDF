@echo off
setlocal

set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo MSBuild not found at:
    echo   %MSBUILD%
    echo Install Visual Studio 2022 with the .NET desktop build tools, or edit this script.
    exit /b 1
)

"%MSBUILD%" "%~dp0WowHeadParser.sln" -t:Rebuild -p:Configuration=Release -v:m -nologo
if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo BUILD OK - output: WowHeadParser\bin\Release\WowHeadParser.exe
endlocal
exit /b 0
