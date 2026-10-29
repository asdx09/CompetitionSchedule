@echo off
rem Dupla kattintással indítható: elindítja a PHP backendet a 7098-as porton (HTTPS).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-dev.ps1"
pause
