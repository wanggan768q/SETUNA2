# AGENTS.md

This file contains information for agentic coding assistants working on SETUNA repository.

**重要提示：之后的回答都使用中文**

## Build Commands

```bash
# Build main project
dotnet build SETUNA/SETUNA.csproj

# Build release mode
dotnet build SETUNA/SETUNA.csproj --configuration Release

# Clean rebuild
dotnet clean SETUNA/SETUNA.csproj && dotnet build SETUNA/SETUNA.csproj

# Build optimized production version
build-optimized.bat

# Build tests
dotnet build SETUNATests/SETUNATests.csproj
```

## Test Commands

```bash
# Run all tests
dotnet test SETUNATests/SETUNATests.csproj

# Run with verbose output
dotnet test SETUNATests/SETUNATests.csproj --verbosity detailed

# Run single test (exact match)
dotnet test --filter "FullyQualifiedName~BasicTest_ShouldPass"

# Run tests in a class
dotnet test --filter "FullyQualifiedName~LayerManagerTests"

# Run tests containing keyword
dotnet test --filter "FullyQualifiedName~Layer"
```

## Project Structure

- **SETUNA/** - Main app (Windows Forms/WPF, .NET 9.0-windows)
- **SETUNATests/** - MSTest test project
- **Target**: .NET 9.0-windows, x64
- **Key deps**: SixLabors.ImageSharp, Svg, Newtonsoft.Json, WebP-wrapper, Moq

## Code Style

### Naming
- **Interfaces**: `IPrefixed`
- **Classes/Methods/Properties**: PascalCase
- **Fields/locals**: camelCase
- **Constants**: PascalCase
- **Private static fields**: `_prefix`

### Imports & Formatting
- System directives first (alpha sorted), then external, then local
- 4 spaces, no tabs; CRLF line endings
- New line before open braces for all constructs
- Space after commas and around binary operators
- No trailing newlines; preserve single-line blocks

### Types & Patterns
- Prefer `var` when type is apparent; use `int`/`string` not `Int32`/`String`
- Null propagation: `?.` and `??` operators
- Expression-bodied members preferred for properties and simple methods
- Auto-properties preferred over backing fields
- Singleton: `public static readonly Instance = new();`
- Extension methods marked with `this` in first parameter

### Error Handling & Async
- Use `using` for disposable objects; try-catch-finally for cleanup
- Don't swallow exceptions without logging
- `async void` only for event handlers; prefer `async Task`
- Use `await File.WriteAllBytesAsync` for async file I/O
- Chain async operations properly

### Key Patterns
```csharp
// Resource management
using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) { }

// Singleton
public class LayerManager {
    public static readonly LayerManager Instance = new();
    private LayerManager() { }
}

// Event handling
WindowManager.WindowActived += WindowManager_WindowActived;

// Null checking
if (formDic.TryGetValue(form.Handle, out var formData)) { }

// LINQ sorting
sortingFormDatas.Sort((x, y) => x.SortingOrder.CompareTo(y.SortingOrder));
```

### Testing
- Use `[TestClass]`/`[TestMethod]` with MSTest
- Mark with `[SupportedOSPlatform("windows")]` when needed
- Naming: `Method_Scenario_ExpectedResult`
- Use Moq for mocking; keep tests simple and focused

### Important Notes
- Use Costura.Fody for DLL embedding in release builds
- Set DPI awareness for Windows < build 14393
- Singleton application pattern enforced
- Hotkey registration is critical
- Layer management has complex Z-order handling

### Debugging
- Use `#if DEBUG` for debug-only code
- Console.WriteLine for basic logging; consider Debug.WriteLine

### NoWarn Config
- `CA1416` - Platform compatibility
- `SYSLIB0014`, `SYSLIB0021`, `SYSLIB0006` - Obsolete APIs
- `WFO1000` - Windows Forms warnings

### Performance
- Use `List<T>` with capacity hints
- Optimize layer counter when > 1000
- Cache frequently accessed data
- Lazy initialization where appropriate
