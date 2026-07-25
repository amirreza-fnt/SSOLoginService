# سامانه احراز هویت متمرکز شهرداری (Central Auth SSO)

سامانه یکپارچه احراز هویت مبتنی بر SSO وزارت کشور و دولت من، جهت استفاده در سامانه‌ها و اپلیکیشن‌های شهرداری.

## معماری کلی

```
┌─────────────────────────────────────────────────────────────┐
│                    UI.ShahrdariCentralAuth                   │
│                   (فرانت‌اند - React/Next.js)                │
└──────────────────────────┬──────────────────────────────────┘
                           │  HTTPS
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    BK.ShahrdariCentralAuth                   │
│                   (بک‌اند - ASP.NET Core API)                │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────────┐ │
│  │ MOI SSO  │  │DolatMan  │  │  Token   │  │   Partner   │ │
│  │ Provider │  │ Provider │  │ Manager  │  │   Manager   │ │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────┬──────┘ │
│       │             │             │               │         │
│       ▼             ▼             ▼               ▼         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                   Database (SQL Server)               │  │
│  │  ┌────────┐ ┌──────────┐ ┌─────────┐ ┌──────────┐  │  │
│  │  │ Users  │ │  Phones  │ │  Otps   │ │ Partners │  │  │
│  │  └────────┘ └──────────┘ └─────────┘ └──────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
           │                          │
           ▼                          ▼
    ┌──────────────┐          ┌──────────────┐
    │ وزارت کشور   │          │  دولت من     │
    │ (MOI SSO)    │          │ (DolatMan)   │
    └──────────────┘          └──────────────┘
```

### اجزای اصلی

| بخش | ریپو | تکنولوژی | مسئول |
|------|------|-----------|--------|
| فرانت‌اند | `UI.ShahrdariCentralAuth` | React / Next.js | صادق |
| بک‌اند | `BK.ShahrdariCentralAuth` | ASP.NET Core 8 API | امیر |

### جریان احراز هویت (SSO Flow)

```
1. کاربر وارد UI می‌شود
2. کلیک روی "ورود با وزارت کشور"
3. UI دریافت URL لاگین از BK:
   POST /api/auth/login/initiate → { loginUrl: "https://ssokeshvar.moi.ir/..." }
4. ریدایرکت کاربر به MOI
5. احراز هویت در MOI
6. MOI ریدایرکت به BK: /sso/callback?code=xxx&state=yyy
7. BK تبادل کد با MOI (Token Exchange)
8. BK دریافت اطلاعات کاربر (UserInfo)
9. BK ایجاد/بروزرسانی کاربر در دیتابیس
10. BK ساخت JWT و ریدایرکت به UI:
    /auth/callback?token=...&refreshToken=...
11. UI ذخیره توکن و انتقال به صفحه اصلی
```

### جریان WebView (برای اپلیکیشن‌های موبایل)

```
1. اپ WebView باز می‌کند: GET /sso/webview?provider=moi
2. BK صفحه HTML ساده برمی‌گرداند که ریدایرکت می‌کند به MOI
3. MOI کاربر را احراز هویت می‌کند
4. MOI ریدایرکت به BK: /sso/callback?code=xxx
5. BK پردازش و ریدایرکت به UI با توکن
6. UI ریدایرکت به URL Scheme اپ: myapp://auth?token=...
7. اپ WebView را می‌بندد و از توکن استفاده می‌کند
```

### جریان Second Login (ورود با کد ملی + OTP)

```
1. کاربر کد ملی خود را وارد می‌کند
   GET /api/auth/check-user?melliCode=xxx
   ← { exists: true/false }
2. اگر exists=false → ریدایرکت به SSO وزارت کشور
3. اگر exists=true → لیست شماره تلفن‌های ثبت شده
   POST /api/auth/second-login
4. کاربر شماره را انتخاب می‌کند
5. ارسال OTP:
   POST /api/auth/second-login/send-otp
6. کاربر کد OTP را وارد می‌کند
7. تایید OTP و دریافت توکن:
   POST /api/auth/second-login/verify-otp
   ← { accessToken, refreshToken }
```

## API Endpoints

### احراز هویت SSO

| Method | Path | توضیح |
|--------|------|-------|
| GET | `/api/auth/providers` | لیست پروایدرهای فعال |
| GET | `/api/auth/login?provider=moi` | ریدایرکت به SSO |
| POST | `/api/auth/login/initiate` | دریافت URL لاگین (JSON) |
| GET | `/sso/callback?code=xxx&state=yyy` | دریافت کد از MOI (ریدایرکت) |
| GET | `/sso/webview?provider=moi` | صفحه WebView برای اپ موبایل |
| POST | `/api/auth/exchange-code` | تبادل کد SSO با JWT (برای MVC/Custom) |

### ورود دوم (Second Login)

| Method | Path | توضیح |
|--------|------|-------|
| GET | `/api/auth/check-user?melliCode=xxx` | بررسی وجود کاربر |
| POST | `/api/auth/second-login` | دریافت شماره تلفن‌های ثبت شده |
| POST | `/api/auth/second-login/send-otp` | ارسال کد تایید |
| POST | `/api/auth/second-login/verify-otp` | تایید کد و دریافت توکن |

### مدیریت توکن

| Method | Path | توضیح |
|--------|------|-------|
| POST | `/api/auth/refresh` | رفرش توکن |
| POST | `/api/auth/logout` | خروج و باطل کردن توکن |
| GET | `/api/auth/validate-token?token=xxx` | اعتبارسنجی توکن |
| POST | `/api/token/revoke` | باطل کردن رفرش توکن (دستی) |
| GET | `/api/token/introspect` | بررسی جزئیات توکن فعلی |
| GET | `/api/auth/me` | اطلاعات کاربر فعلی |

### سلامت سرویس

| Method | Path | توضیح |
|--------|------|-------|
| GET | `/health` | وضعیت سلامت سرویس و دیتابیس |

## ساختار پروژه

```
BK.ShahrdariCentralAuth/
├── deploy/
│   └── nginx.conf                  # کانفیگ nginx لینوکس
├── src/
│   ├── SSOLoginService.Api/        # بک‌اند اصلی
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs       # لاگین، OTP، تبادل کد، چک کاربر
│   │   │   ├── SSOCallbackController.cs # کال‌بک MOI
│   │   │   ├── TokenController.cs       # مدیریت توکن
│   │   │   └── WebViewController.cs     # صفحه WebView برای اپ
│   │   ├── Data/
│   │   │   └── AppDbContext.cs
│   │   ├── DTOs/                    # Data Transfer Objects
│   │   │   ├── Auth/               # Request/Response DTOs
│   │   │   ├── Common/             # ApiResponse, PagedResponse
│   │   │   └── MinistrySSO/        # DTOهای MOI
│   │   ├── Middleware/
│   │   │   └── SecurityHeadersMiddleware.cs
│   │   ├── Models/
│   │   │   ├── User.cs             # کاربر (فقط کدملی ذخیره می‌شود)
│   │   │   ├── UserPhone.cs        # شماره تلفن‌های کاربر
│   │   │   ├── OtpCode.cs          # کدهای تایید
│   │   │   ├── RefreshToken.cs     # توکن‌های رفرش
│   │   │   └── Partner.cs          # شرکت‌های متصل (قابل توسعه)
│   │   ├── Services/
│   │   │   ├── Interfaces/         # اینترفیس‌ها
│   │   │   ├── MoiSSOProvider.cs   # پیاده‌سازی MOI
│   │   │   ├── DolatManSSOProvider.cs # پیاده‌سازی دولت‌من (غیرفعال)
│   │   │   ├── AuthService.cs      # منطق احراز هویت
│   │   │   ├── TokenService.cs     # تولید/مدیریت توکن
│   │   │   ├── OtpService.cs       # مدیریت OTP
│   │   │   └── SmsService.cs       # ارسال پیامک
│   │   └── Program.cs              # نقطه شروع
│   └── SSOLoginService.Web/        # پروژه تست (MVC موقت)
│       ├── Controllers/
│       ├── Services/
│       ├── Views/
│       └── Program.cs
├── nginx.conf                       # کانفیگ قدیمی (ویندوز)
└── README.md                        # این فایل
```

## کانفیگ و پیکربندی

### فایل appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=IP,PORT;Database=DB_NAME;User Id=USER;Password=PASS;TrustServerCertificate=True"
  },
  "SSO": {
    "Moi": {
      "ClientId": "client_id_registered_in_moi",
      "ClientSecret": "client_secret_from_moi",
      "CallbackUrl": "https://sso-api.shahrdari.ir/sso/callback?provider=moi"
    }
  },
  "Jwt": {
    "SecretKey": "your-256bit-secret-key-here",
    "Issuer": "ShahrdariCentralAuth",
    "Audience": "ShahrdariCentralAuth.Client"
  },
  "Frontend": {
    "Url": "https://sso.shahrdari.ir",
    "BaseUrl": "https://sso.shahrdari.ir"
  }
}
```

### متغیرهای محیطی (Environment Variables)

برای دپلوی روی لینوکس می‌توان از env variables استفاده کرد:

| متغیر | توضیح |
|-------|-------|
| `ConnectionStrings__DefaultConnection` | کانکشن استرینگ دیتابیس |
| `SSO__Moi__ClientId` | Client ID وزارت کشور |
| `SSO__Moi__ClientSecret` | Client Secret وزارت کشور |
| `SSO__Moi__CallbackUrl` | آدرس کال‌بک (ثبت شده در MOI) |
| `Jwt__SecretKey` | کلید رمزنگاری JWT |
| `Frontend__BaseUrl` | آدرس فرانت‌اند |
| `Kestrel__Endpoints__Http__Url` | پورت گوش دادن (پیش‌فرض: http://0.0.0.0:5001) |

## دپلوی روی لینوکس

### 1. نصب .NET 8 Runtime

```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

### 2. انتشار پروژه

```bash
dotnet publish src/SSOLoginService.Api/SSOLoginService.Api.csproj \
  -c Release \
  -o /opt/shahrdari-central-auth/api
```

### 3. کانفیگ nginx

```bash
sudo cp deploy/nginx.conf /etc/nginx/sites-available/shahrdari-central-auth
sudo ln -s /etc/nginx/sites-available/shahrdari-central-auth /etc/nginx/sites-enabled/
sudo systemctl reload nginx
```

### 4. راه‌اندازی سرویس (systemd)

```bash
sudo nano /etc/systemd/system/shahrdari-central-auth.service
```

```ini
[Unit]
Description=Shahrdari Central Auth SSO Service
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/shahrdari-central-auth/api
ExecStart=/usr/bin/dotnet SSOLoginService.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=shahrdari-central-auth
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5001

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable shahrdari-central-auth
sudo systemctl start shahrdari-central-auth
sudo systemctl status shahrdari-central-auth
```

### 5. بررسی لاگ‌ها

```bash
sudo journalctl -u shahrdari-central-auth -f
```

## ثبت redirect_uri در وزارت کشور

برای اینکه MOI بعد از احراز هویت کاربر را به بک‌اند ما برگرداند، آدرس زیر باید در پنل وزارت کشور ثبت شود:

```
https://sso-api.shahrdari.ir/sso/callback?provider=moi
```

### نکات مهم
- آدرس callback باید دقیقاً مطابق ثبت شده باشد (شامل query parameter)
- پروتکل باید HTTPS باشد
- دامنه باید معتبر و دارای SSL معتبر باشد
- بعد از ثبت، تست کنید با: `GET /api/auth/login?provider=moi`

## ادغام با شرکت‌های ثالث (قابل توسعه)

برای شرکت‌هایی که می‌خواهند از سرویس SSO استفاده کنند:

1. هر شرکت یک `ApiKey` دریافت می‌کند
2. تنظیمات Rate Limit و IPهای مجاز برای هر شرکت
3. Redirect URIهای مجاز برای بازگشت پس از احراز هویت
4. احراز هویت از طریق WebView (iframe) با نمایش صفحه سرویس ما

### API Call Flow برای شرکت ثالث

```
1. اپ شرکت: GET /sso/webview?apiKey=xxxxx&provider=moi&redirectUri=...
2. BK: بررسی ApiKey و IP و redirectUri مجاز
3. BK: ریدایرکت به MOI
4. MOI: احراز هویت و بازگشت به BK
5. BK: پردازش و ریدایرکت به redirectUri ثبت شده شرکت با توکن
```

## نکات امنیتی

- تمام توکن‌ها در کوکی HttpOnly و Secure ذخیره می‌شوند
- Rate Limiting بر اساس IP (۱۰ درخواست در دقیقه برای endpoints حساس)
- Rate Limiting بر اساس ApiKey برای شرکت‌های ثالث
- CORS محدود به دامنه‌های مجاز
- ForwardedHeaders برای تشخیص IP واقعی از پشت nginx
- Session State با HttpOnly و Secure
- OTP با محدودیت تلاش (۵ بار) و انقضای ۲ دقیقه
- Server-Side state validation برای جلوگیری از CSRF در callback
- No sensitive data logging

## توسعه‌دهندگان

- **امیر** - بک‌اند (BK.ShahrdariCentralAuth)
- **صادق** - فرانت‌اند (UI.ShahrdariCentralAuth)
