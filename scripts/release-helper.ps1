# SETUNA 发布辅助脚本
# 使用方法: .\scripts\release-helper.ps1 [命令] [参数]

param(
    [Parameter(Position=0, Mandatory=$false)]
    [ValidateSet("patch", "minor", "major", "current", "help")]
    [string]$Command = "help",
    
    [Parameter(Position=1, Mandatory=$false)]
    [string]$Notes = ""
)

# 项目信息
$ProjectPath = "SETUNA\SETUNA.csproj"
$AssemblyInfoPath = "SETUNA\Properties\AssemblyInfo.cs"

# 获取当前版本
function Get-CurrentVersion {
    if (Test-Path $ProjectPath) {
        $content = Get-Content $ProjectPath
        $versionLine = $content | Where-Object { $_ -match "<AssemblyVersion>([^<]+)</AssemblyVersion>" }
        if ($versionLine) {
            if ($versionLine -match ">(\d+\.\d+\.\d+)") {
                return $matches[1]
            }
        }
    }
    
    if (Test-Path $AssemblyInfoPath) {
        $content = Get-Content $AssemblyInfoPath
        $versionLine = $content | Where-Object { $_ -match "AssemblyVersion\(`"([^`"]+)`"\)" }
        if ($versionLine) {
            if ($versionLine -match "`"(\d+\.\d+\.\d+)`"") {
                return $matches[1]
            }
        }
    }
    
    return "4.0.0"
}

# 更新版本号
function Update-Version {
    param(
        [string]$NewVersion
    )
    
    Write-Host "正在更新版本号到: $NewVersion" -ForegroundColor Green
    
    # 更新.csproj文件
    if (Test-Path $ProjectPath) {
        $content = Get-Content $ProjectPath
        $content = $content -replace "<AssemblyVersion>[^<]+</AssemblyVersion>", "<AssemblyVersion>$NewVersion.0</AssemblyVersion>"
        $content = $content -replace "<AssemblyInformationalVersion>[^<]+</AssemblyInformationalVersion>", "<AssemblyInformationalVersion>$NewVersion</AssemblyInformationalVersion>"
        $content | Set-Content $ProjectPath
        Write-Host "✓ 已更新 $ProjectPath" -ForegroundColor Green
    }
    
    # 更新AssemblyInfo.cs文件
    if (Test-Path $AssemblyInfoPath) {
        $content = Get-Content $AssemblyInfoPath
        $content = $content -replace '\[assembly: AssemblyVersion\("[^"]+"\)\]', "[assembly: AssemblyVersion(`"$NewVersion.0`")]"
        $content = $content -replace '\[assembly: AssemblyFileVersion\("[^"]+"\)\]', "[assembly: AssemblyFileVersion(`"$NewVersion.0`")]"
        $content | Set-Content $AssemblyInfoPath
        Write-Host "✓ 已更新 $AssemblyInfoPath" -ForegroundColor Green
    }
}

# 创建发布标签
function Create-ReleaseTag {
    param(
        [string]$Version,
        [string]$ReleaseNotes = ""
    )
    
    $tagName = "v$Version"
    
    Write-Host "正在创建发布标签: $tagName" -ForegroundColor Yellow
    
    # 提交版本更改
    git add $ProjectPath $AssemblyInfoPath
    git commit -m "🔖 Bump version to $tagName"
    
    # 创建标签
    git tag -a $tagName -m "Release $tagName"
    
    if ($ReleaseNotes) {
        Write-Host "发布说明: $ReleaseNotes" -ForegroundColor Cyan
    }
    
    Write-Host "请运行以下命令推送更改和标签:" -ForegroundColor Yellow
    Write-Host "git push origin main" -ForegroundColor Gray
    Write-Host "git push origin $tagName" -ForegroundColor Gray
}

# 主逻辑
switch ($Command) {
    "current" {
        $currentVersion = Get-CurrentVersion
        Write-Host "当前版本: v$currentVersion" -ForegroundColor Green
    }
    
    "patch" {
        $current = Get-CurrentVersion
        if ($current -match "(\d+)\.(\d+)\.(\d+)") {
            $newVersion = "$($matches[1]).$($matches[2]).$([int]$matches[3] + 1)"
            Update-Version $newVersion
            Create-ReleaseTag $newVersion $Notes
        } else {
            Write-Host "无法解析当前版本号" -ForegroundColor Red
        }
    }
    
    "minor" {
        $current = Get-CurrentVersion
        if ($current -match "(\d+)\.(\d+)\.(\d+)") {
            $newVersion = "$($matches[1]).$([int]$matches[2] + 1).0"
            Update-Version $newVersion
            Create-ReleaseTag $newVersion $Notes
        } else {
            Write-Host "无法解析当前版本号" -ForegroundColor Red
        }
    }
    
    "major" {
        $current = Get-CurrentVersion
        if ($current -match "(\d+)\.(\d+)\.(\d+)") {
            $newVersion = "$([int]$matches[1] + 1).0.0"
            Update-Version $newVersion
            Create-ReleaseTag $newVersion $Notes
        } else {
            Write-Host "无法解析当前版本号" -ForegroundColor Red
        }
    }
    
    "help" {
        Write-Host "SETUNA 发布辅助脚本" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "使用方法:" -ForegroundColor Yellow
        Write-Host "  .\scripts\release-helper.ps1 <命令> [发布说明]" -ForegroundColor Gray
        Write-Host ""
        Write-Host "可用命令:" -ForegroundColor Yellow
        Write-Host "  current  - 显示当前版本号" -ForegroundColor Gray
        Write-Host "  patch    - 创建补丁版本 (x.y.z+1)" -ForegroundColor Gray
        Write-Host "  minor    - 创建次要版本 (x.y+1.0)" -ForegroundColor Gray
        Write-Host "  major    - 创建主要版本 (x+1.0.0)" -ForegroundColor Gray
        Write-Host "  help     - 显示此帮助信息" -ForegroundColor Gray
        Write-Host ""
        Write-Host "示例:" -ForegroundColor Yellow
        Write-Host "  .\scripts\release-helper.ps1 patch" -ForegroundColor Gray
        Write-Host "  .\scripts\release-helper.ps1 minor ""修复了截图模糊问题""" -ForegroundColor Gray
        Write-Host ""
    }
}