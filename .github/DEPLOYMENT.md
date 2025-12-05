# GitHub Actions 自动化部署指南

SETUNA2 项目现已配置完整的 GitHub Actions 自动化发布流程，支持版本管理、自动构建、打包和发布。

## 🚀 工作流程概览

### 1. 持续集成 (CI)
- **触发条件**: 推送到 `main`/`ConvertNetCore`/`develop` 分支，或创建 Pull Request
- **文件**: `.github/workflows/ci.yml`
- **功能**: 
  - 自动构建项目
  - 运行单元测试
  - 代码覆盖率报告
  - 缓存 NuGet 包以提高构建速度

### 2. 版本自动管理
- **触发条件**: 推送到主分支（非标签推送）
- **文件**: `.github/workflows/version-update.yml`
- **功能**:
  - 根据提交信息自动判断版本类型
  - 更新 AssemblyInfo.cs 和 .csproj 中的版本号
  - 自动创建版本标签

### 3. 正式发布
- **触发条件**: 推送版本标签 (如 `v4.0.1`)
- **文件**: `.github/workflows/release.yml`
- **功能**:
  - 构建 Release 配置
  - 生成独立的 exe 可执行文件
  - 创建 ZIP 压缩包
  - 自动创建 GitHub Release
  - 上传构建产物

### 4. 预发布构建
- **触发条件**: 手动触发 (workflow_dispatch)
- **文件**: `.github/workflows/prerelease.yml`
- **功能**:
  - 构建预发布版本
  - 支持自定义版本后缀
  - 创建预发布标签

## 📝 使用方法

### 方法一：自动发布 (推荐)

1. **提交代码并自动发布**
   ```bash
   # 提交新功能，会自动创建 minor 版本
   git commit -m "feat: 添加新的截图编辑功能"
   git push origin main
   
   # 提交修复，会自动创建 patch 版本
   git commit -m "fix: 修复高分屏截图模糊问题"
   git push origin main
   ```

2. **版本号规则**
   - `feat`, `feature`, `新增`, `功能` → Minor 版本 (x.y+1.0)
   - `fix`, `bugfix`, `修复`, `问题` → Patch 版本 (x.y.z+1)
   - 其他 → Patch 版本

### 方法二：使用辅助脚本

1. **使用 PowerShell 脚本**
   ```powershell
   # 查看当前版本
   .\scripts\release-helper.ps1 current
   
   # 创建补丁版本
   .\scripts\release-helper.ps1 patch "修复了截图工具栏显示问题"
   
   # 创建次要版本
   .\scripts\release-helper.ps1 minor "新增批量处理功能"
   
   # 创建主要版本
   .\scripts\release-helper.ps1 major "重构整个UI界面"
   ```

2. **推送更改和标签**
   ```bash
   git push origin main
   git push origin v4.0.1  # 推送标签触发发布
   ```

### 方法三：手动发布

1. **手动创建标签**
   ```bash
   git tag -a v4.0.1 -m "Release version 4.0.1"
   git push origin v4.0.1
   ```

2. **手动触发预发布**
   - 访问 GitHub 项目的 Actions 页面
   - 选择 "Prerelease Build" workflow
   - 点击 "Run workflow"
   - 填写版本后缀和发布说明

## 🔧 配置说明

### 项目配置文件

项目已配置以下关键设置：

1. **SETUNA.csproj**
   - `<OutputType>WinExe</OutputType>`: 生成 Windows 可执行文件
   - `<TargetFramework>net9.0-windows</TargetFramework>`: 目标框架
   - `<SelfContained>true</SelfContained>`: 独立部署
   - `<PublishSingleFile>true</PublishSingleFile>`: 单文件发布

2. **GitHub Secrets**
   - `GITHUB_TOKEN`: 自动提供，用于创建 Release
   - 无需额外配置

### 构建配置

- **运行环境**: Windows Server 2022 (windows-latest)
- **.NET 版本**: 9.0.x
- **目标平台**: win-x64
- **发布模式**: Release
- **输出格式**: 单文件独立可执行程序

## 📦 产物说明

每次发布会生成以下文件：

1. **GitHub Release**
   - 自动生成发布说明
   - 包含下载链接
   - 支持预发布版本

2. **ZIP 包**
   - 名称: `SETUNA-{version}-win-x64.zip`
   - 内容: 完整的可执行程序及依赖
   - 无需安装 .NET Runtime

3. **Artifacts**
   - GitHub Actions 中保存的构建产物
   - 保留期: 正式版本 30 天，预发布版本 7 天

## 🐛 故障排除

### 常见问题

1. **构建失败**
   - 检查项目是否能正常编译
   - 确认所有 NuGet 包都已恢复
   - 查看 Actions 日志获取详细错误信息

2. **版本号冲突**
   - 确保没有重复的标签
   - 使用 `git tag -l` 查看现有标签

3. **发布失败**
   - 检查 GitHub Token 权限
   - 确认仓库设置允许创建 Release

### 调试步骤

1. 查看 Actions 日志
2. 本地验证构建命令
3. 检查版本号格式
4. 验证 Git 配置

## 📅 最佳实践

1. **版本管理**
   - 使用语义化版本控制 (SemVer)
   - 及时更新 README 中的版本信息
   - 保持 CHANGELOG 更新

2. **提交信息**
   - 使用清晰的提交前缀 (feat/fix/docs 等)
   - 提交信息要描述具体改动
   - 避免在提交中包含敏感信息

3. **发布流程**
   - 优先使用自动化流程
   - 预发布版本充分测试
   - 及时回应有用的 Issue 和 PR

## 📞 获取帮助

如果遇到问题：

1. 查看 [GitHub Actions](https://github.com/tylearymf/SETUNA2/actions) 页面
2. 检查 [Issues](https://github.com/tylearymf/SETUNA2/issues) 中是否有类似问题
3. 创建新的 Issue 并提供详细的错误信息
4. 查看 [GitHub Actions 文档](https://docs.github.com/en/actions)

---

*最后更新: 2024-12-05*