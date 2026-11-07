# Implementation Guides — Avalonia Development

**Source URL:** https://docs.avaloniaui.net/docs/guides/implementation-guides/

## Overview

The implementation guides section of the Avalonia documentation is the central hub for practical development patterns. It covers Dependency Injection, logging, error handling, developer tools setup, and related infrastructure topics.

## Key Areas (Based on Documentation Structure)

### Dependency Injection

Avalonia integrates with standard .NET DI patterns. The recommended approach:

1. Use `Microsoft.Extensions.DependencyInjection` or `CommunityToolkit.Mvvm`
2. Register services and ViewModels in `App.axaml.cs`
3. Inject via constructor into ViewModels
4. Use `AddTransient` for ViewModels, `AddSingleton` for services

### Logging

Avalonia supports structured logging. Recommended setup:

- Use a logging framework like Serilog or Microsoft.Extensions.Logging
- Configure logging in the application entry point
- Log errors and diagnostics from ViewModel operations
- Use `Scriba` if following PigeonPost conventions (structured JSON)

### Error Handling

Patterns for robust Avalonia applications:

- Global exception handling via `AppDomain.CurrentDomain.UnhandledException`
- Try/catch in async commands with user-visible error reporting
- Validation via `DataValidationErrors` on input controls
- Error logging with context information

### Developer Tools

Built-in runtime diagnostic tools:

- **F12 DevTools**: Press F12 at runtime to inspect:
  - Visual tree (element hierarchy)
  - Properties (current values of all properties)
  - Styles (matching selectors and applied values)
  - Layout (measure/arrange information)
- Enable DevTools by adding `.UseReactiveUI()` or enabling in startup
- Accessible in debug builds by default

## Implementation Guidance

### Setting Up DI in App.axaml.cs

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var services = new ServiceCollection();
    services.AddSingleton<IDataService, DataService>();
    services.AddTransient<MainViewModel>();
    var provider = services.BuildServiceProvider();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow
        {
            DataContext = provider.GetRequiredService<MainViewModel>()
        };
    }
    base.OnFrameworkInitializationCompleted();
}
```

### Recommended Project Structure

```
MyApp/
├── Models/           # Domain data
├── ViewModels/       # MVVM ViewModels
├── Views/            # AXAML Views
├── Services/         # DI services
├── App.axaml         # Application definition
├── App.axaml.cs      # DI wiring, startup
└── Program.cs        # Entry point
```

## See Also

- [MVVM pattern](https://docs.avaloniaui.net/docs/fundamentals/the-mvvm-pattern)
- [App development](https://docs.avaloniaui.net/docs/app-development/cross-platform-solution-setup)
- [DevTools](https://docs.avaloniaui.net/tools/developer-tools/installation)
