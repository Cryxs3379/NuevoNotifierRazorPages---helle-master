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
