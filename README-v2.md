# 📦 StreamBG — VOD система + Мобилно приложение (.NET MAUI)

## Съдържание на тази версия

```
StreamBG-v2/
├── vod/
│   ├── VodEntities.cs          ← Domain модели: VodVideo, VodChapter, WatchProgress
│   ├── VodDbConfig.cs          ← EF Core конфигурация (добави в DbContext)
│   ├── VodController.cs        ← REST API за VOD: browse, watch, progress, comments
│   └── VodProcessingService.cs ← Бекграунд FFmpeg транскодиране + StreamRecorder
└── maui/StreamBG.Mobile/
    ├── Models/Models.cs         ← Всички DTO/модели
    ├── Services/
    │   ├── ApiService.cs        ← HTTP клиент за всички API calls
    │   └── ChatService.cs       ← SignalR клиент за реалтаймов чат
    ├── ViewModels/
    │   ├── AuthViewModel.cs     ← Логин/регистрация (MVVM)
    │   ├── HomeViewModel.cs     ← Главен екран: Live + VOD tabs
    │   ├── StreamViewModel.cs   ← Гледане на стрийм + чат
    │   └── VodViewModel.cs      ← VOD плейър + прогрес
    ├── Pages/
    │   ├── HomePage.xaml        ← Grid с Live/VOD + категории
    │   ├── StreamPage.xaml      ← Плейър + чат overlay
    │   ├── VodPage.xaml         ← VOD плейър + коментари
    │   └── AuthPage.xaml        ← Логин/регистрация форма
    ├── AppShell.xaml            ← Shell навигация (Tab bar)
    └── MauiProgram.cs           ← DI регистрация
```

---

## VOD система — как работи

### 1. Автоматично записване на стриймове

Когато стриймърът е на живо, `StreamRecorder` стартира FFmpeg в бекграунд:
```
nginx-rtmp → RTMP → FFmpeg → /var/vod/raw/{userId}/{streamId}.mp4
```

### 2. Транскодиране след края на стрийма

`VodProcessingService` следи за нови VOD записи (статус `Pending`) и ги транскодира:
```
MP4 → FFmpeg → HLS (3 качества):
  - 1080p (5000 kbps)
  - 720p  (2800 kbps)
  - 480p  (1200 kbps)
  - master.m3u8 (adaptive bitrate)
```

### 3. Конфигурация в appsettings.json

```json
"Vod": {
  "RawPath": "/var/vod/raw",
  "OutputPath": "/var/vod/processed",
  "PublicBaseUrl": "https://your-server.bg/vod"
}
```

### 4. Nginx за VOD файловете

Добави в nginx.conf:
```nginx
location /vod/ {
    alias /var/vod/processed/;
    types {
        application/vnd.apple.mpegurl  m3u8;
        video/mp2t                     ts;
    }
    add_header Cache-Control no-cache;
    add_header Access-Control-Allow-Origin *;
}
```

### 5. Добавяне в DbContext

```csharp
// В StreamBGDbContext.cs
public DbSet<VodVideo>    VodVideos    => Set<VodVideo>();
public DbSet<VodChapter>  VodChapters  => Set<VodChapter>();
public DbSet<VodComment>  VodComments  => Set<VodComment>();
public DbSet<WatchProgress> WatchProgress => Set<WatchProgress>();

protected override void OnModelCreating(ModelBuilder b) {
    // ... съществуваща конфигурация ...
    VodModelConfig.ConfigureVod(b);
}
```

### 6. Регистрация на сервиза в Program.cs

```csharp
builder.Services.AddHostedService<VodProcessingService>();
builder.Services.AddSingleton<StreamRecorder>();
```

### VOD API endpoints

| Метод | URL | Описание |
|-------|-----|----------|
| GET  | `/api/vod` | Всички VOD видеа (пагинирани) |
| GET  | `/api/vod/{id}` | Един VOD + глави |
| GET  | `/api/vod/{id}/comments` | Коментари |
| POST | `/api/vod/{id}/comments` | Добавяне на коментар |
| POST | `/api/vod/{id}/progress` | Запазване на прогрес |
| GET  | `/api/vod/{id}/progress` | Вземане на прогрес |
| GET  | `/api/vod/my` | Моите VOD видеа |
| PUT  | `/api/vod/{id}` | Обновяване на метаданни |
| DELETE | `/api/vod/{id}` | Изтриване |

---

## .NET MAUI мобилно приложение

### Изисквания

- Visual Studio 2022 с .NET MAUI workload
- Android SDK (API 24+) или Xcode 15+ за iOS
- .NET 8 SDK

### Стартиране

```bash
cd maui/StreamBG.Mobile

# Android
dotnet build -t:Run -f net8.0-android

# iOS (само на Mac)
dotnet build -t:Run -f net8.0-ios
```

### Настройка на API адреса

При стартиране промени в `MauiProgram.cs`:
```csharp
var apiBaseUrl = "https://your-actual-server.bg";
```

Или динамично от Settings страницата.

### Пакети

| Пакет | Цел |
|-------|-----|
| `LibVLCSharp.MAUI` | HLS видео плейър (supports live + VOD) |
| `CommunityToolkit.Maui` | UI компоненти и конвертори |
| `CommunityToolkit.Mvvm` | MVVM: ObservableProperty, RelayCommand |
| `Microsoft.AspNetCore.SignalR.Client` | Реалтаймов чат |

### Функционалност на приложението

- 🏠 **Начален екран** — Grid с Live стриймове и VOD видеа, филтриране по категория
- 📺 **Гледане на стрийм** — HLS плейър + реалтаймов SignalR чат с брой зрители
- 📼 **VOD плейър** — HLS с adaptive bitrate, продължаване от последна позиция
- 🔐 **Вход/Регистрация** — JWT auth, запазен токен с Preferences
- 💬 **Чат** — цветни потребителски имена, изпращане, автоматичен scroll
- 📊 **Прогрес** — автоматично запазване на позицията на всеки 15 сек

---

## Цялостна архитектура (финална)

```
┌─────────────────────────────────────────────────────┐
│              Входни източници                        │
│  OBS / Телефон / IP Камера / Screen Cap              │
└──────────────────┬──────────────────────────────────┘
                   │ RTMP (порт 1935)
                   ▼
┌─────────────────────────────────────────────────────┐
│           nginx-rtmp Медиа сървър                    │
│  → on-publish webhook → верификация                  │
│  → HLS сегменти /tmp/hls/{key}/index.m3u8           │
│  → on-publish-done → стартира VOD запис             │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│           ASP.NET Core 8 API                         │
│  ┌──────────┐ ┌──────────┐ ┌────────────┐           │
│  │ REST API │ │ SignalR  │ │ VOD+Social │           │
│  │ Auth/    │ │ Chat Hub │ │ Background │           │
│  │ Streams  │ │ Viewers  │ │  Services  │           │
│  └──────────┘ └──────────┘ └────────────┘           │
│  ┌──────────┐ ┌──────────┐                          │
│  │SQL Server│ │  Redis   │                          │
│  └──────────┘ └──────────┘                          │
└──────────────────┬──────────────────────────────────┘
                   │
         ┌─────────┼─────────┐
         ▼         ▼         ▼
    React Web   MAUI iOS  MAUI Android
    (HLS.js +  (LibVLC +  (LibVLC +
     SignalR)   SignalR)   SignalR)
         │
         ▼
  Facebook / YouTube / TikTok
  (FFmpeg паралелен relay)
```
