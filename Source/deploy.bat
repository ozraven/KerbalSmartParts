cd Source
mkdir R:\KSP_1.0.5_Dev\GameData\SmartParts
mkdir R:\KSP_1.0.5_Dev\GameData\SmartParts\Parts
mkdir R:\KSP_1.0.5_Dev\GameData\SmartParts\Plugins
mkdir R:\KSP_1.0.5_Dev\GameData\SmartParts\Sounds


copy /Y "%~dp0..\bin\Debug\SmartParts.dll" "R:\KSP_1.0.5_Dev\GameData\SmartParts\Plugins"

copy /Y "%~dp0SmartParts.version" "R:\KSP_1.0.5_Dev\GameData\SmartParts"


xcopy /Y /S "%~dp0..\GameData\SmartParts\Parts" "R:\KSP_1.0.5_Dev\GameData\SmartParts\Parts"


copy /Y "%~dp0..\License.txt" "R:\KSP_1.0.5_Dev\GameData\SmartParts"
copy /Y "%~dp0..\README.md" "R:\KSP_1.0.5_Dev\GameData\SmartParts"
