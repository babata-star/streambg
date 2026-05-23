# StreamBG — Българска стрийминг платформа

> Пълен аналог на Twitch, изграден на .NET 8 — live стриймове с адаптивен битрейт, VOD архив, клипчета, real-time чат и мобилно приложение за Android/iOS.

## Съдържание

1. [Изисквания](#изисквания)
2. [Бърз старт](#бърз-старт)
3. [Конфигурация](#конфигурация)
4. [Потребителски интерфейс](#потребителски-интерфейс)
5. [Администраторски панел](#администраторски-панел)
6. [OBS настройка](#obs-настройка)
7. [Мобилно приложение](#мобилно-приложение)
8. [API документация](#api-документация)
9. [CDN интеграция](#cdn-интеграция)
10. [Архитектура](#архитектура)
11. [Решаване на проблеми](#решаване-на-проблеми)

## Изисквания

| Софтуер | Версия |
|---------|--------|
| Docker Desktop | 24+ |
| Docker Compose | v2 (включен в Docker Desktop) |
| Git | всяка |

**За разработка (опционално):**
- .NET SDK 8.0
- Node.js 20+
- Visual Studio 2022 / Rider / VS Code

## Бърз старт

### 1. Клонирай проекта

```bash
git clone https://github.com/babata-star/streambg.git
cd streambg
```

### 2. Конфигурирай секретите

```bash
cp docker/.env.example docker/.env
```

Отвори `docker/.env` и смени **задължително**:

```env
SQL_PASSWORD=SuperSecretPass123!    # мин. 8 знака, главна буква, цифра, специален знак
JWT_SECRET_KEY=my-super-secret-32-char-key-here!!
```

### 3. Стартирай всичко

```bash
cd docker
docker compose up -d
```

Стекът стартира в ред: SQL Server → Redis → API → nginx-rtmp → Frontend.  
Изчакай ~60 сек при първи старт (SQL Server инициализация).

### 4. Приложи миграциите (само веднъж)

```bash
docker compose exec api dotnet ef database update
```

> При `ASPNETCORE_ENVIRONMENT=Development` миграциите се прилагат автоматично.

### 5. Отвори в браузъра

| URL | Описание |
|-----|----------|
| `http://localhost` | Web интерфейс |
| `http://localhost/admin` | Администраторски панел |
| `http://localhost:5000/swagger` | API документация (Swagger) |
| `http://localhost:8080/stat` | nginx-rtmp статистика (само localhost) |

### 6. Създай първи администратор

Регистрирай се на `http://localhost/register`, после промени ролята в базата.

**PowerShell / CMD (Windows):**
```powershell
docker exec docker-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStr0ngPass!" -C -Q "UPDATE Users SET IsAdmin=1 WHERE Username='твоят-username'"
```

**Git Bash (Windows) — задължително `MSYS_NO_PATHCONV=1`:**
```bash
MSYS_NO_PATHCONV=1 docker exec docker-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStr0ngPass!" -C -Q "UPDATE Users SET IsAdmin=1 WHERE Username='твоят-username'"
```

**Linux / macOS:**
```bash
docker exec docker-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStr0ngPass!" -C \
  -Q "UPDATE Users SET IsAdmin=1 WHERE Username='твоят-username'"
```

> `-C` = TrustServerCertificate (нужно за self-signed сертификата в Docker)  
> Ако контейнерът има различно име: `docker ps --format "{{.Names}}" | grep sql`

---

## Конфигурация

Всички настройки минават през `docker/.env`. Файлът `docker-compose.yml` ги инжектира като environment variables в контейнерите.

### Задължителни

```env
SQL_PASSWORD=           # SQL Server sa парола (мин. 8 знака + специален знак)
JWT_SECRET_KEY=         # минимум 32 символа — ключ за подписване на JWT токени
```

### CDN (опционално)

По подразбиране медията се сервира директно от nginx-rtmp. За production с CDN:

```env
CDN_ENABLED=true
CDN_PROVIDER=Cloudflare   # или CloudFront

CDN_HLS_BASE_URL=https://stream.yourdomain.bg/hls
CDN_VOD_BASE_URL=https://cdn.yourdomain.bg/vod
CF_ZONE_ID=your-zone-id
CF_API_TOKEN=your-api-token
```

Вижте [CDN интеграция](#cdn-интеграция) за пълни инструкции.

---

## Потребителски интерфейс

### Начална страница (`/`)

- **Live стриймове** — карти с thumbnail, брой зрители, категория
- **Филтриране по категория** — хоризонтален скролируем списък
- Кликни върху стрийм → отваря страницата на стрийма

### Страница на стрийм (`/stream/:username`)

- **HLS видео плейър** с адаптивен битрейт (720p / 480p / 360p) чрез hls.js
- **Real-time чат** — SignalR, цветни потребителски имена
- Автоматично избира качество според скоростта на интернет

### Вход и Регистрация (`/login`, `/register`)

- JWT автентикация с refresh tokens
- Токенът се пази в `localStorage` и се изпраща автоматично

### Табло на стриймъра (`/dashboard`)

Достъпно само за потребители с роля **Стриймър**.

- **Stream Key** — копирай и използвай в OBS
- **Настройки** — заглавие, описание, категория (10 избора)
- **Социален relay** — едновременно стриймване към YouTube, Facebook, TikTok
- Бутон "Генерирай нов ключ" при компрометиране

---

## Администраторски панел

Достъпен на `/admin`. Изисква `IsAdmin = true`.

### Достъп

1. Регистрирай се на `/register`
2. Промени `IsAdmin` в базата (виж [Бърз старт → стъпка 6](#6-създай-първи-администратор))
3. Влез на `/login` → навигирай на `/admin`

### Преглед (Overview таб)

Показва 5 live статистики:

| Карта | Описание |
|-------|----------|
| 👥 Потребители | Общ брой регистрирани |
| 📡 Живи стриймове | Активни в момента |
| 👁 Зрители сега | Сумарни зрители |
| 🎬 Стриймове днес | Стартирани днес |
| ✨ Нови потребители днес | Регистрации за деня |

### Потребители таб

Таблица с всички потребители. Поддържа **live търсене** по username или email.

**Роли** (цветни бейджове):
- 🟣 **Admin** — пълен достъп
- 🔵 **Стриймър** — може да стриймва, има табло
- ⚫ **Зрител** — стандартен потребител

**Действия:**

| Бутон | Условие | Ефект |
|-------|---------|-------|
| Стриймър | само Зрители | Дава права за стрийминг |
| Блокирай | не-Admin | Забранява вход, спира стрийма |
| Разблокирай | блокирани | Възстановява достъпа |

> Admin акаунти не могат да бъдат блокирани от интерфейса.

### Стриймове таб

Таблица с всички активни live стриймове:

| Колона | Описание |
|--------|----------|
| Стриймър | Username с пулсиращ 🔴 dot |
| Заглавие | Заглавие на стрийма |
| Зрители | Брой в момента |
| Категория | Избрана категория |
| Прекрати | Принудително спира стрийма |

### CDN инвалидация (Admin API)

```bash
# Вземи Admin JWT
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.bg","password":"Pass123!"}' \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

# Изчисти cache за конкретен стрийм
curl -X POST http://localhost:5000/api/admin/cdn/invalidate/stream/pesho_key \
  -H "Authorization: Bearer $TOKEN"

# Изчисти cache за VOD
curl -X POST "http://localhost:5000/api/admin/cdn/invalidate/vod/userId/42" \
  -H "Authorization: Bearer $TOKEN"

# Персонализиран path pattern
curl -X POST http://localhost:5000/api/admin/cdn/invalidate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"patterns":["/vod/*","/thumbnails/*"]}'
```

---

## OBS настройка

### Стъпки

1. Влез в **Таблото** (`/dashboard`) → копирай Stream Key
2. В OBS: **Settings → Stream**

| Поле | Стойност |
|------|---------|
| Service | Custom |
| Server | `rtmp://твоят-сървър:1935/live` |
| Stream Key | (копираният ключ) |

3. Кликни **Start Streaming**

### Препоръчани OBS настройки

```
Output → Encoding:
  Encoder:    x264 (или NVENC ако имаш GPU)
  Bitrate:    3000–6000 Kbps
  Keyframe:   2 seconds  ← ЗАДЪЛЖИТЕЛНО

Video:
  Base Resolution:   1920×1080
  Output Resolution: 1280×720
  FPS:               30 или 60
```

> **Keyframe interval = 2s** е задължителен за правилна ABR сегментация.

### Адаптивен битрейт (ABR)

| Качество | Резолюция | Битрейт |
|----------|-----------|---------|
| 720p | 1280×720 | 2.8 Mbps |
| 480p | 854×480 | 1.4 Mbps |
| 360p | 640×360 | 700 Kbps |

---

## Мобилно приложение

### Изграждане (Android)

```bash
cd maui/StreamBG.Mobile

# Инсталирай MAUI workload (веднъж)
dotnet workload install maui-android

# Смени URL-а на сървъра в MauiProgram.cs
# Preferences.Get("api_base_url", "https://твоят-сървър.bg")

# Изгради APK
dotnet publish -f net8.0-android -c Release
```

APK: `bin/Release/net8.0-android/publish/`

### Функционалност

| Таб | Описание |
|-----|----------|
| 🏠 Начало | Live стриймове, категории, thumbnail grid |
| ✂ Клипове | Browse клипчета, сортиране Нови/Популярни |
| 🔍 Търси | Търсене по стриймъри, стриймове, VOD |
| 👤 Профил | Настройки, stream key, social relay |

---

## API документация

Swagger UI достъпен на: `http://localhost:5000/swagger`

### Автентикация

```http
POST /api/auth/register
{"username":"pesho","email":"pesho@example.bg","password":"Pass123!"}

POST /api/auth/login
{"email":"pesho@example.bg","password":"Pass123!"}
```

Отговорът съдържа `accessToken`. Изпращай го в хедъра:
```
Authorization: Bearer <accessToken>
```

### Endpoints

```
# Стриймове
GET  /api/streams                         Всички live стриймове
GET  /api/streams?category=Игри           Филтрирани по категория
GET  /api/streams/{username}              Конкретен стрийм
GET  /api/streams/my/key          [Auth]  Моят stream key
PUT  /api/streams/settings         [Auth]  Заглавие / категория
PUT  /api/streams/my/social        [Auth]  Social relay настройки

# VOD
GET  /api/vod                             Всички VOD видеа
GET  /api/vod?username=pesho              VOD на стриймър
GET  /api/vod/{id}                        Конкретно VOD
POST /api/vod/{id}/progress       [Auth]  Запази позиция
GET  /api/vod/{id}/progress        [Auth]  Вземи позиция

# Клипове
GET    /api/clips                         Всички клипове
GET    /api/clips?sort=popular            По популярност
GET    /api/clips?streamerId={id}         Клипове на стриймър
POST   /api/clips                 [Auth]  Създай клип
GET    /api/clips/{id}                    Конкретен клип
POST   /api/clips/{id}/view               Брои гледане
DELETE /api/clips/{id}            [Auth]  Изтрий клип

# Търсене
GET  /api/search?q=fortnite               Всичко (streams+users+vod)
GET  /api/search?q=pesho&type=users       Само потребители

# Категории
GET  /api/categories                      Всички категории
GET  /api/categories/top?count=10         Топ по брой стриймове

# Администрация [Admin]
GET  /api/admin/stats                     Статистика
GET  /api/admin/users                     Всички потребители
POST /api/admin/users/{id}/ban            Блокирай
POST /api/admin/users/{id}/unban          Разблокирай
POST /api/admin/users/{id}/make-streamer  Стриймър права
GET  /api/admin/streams/live              Активни стриймове
POST /api/admin/streams/{id}/terminate    Прекрати стрийм
POST /api/admin/cdn/invalidate            CDN cache purge
```

### SignalR чат

```javascript
const conn = new HubConnectionBuilder()
  .withUrl('/hubs/chat', { accessTokenFactory: () => token })
  .build()
await conn.start()

await conn.invoke('JoinStream', 'pesho')        // влез в стрийм
await conn.invoke('SendMessage', 'pesho', 'GG') // изпрати съобщение

conn.on('ReceiveMessage', (msg) => { /* msg.username, msg.content, msg.color */ })
conn.on('ViewerCount', (count) => { /* актуален брой зрители */ })
```

---

## CDN интеграция

### Cloudflare

```env
CDN_ENABLED=true
CDN_PROVIDER=Cloudflare
CDN_HLS_BASE_URL=https://stream.yourdomain.bg/hls
CDN_VOD_BASE_URL=https://cdn.yourdomain.bg/vod
CDN_THUMBNAIL_BASE_URL=https://cdn.yourdomain.bg/thumbnails
CF_ZONE_ID=abc123def456...
CF_API_TOKEN=token-with-Cache-Purge-permission
```

**DNS:** `stream.yourdomain.bg` и `cdn.yourdomain.bg` → твоят IP (Proxied ✓)

### AWS CloudFront

```env
CDN_ENABLED=true
CDN_PROVIDER=CloudFront
CDN_HLS_BASE_URL=https://xxxxx.cloudfront.net/hls
CDN_VOD_BASE_URL=https://xxxxx.cloudfront.net/vod
CF_DISTRIBUTION_ID=EXXXXXXXXXXXXX
AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE
AWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/...
AWS_REGION=us-east-1
```

---

## Архитектура

```
OBS/MAUI                            Браузър/MAUI
    │                                    │
    ▼ RTMP :1935                         ▼ HTTP :80
┌─────────────────────────────────────────────────────┐
│                   Docker Compose                     │
│                                                      │
│  ┌──────────┐    ┌─────────────┐   ┌─────────────┐ │
│  │ Frontend │    │     API     │   │ nginx-rtmp  │ │
│  │  React   │◄──▶│  ASP.NET 8  │◄──│  +FFmpeg    │ │
│  │  :80     │    │  :5000      │   │  :1935/:8080│ │
│  └──────────┘    └──────┬──────┘   └──────┬──────┘ │
│                         │                  │        │
│                 ┌───────┴───────┐          │        │
│          ┌──────▼──────┐  ┌─────▼──────┐  │        │
│          │ SQL Server  │  │   Redis    │  │        │
│          └─────────────┘  └────────────┘  │        │
│                                            │        │
│  Volumes: vod_data ◄──────────────────────┘        │
│           recordings_data  clips_data  hls_data     │
└─────────────────────────────────────────────────────┘
```

### Поток на стрийм

```
OBS → RTMP :1935/live/{key}
  ↓ nginx on_publish → API валидира ключа
  ↓ exec abr-transcode.sh → FFmpeg splits:
      /tmp/hls/{key}/master.m3u8
      /tmp/hls/{key}/720p/  480p/  360p/
  ↓ Зрители ← HLS :8080/hls/{key}/master.m3u8
  ↓ (при спиране) nginx record_all → /data/recordings/{key}.flv
  ↓ on_record_done → API → VodTranscodingService
  ↓ FFmpeg → /data/vod/{id}/master.m3u8  (VOD архив)
```

### Поток на клип

```
POST /api/clips { streamId, offsetSeconds }
  ↓ ClipService валидира → DB запис → Channel<int> queue
  ↓ ClipTranscodingService (BackgroundService)
  ↓ FFmpeg -ss {offset} -t 30 → /data/clips/{id}.mp4
  ↓ FFmpeg thumbnail         → /data/clips/{id}.jpg
  ↓ Clip.Status = "Ready"
```

---

## Решаване на проблеми

### SQL Server не стартира

```bash
docker compose logs sqlserver | tail -20
# Изчакай: "SQL Server is now ready for client connections"
```

### Миграцията не се прилага

```bash
docker compose exec api dotnet ef database update --verbose
```

### Стриймът не се появява в интерфейса

```bash
docker compose logs api | grep -i "rtmp\|publish"
docker compose logs nginx-rtmp | grep "publishing\|error"
```

### ABR не работи (само едно качество)

```bash
# Провери дали скриптът е изпълним
docker compose exec nginx-rtmp ls -la /etc/nginx/abr-transcode.sh
# Провери FFmpeg
docker compose exec nginx-rtmp which ffmpeg
docker compose logs nginx-rtmp | grep -i "ffmpeg\|transcode"
```

### Клипът стои в "Обработва се" дълго

```bash
docker compose logs api | grep -i "clip\|transcode"
docker compose exec nginx-rtmp ls /data/recordings/
```

### Ресет (изтрива всички данни!)

```bash
docker compose down -v
docker compose up -d
docker compose exec api dotnet ef database update
```

