@echo off
setlocal
set "ROOT=%~dp0ServerData"

if not exist "%ROOT%" (
    echo [x] ServerData not found: %ROOT%
    echo     Build the hotfix content in Unity first:  Tools / hotfix / build content
    pause
    exit /b 1
)

where node >nul 2>nul
if errorlevel 1 (
    echo [x] node not found. Install Node.js and make sure it is in PATH.
    pause
    exit /b 1
)

echo ============================================================
echo  Local hotfix server   (folder: %ROOT%)
echo.
echo  Local :  http://127.0.0.1:8000
echo  Phone :  use one of the IPv4 addresses below, e.g. http://^<IP^>:8000
echo           (allow port 8000 in the firewall; Android also needs HTTP allowed)
echo  Stop  :  close this window, or press Ctrl+C
echo ============================================================
ipconfig | findstr /i "IPv4"
echo.

node -e "const h=require('http'),f=require('fs'),p=require('path');const r=process.argv[1];h.createServer((q,s)=>{const u=decodeURIComponent(q.url.split('?')[0]);f.readFile(p.join(r,u),(e,d)=>e?(s.writeHead(404),s.end('404')):(s.writeHead(200),s.end(d)))}).listen(8000,'0.0.0.0',()=>console.log('listening on http://127.0.0.1:8000'))" "%ROOT%"

echo.
echo Server stopped.
pause
