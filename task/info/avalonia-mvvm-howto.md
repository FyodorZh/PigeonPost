# How to: Implement Common MVVM Patterns — Avalonia

**Source URL:** https://docs.avaloniaui.net/docs/how-to/mvvm-how-to

## Overview

Practical MVVM patterns for Avalonia using `CommunityToolkit.Mvvm`, the recommended MVVM framework.

## Setup

```bash
dotnet add package CommunityToolkit.Mvvm
```

Uses source generators and base classes to eliminate boilerplate code.

## Observable Properties

Use `[ObservableProperty]` attribute on private fields; source generator creates public properties with `INotifyPropertyChanged`:

```csharp
public partial class PersonViewModel : ObservableObject
{
    [ObservableProperty]
    private string _firstName = "";

    [ObservableProperty]
    private string _lastName = "";
}
```

Generated: `FirstName` and `LastName` properties. Class must be `partial`.

### Computed Properties

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FullName))]
private string _firstName = "";

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FullName))]
private string _lastName = "";

public string FullName => $"{FirstName} {LastName}";
```

### Property Changed Callbacks

```csharp
[ObservableProperty]
private string _searchText = "";

partial void OnSearchTextChanged(string value)  // After change
{
    ApplyFilter(value);
}

partial void OnSearchTextChanging(string value)  // Before change
{
}
```

## Commands

### Basic Command

```csharp
[RelayCommand]
private void Save()
{
    _repository.Save(CurrentItem);
}
```

Generates `SaveCommand` property. Naming: method name + "Command".

### Command with Parameter

```csharp
[RelayCommand]
private void Delete(Item item)
{
    Items.Remove(item);
}
```

```xml
<Button Content="Delete"
        Command="{Binding DeleteCommand}"
        CommandParameter="{Binding SelectedItem}" />
```

### Async Command

```csharp
[RelayCommand]
private async Task LoadDataAsync(CancellationToken token)
{
    IsLoading = true;
    var data = await _api.FetchDataAsync(token);
    Items = new ObservableCollection<Item>(data);
    IsLoading = false;
}
```

Generated command automatically:
- Disables the button while running
- Passes a `CancellationToken`
- Exposes `IsRunning` property

```xml
<Button Content="Load" Command="{Binding LoadDataCommand}" />
<ProgressBar IsVisible="{Binding LoadDataCommand.IsRunning}" />
```

### CanExecute

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
private string _name = "";

[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { }

private bool CanSave() => !string.IsNullOrWhiteSpace(Name);
```

## ViewModel Communication

### WeakReferenceMessenger

```csharp
// Define message
public record ItemSelectedMessage(Item Item);

// Send
WeakReferenceMessenger.Default.Send(new ItemSelectedMessage(item));

// Receive
public class DetailViewModel : ObservableRecipient, IRecipient<ItemSelectedMessage>
{
    public DetailViewModel() => IsActive = true;

    public void Receive(ItemSelectedMessage message)
    {
        LoadItem(message.Item);
    }
}
```

### Request/Response Pattern

```csharp
public record ConfirmDeleteRequest(Item Item);

// Request
var confirmed = WeakReferenceMessenger.Default.Send(new ConfirmDeleteRequest(item));

// Response handler
WeakReferenceMessenger.Default.Register<ConfirmDeleteRequest>(this, async (r, m) =>
{
    m.Reply(await ShowConfirmDialogAsync());
});
```

## Dependency Injection

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<IDataService, DataService>();
        return services;
    }
}
```

Wire up in `App.axaml.cs`:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var services = new ServiceCollection();
    services.AddViewModels();
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

- **`AddTransient`**: new instance each time (for ViewModels)
- **`AddSingleton`**: shared instance (for services)

## Constructor Injection

```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly INavigationService _navigation;

    public MainViewModel(IDataService dataService, INavigationService navigation)
    {
        _dataService = dataService;
        _navigation = navigation;
    }
}
```

The container resolves all dependencies automatically when registered.

## ObservableCollection Patterns

### Replace vs Add

- **Slow**: `Items.Add(item)` per item — triggers UI update on each add
- **Fast**: `Items = new ObservableCollection<Item>(newItems)` — single notification

### Filtered Collection

```csharp
[ObservableProperty]
private string _filter = "";

[ObservableProperty]
private ObservableCollection<Item> _filteredItems = new();

partial void OnFilterChanged(string value)
{
    FilteredItems = new ObservableCollection<Item>(
        _allItems.Where(i => i.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
}
```

## Validation

```csharp
public partial class RegisterViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required")]
    private string _name = "";

    [RelayCommand]
    private void Submit()
    {
        ValidateAllProperties();
        if (!HasErrors)
        {
            // Proceed
        }
    }
}
```

The `[NotifyDataErrorInfo]` attribute triggers validation automatically on property change. Avalonia's `DataValidationErrors` control displays these errors.

## See Also

- [The MVVM pattern](https://docs.avaloniaui.net/docs/fundamentals/the-mvvm-pattern)
- [Binding to commands](https://docs.avaloniaui.net/docs/data-binding/binding-to-commands)
- [INotifyPropertyChanged](https://docs.avaloniaui.net/docs/data-binding/inotifypropertychanged)
- [Dependency injection](https://docs.avaloniaui.net/docs/app-development/dependency-injection)
- [Validation in data binding](https://docs.avaloniaui.net/docs/data-binding/binding-validation)
