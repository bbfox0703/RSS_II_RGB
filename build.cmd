@echo off
setlocal EnableDelayedExpansion

:: ============================================================
:: build.cmd -- RSS_II_RGB build wrapper (.NET 10 / Avalonia)
::
:: NOTE: builds are ALWAYS clean -- bin/obj are removed first.
::
:: Usage:
::   build                 Build Release (all projects + tests)
::   build debug           Debug build
::   build release         Release build
::   build publish         Native AOT publish into dist\
::   build app             Build the app only (no tests)
::   build test            Run unit tests only
::   build clean           Also remove dist\
::   build publish clean   Clean + publish
:: ============================================================

set "MODE=Release"
set "TARGET=All"
set "CLEAN="
set "HAS_ARGS=0"

:parse_args
if "%~1"=="" goto :run
set "HAS_ARGS=1"

set "ARG=%~1"
for %%A in ("%ARG%") do set "UPPER=%%~A"
call :to_upper UPPER

if "!UPPER!"=="DEBUG"   ( set "MODE=Debug"   & shift & goto :parse_args )
if "!UPPER!"=="RELEASE" ( set "MODE=Release" & shift & goto :parse_args )
if "!UPPER!"=="PUBLISH" ( set "MODE=Publish" & shift & goto :parse_args )
if "!UPPER!"=="CLEAN"   ( set "CLEAN=-Clean" & shift & goto :parse_args )
if "!UPPER!"=="ALL"     ( set "TARGET=All"   & shift & goto :parse_args )
if "!UPPER!"=="APP"     ( set "TARGET=App"   & shift & goto :parse_args )
if "!UPPER!"=="TEST"    ( set "TARGET=Test"  & shift & goto :parse_args )
if "!UPPER!"=="/?"      goto :usage
if "!UPPER!"=="-H"      goto :usage
if "!UPPER!"=="--HELP"  goto :usage

echo.
echo  ERROR: Unknown argument '%~1'
goto :usage_error

:run
set "LOG=%~dp0build_log.txt"

echo.
echo  RSS_II_RGB Build
echo  Mode: %MODE%  Target: %TARGET%  Clean: %CLEAN%
echo  Log:  %LOG%

if "!HAS_ARGS!"=="0" (
    echo  Hint: No arguments -- using defaults. Available options:
    echo    build debug          Debug build
    echo    build app            App only
    echo    build test           Run tests
    echo    build publish        Native AOT into dist\
    echo    build clean          Clean first
    echo    build --help         Full usage
)
echo.

:: -File avoids encoding issues at the cmd -> PowerShell boundary.
:: Logging is handled inside build.ps1 via Start-Transcript.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Mode %MODE% -Target %TARGET% %CLEAN% -LogFile "%LOG%"
set "EC=%ERRORLEVEL%"

if %EC% neq 0 (
    echo.
    echo  BUILD FAILED [exit code %EC%] -- see %LOG%
    echo.
) else (
    echo.
    echo  BUILD SUCCEEDED -- log: %LOG%
    echo.
)

exit /b %EC%

:usage_error
call :print_usage
exit /b 1

:usage
call :print_usage
exit /b 0

:print_usage
echo.
echo  Usage: build [mode] [target] [options]
echo.
echo  Modes:
echo    debug       Unoptimized, debug symbols (fast iteration)
echo    release     Optimized build (default)
echo    publish     Native AOT trimmed exe into dist\ (distribution)
echo.
echo  Targets:
echo    all         Build everything + run tests (default)
echo    app         Build the app only
echo    test        Run unit tests only
echo.
echo  Options:
echo    clean       Also remove dist\ first
echo.
echo  Examples:
echo    build                   Release build, all projects + tests
echo    build debug             Debug build
echo    build publish clean     Clean + Native AOT publish
echo    build test              Run tests
echo.
goto :eof

:to_upper
for %%a in (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (
    set "%1=!%1:%%a=%%a!"
)
goto :eof
