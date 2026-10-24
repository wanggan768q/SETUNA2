@echo off
rem SETUNA Avalonia 版打包脚本（期 6）：PublishSingleFile 替代 Costura.Fody
echo 正在清理之前的构建...
if exist "publish-framework" rmdir /s /q "publish-framework"
if exist "publish-self-contained" rmdir /s /q "publish-self-contained"

echo.
echo 正在构建框架依赖版本...
dotnet publish SETUNA.Avalonia/SETUNA.Avalonia.csproj --configuration Release --output ./publish-framework --self-contained false --runtime win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true 

echo.
echo 正在构建自包含版本...
dotnet publish SETUNA.Avalonia/SETUNA.Avalonia.csproj --configuration Release --output ./publish-self-contained --self-contained true --runtime win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true 

echo.
echo 正在检查文件大小...
echo 框架依赖版本：
dir "publish-framework\SETUNA.Avalonia.exe" | findstr SETUNA.Avalonia.exe

echo.
echo 自包含版本：
dir "publish-self-contained\SETUNA.Avalonia.exe" | findstr SETUNA.Avalonia.exe

echo.
echo 正在创建ZIP包...
powershell -Command "Compress-Archive -Path 'publish-framework/*' -DestinationPath 'SETUNA.Avalonia-framework-win-x64.zip' -Force"
powershell -Command "Compress-Archive -Path 'publish-self-contained/*' -DestinationPath 'SETUNA.Avalonia-selfcontained-win-x64.zip' -Force"

echo.
echo ZIP包大小：
dir "SETUNA.Avalonia-framework-win-x64.zip" | findstr framework
dir "SETUNA.Avalonia-selfcontained-win-x64.zip" | findstr selfcontained

echo.
echo 构建完成！
pause
