# Core.Localization.Resource.Yaml

YAML dosyası tabanlı lokalizasyon implementasyonu. Lazy-loading, fallback ve section desteği içerir.

## Dosya Yapısı

```
Features/
└── Users/
    └── Resources/
        └── Locales/
            ├── Users.tr.yaml
            ├── Users.en.yaml
            └── Users.de.yaml
```

## YAML Formatı

```yaml
# Users.tr.yaml
UserNotFound: "Kullanıcı bulunamadı."
InvalidPassword: "Geçersiz şifre."
EmailAlreadyExists: "Bu email zaten kayıtlı."
```

## Davranış

- İlk erişimde YAML dosyası yüklenir ve bellekte tutulur (lazy-loading)
- İstenen locale bulunamazsa varsayılan locale'e (`en`) düşer
- Key hiçbir locale'de bulunamazsa key'in kendisi döner
- `section` parametresi YAML dosya adını belirtir

## DI Kaydı

[`Core.Localization.Resource.Yaml.DependencyInjection`](../Core.Localization.Resource.Yaml.DependencyInjection/README.md) projesini kullanın.

```csharp
builder.Services.AddYamlLocalization();
```
