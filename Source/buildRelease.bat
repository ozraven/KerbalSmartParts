
rem @echo off
set DEFHOMEDRIVE=d:
set DEFHOMEDIR=%DEFHOMEDRIVE%%HOMEPATH%
set HOMEDIR=
set HOMEDRIVE=%CD:~0,2%

set RELEASEDIR=d:\Users\jbb\release
set ZIP="c:\Program Files\7-zip\7z.exe"
echo Default homedir: %DEFHOMEDIR%

rem set /p HOMEDIR= "Enter Home directory, or <CR> for default: "

if "%HOMEDIR%" == "" (
set HOMEDIR=%DEFHOMEDIR%
)
echo %HOMEDIR%

SET _test=%HOMEDIR:~1,1%
if "%_test%" == ":" (
set HOMEDRIVE=%HOMEDIR:~0,2%
)


rem type SmartParts.version
rem set /p VERSION= "Enter version: "


set VERSIONFILE=SmartParts.version
rem The following requires the JQ program, available here: https://stedolan.github.io/jq/download/
c:\local\jq-win64  ".VERSION.MAJOR" %VERSIONFILE% >tmpfile
set /P major=<tmpfile

c:\local\jq-win64  ".VERSION.MINOR"  %VERSIONFILE% >tmpfile
set /P minor=<tmpfile

c:\local\jq-win64  ".VERSION.PATCH"  %VERSIONFILE% >tmpfile
set /P patch=<tmpfile

c:\local\jq-win64  ".VERSION.BUILD"  %VERSIONFILE% >tmpfile
set /P build=<tmpfile
del tmpfile
set VERSION=%major%.%minor%.%patch%
if "%build%" NEQ "0"  set VERSION=%VERSION%.%build%

echo %VERSION%

pause




mkdir %HOMEDIR%\install\GameData\SmartParts
mkdir %HOMEDIR%\install\GameData\SmartParts\Parts
mkdir %HOMEDIR%\install\GameData\SmartParts\Plugins
mkdir %HOMEDIR%\install\GameData\SmartParts\Sounds


del /Q %HOMEDIR%\install\GameData\SmartParts
del /Q %HOMEDIR%\install\GameData\SmartParts\Parts
del /Q %HOMEDIR%\install\GameData\SmartParts\Plugins
del /Q %HOMEDIR%\install\GameData\SmartParts\Sounds


copy /Y "%~dp0..\bin\Release\SmartParts.dll" "%HOMEDIR%\install\GameData\SmartParts\Plugins"

copy /Y "%~dp0SmartParts.version" "%HOMEDIR%\install\GameData\SmartParts"

xcopy /Y /S "%~dp0..\GameData\SmartParts\Parts" "%HOMEDIR%\install\GameData\SmartParts\Parts"


del %HOMEDIR%\install\GameData\SmartParts\Parts\Fuel-Breakers\*.tga
del %HOMEDIR%\install\GameData\SmartParts\Parts\Fuel-Controller\*.tga
del %HOMEDIR%\install\GameData\SmartParts\Parts\Smart-Controller\*.tga
del %HOMEDIR%\install\GameData\SmartParts\Parts\Valve\*.tga


copy /Y "%~dp0..\License.txt" "%HOMEDIR%\installv\GameData\SmartParts"
copy /Y "%~dp0..\README.md" "%HOMEDIR%\install\GameData\SmartParts"

copy /Y MiniAVC.dll  "%HOMEDIR%\install\GameData\SmartParts"

%HOMEDRIVE%
cd %HOMEDIR%\install

set FILE="%RELEASEDIR%\SmartParts-%VERSION%.zip"
IF EXIST %FILE% del /F %FILE%
%ZIP% a -tzip %FILE% Gamedata\SmartParts
