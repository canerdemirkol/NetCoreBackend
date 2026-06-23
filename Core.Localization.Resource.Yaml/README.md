# Core.Localization.Resource.Yaml

YAML file based localization implementation. Includes lazy-loading, fallback, and section support.

## File Structure

```
Features/
└── Users/
    └── Resources/
        └── Locales/
            ├── Users.tr.yaml
            ├── Users.en.yaml
            └── Users.de.yaml
```

## YAML Format

```yaml
# Users.tr.yaml
UserNotFound: "Kullanıcı bulunamadı."
InvalidPassword: "Geçersiz şifre."
EmailAlreadyExists: "Bu email zaten kayıtlı."
```

## Behavior

- On first access, the YAML file is loaded and held in memory (lazy-loading)
- If the requested locale is not found, it falls back to the default locale (`en`)
- If the key is not found in any locale, the key itself is returned
- The `section` parameter specifies the YAML file name

## DI Registration

Use the [`Core.Localization.Resource.Yaml.DependencyInjection`](../Core.Localization.Resource.Yaml.DependencyInjection/README.md) project.

```csharp
builder.Services.AddYamlLocalization();
```
