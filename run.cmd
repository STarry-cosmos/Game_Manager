@echo off
rem ============================================================
rem  Game Manager 启动脚本
rem  本文件编码为 GBK / CP936，请勿用 UTF-8 另存（VS Code、
rem  Win11 记事本默认存 UTF-8，会导致中文乱码与命令解析错位）。
rem  也不要添加 chcp 65001 —— 会让 cmd 按字节解析时错位。
rem ============================================================
setlocal
title Game Manager 启动器
cd /d "%~dp0"

set "SLN=Game_Manager.sln"
set "APPNAME=Game_Manager.exe"
set "CONFIG=Debug"
set "DO_BUILD=1"
set "DO_RUN=1"

:parse_args
if "%~1"=="" goto args_done
if /i "%~1"=="-r" goto opt_release
if /i "%~1"=="-b" goto opt_buildonly
if /i "%~1"=="-n" goto opt_nobuild
if /i "%~1"=="-h" goto show_help
if /i "%~1"=="--help" goto show_help
if /i "%~1"=="/?" goto show_help
echo [提示] 未知参数 "%~1"，已忽略
shift
goto parse_args

:opt_release
set "CONFIG=Release"
shift
goto parse_args

:opt_buildonly
set "DO_RUN=0"
shift
goto parse_args

:opt_nobuild
set "DO_BUILD=0"
shift
goto parse_args

:args_done
echo ============================================================
echo   Game Manager  ^|  配置:%CONFIG%  编译:%DO_BUILD%  启动:%DO_RUN%
echo ============================================================
echo.

rem ---------- 检查 dotnet ----------
dotnet --version >nul 2>&1
if errorlevel 1 goto no_dotnet

rem ---------- 关闭旧实例，避免 exe 被占用导致编译失败 ----------
tasklist /FI "IMAGENAME eq %APPNAME%" /NH 2>nul | findstr /I /C:"%APPNAME%" >nul
if errorlevel 1 goto no_old_instance
echo [准备] 检测到正在运行的 Game Manager，先关闭旧实例...
taskkill /IM %APPNAME% /F >nul 2>&1
ping -n 2 127.0.0.1 >nul
goto after_kill

:no_old_instance
echo [准备] 没有运行中的实例

:after_kill

rem ---------- 编译 ----------
if "%DO_BUILD%"=="0" goto skip_build
echo [编译] dotnet build -c %CONFIG%
echo.
dotnet build "%SLN%" -c %CONFIG% --nologo -v m
if errorlevel 1 goto build_failed
echo.
goto after_build

:skip_build
echo [跳过] 按参数要求不执行编译

:after_build
if "%DO_RUN%"=="0" goto build_only_done

rem ---------- 定位并启动 exe ----------
set "EXE="
for /f "delims=" %%F in ('dir /b /s "%CD%\bin\%CONFIG%\net8.0-windows\%APPNAME%" 2^>nul') do set "EXE=%%F"
if not defined EXE goto exe_missing

echo [启动] %EXE%
start "" "%EXE%"
echo.
echo [完成] 已启动，本窗口可以直接关闭
goto the_end_ok

:build_only_done
echo [完成] 编译通过（按参数要求未启动）
goto the_end_ok

rem ==================== 错误分支 ====================

:no_dotnet
echo [错误] 没有找到 dotnet 命令，请先安装 .NET 8 SDK
echo        下载地址: https://dotnet.microsoft.com/download/dotnet/8.0
goto the_end_fail

:build_failed
echo.
echo [失败] 编译未通过，请查看上方错误信息
goto the_end_fail

:exe_missing
echo [失败] 编译已完成，但找不到 %APPNAME%
echo        预期位置: bin\%CONFIG%\net8.0-windows\
goto the_end_fail

:show_help
echo 用法: run.cmd [选项]
echo.
echo   (无参数)   以 Debug 编译并启动
echo   -r         改用 Release 配置
echo   -b         只编译，不启动（用于验证能否编译通过）
echo   -n         跳过编译，直接启动（改完配置快速重启）
echo   -h         显示本帮助
echo.
echo 示例:
echo   run.cmd          日常开发：编译并启动
echo   run.cmd -b       只检查编译是否通过
echo   run.cmd -r       以 Release 配置编译并启动
goto the_end_ok

:the_end_ok
endlocal
exit /b 0

:the_end_fail
echo.
pause
endlocal
exit /b 1
