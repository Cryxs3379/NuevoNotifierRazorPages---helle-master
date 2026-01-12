# Notifier API - Llamadas Perdidas

API desarrollada en .NET 8 para consultar las últimas llamadas perdidas desde la base de datos SQL Server.

## Configuración de la Base de Datos

### Parámetros de Conexión
- **Server**: dbserver
- **Database**: Notifications
- **User ID**: NavisionReaderUser
- **Password**: z91234.AS92
- **Encrypt**: False

### Connection String
```
Server=dbserver;Database=Notifications;User Id=NavisionReaderUser;Password=z91234.AS92;Encrypt=False;TrustServerCertificate=True;
```

## Endpoints Disponibles

### 1. Obtener Llamadas Perdidas
```
GET /api/MissedCalls?limit=100
```
Retorna las últimas llamadas perdidas (Status = 0 y AnswerCall = null).

**Parámetros:**
- `limit` (opcional): Número máximo de registros (por defecto 100)

### 2. Obtener Llamadas Perdidas Detalladas
```
GET /api/MissedCalls/detailed?limit=100
```
Retorna las llamadas perdidas con información adicional como tiempo transcurrido.

### 3. Obtener Estadísticas
```
GET /api/MissedCalls/stats
```
Retorna estadísticas de llamadas perdidas (total, hoy, esta semana, última llamada).

## Criterios para Llamadas Perdidas

Una llamada se considera "perdida" cuando:
- `Status = 0`
- `AnswerCall = null`

## Ejecutar la Aplicación

1. **Restaurar paquetes NuGet:**
   ```bash
   dotnet restore
   ```

2. **Ejecutar la aplicación:**
   ```bash
   dotnet run
   ```

3. **Acceder a Swagger UI:**
   - URL: `https://localhost:7xxx` (puerto mostrado en consola)
   - Swagger UI estará disponible en la raíz de la aplicación

## Estructura del Proyecto

```
NotifierAPI/
├── Controllers/
│   └── MissedCallsController.cs
├── Data/
│   └── NotificationDbContext.cs
├── Models/
│   ├── IncomingCall.cs
│   └── MissedCallDto.cs
├── appsettings.json
├── Program.cs
└── NotifierAPI.csproj
```

## Paquetes NuGet Utilizados

- `Microsoft.EntityFrameworkCore.SqlServer` (8.0.0)
- `Microsoft.EntityFrameworkCore.Tools` (8.0.0)
- `Microsoft.EntityFrameworkCore.Design` (8.0.0)
- `Swashbuckle.AspNetCore` (6.4.0)

## Ejemplo de Respuesta

```json
[
  {
    "id": 146225,
    "dateAndTime": "2025-10-08T09:24:30.000",
    "phoneNumber": "+34691864942",
    "status": 0,
    "clientCalledAgain": null,
    "answerCall": null,
    "isMissedCall": true,
    "timeAgo": "00:15:30",
    "formattedTimeAgo": "15 minuto(s) atrás"
  }
]
```
