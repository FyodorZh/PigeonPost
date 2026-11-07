# The MVVM Pattern — Avalonia

**Source URL:** https://docs.avaloniaui.net/docs/fundamentals/the-mvvm-pattern

## Overview

The Model-View-ViewModel (MVVM) pattern separates an application's user interface from its logic. Instead of mixing display code and behavior in the same file, MVVM splits them into three distinct parts that communicate through data binding.

## Architecture

```
View (AXAML) ←bindings→ ViewModel (C#) → Model (Services/Data)
```

### View
- Structure, layout, and appearance defined in AXAML files
- Minimal code-behind
- Gets data from the view model through bindings

### View Model
- Intermediary between View and Model
- Exposes data and commands for the View to bind to
- Handles user interaction logic
- Raises `PropertyChanged` events to notify the View of changes
- POCO class with no dependency on Avalonia

### Model
- Application's domain layer: data access, business logic, validation
- Examples: repositories, DTOs, service clients
- Has no knowledge of the ViewModel or View

## Key Principle: One-Way Dependency Chain

- View → ViewModel → Model (each layer only knows about the one below it)
- Model has no knowledge of ViewModel
- ViewModel has no knowledge of View
- This makes each layer independently testable and replaceable

## Why Use MVVM?

| Benefit | Description |
|---------|-------------|
| Testability | ViewModels can be unit tested like any class, without UI |
| Separation of concerns | UI layout and app logic evolve independently |
| Natural fit for XAML | Data binding provides the connection between layers |

## When to Use MVVM

| Strategy | Description |
|----------|-------------|
| Start with code-behind | Convert to MVVM if the app becomes hard to maintain |
| Start with MVVM | Use from the start if you expect the app to grow |

## Data Binding

Data binding is the key technology connecting Views to ViewModels:

- **Two-way binding**: Text inputs — changes in ViewModel update the control, user input flows back to ViewModel
- **One-way binding**: Button commands — only flow from View to ViewModel
- Because ViewModel has no reference to View or Avalonia types, it can be unit tested

## Model Layer Best Practice

- MVVM doesn't prescribe how to structure the Model layer
- Use **dependency injection** to provide model services to ViewModels rather than creating tight couplings
- Common model services: data storage, network services, business rules

## See Also

- [Code-behind pattern](https://docs.avaloniaui.net/docs/fundamentals/code-behind)
- [UI composition](https://docs.avaloniaui.net/docs/fundamentals/ui-composition)
- [Introduction to data binding](https://docs.avaloniaui.net/docs/data-binding/introduction-to-data-binding)
