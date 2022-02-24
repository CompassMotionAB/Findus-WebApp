@echo off
setlocal
set "executable=FindusWebApp.exe"

cmd /c start /b ./%%executable%%
:: :dloop
:: tasklist|find "%executable%" >nul
:: IF ERRORLEVEL 1 call :timeout 1 >nul&goto dloop

start http://localhost:5000

pause
taskkill /im %executable%
exit

goto :eof
@goto :eof
:timeout
:: use ping to delay for the number of seconds passed as an argument

@ping -n %1 127.0.0.1 > nul
@ping -n 2 127.0.0.1 > nul
@goto :eof
