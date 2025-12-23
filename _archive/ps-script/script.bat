@echo off
setlocal

set "exe_in=%~1"
set "ico_out=%~2"

set "psCommand="[void][Reflection.Assembly]::LoadWithPartialName('System.Drawing');^
[Drawing.Icon]::ExtractAssociatedIcon(\"%exe_in%\").ToBitmap().Save(\"%ico_out%\")""

powershell -noprofile -noninteractive %psCommand%