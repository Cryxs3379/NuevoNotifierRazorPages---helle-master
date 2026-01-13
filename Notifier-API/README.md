# Notifier-API (Razor Pages) — Versión Simple

Proyecto .NET 8 con Razor Pages para:
- Ver mensajes (entrantes/salientes)
- Enviar SMS de prueba
- Ver llamadas perdidas (desde tu API SQL externa)

## Requisitos
- .NET 8 SDK instalado

## Ejecutar
Desde la raíz del repo:
```bash
# Opción A
cd Notifier-API
dotnet run

# Opción B (desde la raíz)
dotnet run --project Notifier-API
```
La UI estará en: http://localhost:5080

## Servicios (mock vs real)
- Por defecto usa mocks (sin depender de Esendex).
- Si configuras credenciales en `appsettings.json` (sección `Esendex`), usará los servicios reales.

### Credenciales (desarrollo)
Edita `Notifier-API/appsettings.json`:
```json
{
  "Esendex": {
    "BaseUrl": "https://api.esendex.com/v1.0/",
    "Username": "tu.email@empresa.com",
    "ApiPassword": "EXxxxxxxxxxxxx",
    "AccountReference": "EX0000000"
  },
  "MissedCallsAPI": { "BaseUrl": "http://localhost:5000" }
}
```
- Si `Username`/`ApiPassword`/`AccountReference` están presentes, se conectará a Esendex.
- Si no, la app funciona en modo mock.

## Estructura mínima
```
Notifier-API/
├── Program.cs                # DI mínima + Razor Pages + HttpClient
├── appsettings.json          # Configuración (Esendex y MissedCallsAPI)
├── Pages/                    # UI Razor Pages (Dashboard, Messages, Calls)
├── Services/                 # Interfaces + implementaciones (mock/real)
└── Models/                   # DTOs simples
```

## Páginas
- Dashboard: `/`
- Mensajes: `/Messages` (filtros y paginación básica)
- Enviar SMS: `/Messages/Reply`
- Llamadas perdidas: `/Calls`

## API REST v1

Todos los endpoints de la API están bajo el prefijo `/api/v1/` y devuelven JSON.

### Health Check

**GET** `/api/v1/health`

Verifica el estado de la aplicación y si Esendex está configurado.

**Respuesta:**
```json
{
  "status": "ok",
  "esendexConfigured": true
}
```

### Mensajes

**GET** `/api/v1/messages`

Obtiene la lista de mensajes con paginación.

**Parámetros de consulta:**
- `direction` (opcional): `"inbound"` o `"outbound"` (default: `"inbound"`)
- `page` (opcional): Número de página (default: `1`, mínimo: `1`)
- `pageSize` (opcional): Tamaño de página (default: `25`, rango: `10-200`)
- `accountRef` (opcional): Referencia de cuenta Esendex

**Respuesta:**
```json
{
  "items": [
    {
      "id": "message-id",
      "from": "+34123456789",
      "to": "+34987654321",
      "message": "Texto del mensaje",
      "receivedUtc": "2024-01-01T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "total": 100
}
```

**GET** `/api/v1/messages/{id}`

Obtiene un mensaje completo por su ID.

**Parámetros de ruta:**
- `id`: ID del mensaje

**Respuesta:**
```json
{
  "id": "message-id",
  "from": "+34123456789",
  "to": "+34987654321",
  "message": "Texto completo del mensaje",
  "receivedUtc": "2024-01-01T12:00:00Z"
}
```

**DELETE** `/api/v1/messages/{id}`

Elimina un mensaje entrante (inbound) en Esendex.

**Parámetros de ruta:**
- `id`: ID del mensaje

**Respuesta:** `204 No Content` si se eliminó correctamente.

### Llamadas Perdidas

**GET** `/api/v1/calls/missed`

Obtiene la lista de llamadas perdidas.

**Parámetros de consulta:**
- `limit` (opcional): Número máximo de registros (default: `100`, rango: `10-500`)

**Respuesta:**
```json
{
  "success": true,
  "count": 50,
  "data": [
    {
      "id": 123,
      "dateAndTime": "2024-01-01T12:00:00Z",
      "phoneNumber": "+34123456789",
      "status": 0,
      "clientCalledAgain": null,
      "answerCall": null
    }
  ]
}
```

**GET** `/api/v1/calls/stats`

Obtiene estadísticas de llamadas perdidas.

**Respuesta:**
```json
{
  "totalMissedCalls": 150,
  "todayMissedCalls": 5,
  "thisWeekMissedCalls": 25,
  "lastMissedCall": {
    "dateAndTime": "2024-01-01T12:00:00Z",
    "phoneNumber": "+34123456789"
  }
}
```

### Códigos de Estado HTTP

- `200 OK`: Solicitud exitosa
- `204 No Content`: Eliminación exitosa
- `400 Bad Request`: Parámetros inválidos
- `401 Unauthorized`: Error de autenticación con Esendex
- `404 Not Found`: Recurso no encontrado
- `500 Internal Server Error`: Error del servidor
- `502 Bad Gateway`: Error al conectar con servicio externo

## Notas sobre llamadas perdidas
- La página de llamadas perdidas consulta una API externa (tu API SQL) mediante `IMissedCallsService`.
- Configura la URL en `MissedCallsAPI:BaseUrl` (por defecto `http://localhost:5000`).

## Comandos útiles
```bash
# Compilar
cd Notifier-API && dotnet build

# Ejecutar con proyecto especificado
dotnet run --project Notifier-API
```

## Buenas prácticas
- No subas credenciales reales a Git.
- Usa variables de entorno o Secret Manager en producción.
- Si quieres volver a activar características avanzadas (Swagger, API Key, CORS, Polly, cache), agrégalas luego de entender el flujo básico.
