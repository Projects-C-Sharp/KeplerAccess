# Kepler Access — `access.kepler.andrescortes.dev`

Portal PWA de control de acceso al teatro. Construido con **ASP.NET Core MVC + C#**.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core 9 MVC (C#) |
| Frontend | Razor Views + Vanilla JS |
| QR Decode | [jsQR](https://github.com/cozmo/jsQR) (client-side, sin dependencias) |
| Cámara | Web API `getUserMedia` |
| PWA | Service Worker + Web App Manifest |
| Auth | JWT (cookie HttpOnly) via API Central |
| Fuentes | Syne (display) · DM Mono (mono) |

## Estructura

```
AccessKepler/
├── Controllers/
│   └── HomeController.cs        # Login, Scanner, ValidateQr, Stats
├── Models/
│   └── Models.cs                # DTOs: ValidateTicketRequest/Response, TicketInfo...
├── Views/
│   ├── Home/
│   │   ├── Login.cshtml         # Pantalla de login
│   │   └── Scanner.cshtml       # App de escaneo QR
│   └── Shared/
│       └── _Layout.cshtml       # Layout base con PWA meta tags
├── wwwroot/
│   ├── css/app.css              # Estilos — paleta Calcite
│   ├── js/
│   │   ├── scanner.js           # Cámara + jsQR + lógica de validación
│   │   ├── sw.js                # Service Worker PWA
│   │   ├── sw-register.js       # Registro del SW
│   │   └── jsqr.min.js          # Librería QR decoder
│   ├── icons/                   # Iconos PWA (72→512px)
│   └── manifest.json            # Web App Manifest
├── Program.cs                   # DI, JWT, CORS, HttpClient
├── appsettings.json
└── Dockerfile
```

## Configuración

### `appsettings.json`

```json
{
  "Jwt": {
    "Secret": "TU_CLAVE_SECRETA_MINIMO_32_CARACTERES"
  },
  "Api": {
    "BaseUrl": "https://api.kepler.andrescortes.dev"
  }
}
```

## API Central — Endpoints requeridos

El `HomeController` consume estos endpoints de la API Central:

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/auth/login` | Autenticación del empleado |
| `POST` | `/api/tickets/validate` | Validación del código QR |
| `GET`  | `/api/access/stats` | Estadísticas en tiempo real |

### POST `/api/auth/login`

**Request:**
```json
{ "email": "...", "password": "..." }
```

**Response:**
```json
{
  "success": true,
  "token": "eyJhbGci...",
  "employeeName": "Juan Pérez"
}
```

### POST `/api/tickets/validate`

**Request:**
```json
{
  "qrCode": "TKT-2024-ABC123XYZ",
  "scannedBy": "Juan Pérez",
  "scannedAt": "2024-11-15T20:30:00Z"
}
```

**Response:**
```json
{
  "isValid": true,
  "status": "valid",
  "message": "Boleta válida. Bienvenido.",
  "alertLevel": 0,
  "ticket": {
    "ticketId": "TKT-2024-ABC123XYZ",
    "eventName": "Ballet Nacional",
    "eventDate": "2024-11-15",
    "eventTime": "20:00",
    "holderName": "María García",
    "seat": "F-14",
    "zone": "Platea",
    "ticketType": "General"
  }
}
```

**`status` posibles:** `valid` · `already_used` · `invalid` · `expired` · `error`

**`alertLevel`:** `0` = Success · `1` = Warning · `2` = Danger (fraude)

## Desarrollo local

```bash
# Clonar y restaurar
dotnet restore

# Correr
dotnet run

# La app estará en https://localhost:5001
# ⚠️ HTTPS es requerido para acceso a cámara en browsers
```

## Deploy con Docker

```bash
docker build -t kepler-access .
docker run -p 8080:8080 \
  -e Jwt__Secret="tu-clave-secreta" \
  -e Api__BaseUrl="https://api.kepler.andrescortes.dev" \
  kepler-access
```

## Funcionalidades PWA

- ✅ Instalable en Android e iOS (Add to Home Screen)
- ✅ Fullscreen standalone (sin barra del browser)
- ✅ Service Worker para caché de assets estáticos
- ✅ Indicador de conectividad en tiempo real
- ✅ Compatible con cámara trasera del dispositivo
- ✅ Torch/flash si el dispositivo lo soporta
- ✅ Entrada manual como fallback

## Seguridad antifraude

La detección de fraude ocurre en la **API Central**, pero la app comunica visualmente:

- 🚫 **ACCESO DENEGADO** — QR inválido o no encontrado
- 🔁 **¡BOLETA DUPLICADA!** — QR ya fue escaneado (status: `already_used`)
- ⚠️ **ALERTA** — Advertencia general

La vibración del celular diferencia los casos:
- Válido: `[50, 50, 100]`
- Duplicado/fraude: `[100, 50, 100, 50, 200]`
- Alerta: `[100, 100, 100]`
