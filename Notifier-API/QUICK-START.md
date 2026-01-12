# ⚡ Quick Start - Notifier API

## 🚀 Compilar y Ejecutar (3 pasos)

### 1️⃣ Detener procesos anteriores

```powershell
taskkill /F /IM Notifier-API.exe 2>$null
taskkill /F /IM dotnet.exe 2>$null
```

### 2️⃣ Compilar

```powershell
dotnet clean
dotnet restore
dotnet build
```

### 3️⃣ Ejecutar

```powershell
dotnet run
```

---

## ✅ Verificar que funciona

### Abrir Swagger UI
http://localhost:5080/swagger

### Probar Health
```powershell
curl http://localhost:5080/api/health
```

### Probar Messages
```powershell
curl "http://localhost:5080/api/v1/messages?direction=inbound&page=1&pageSize=5"
```

---

## 🔧 Si hay problemas

### Error: Puerto ocupado
```powershell
netstat -ano | findstr 5080
# Matar proceso con PID mostrado
taskkill /F /PID <numero>
```

### Error: Dependencias
```powershell
dotnet restore --force
dotnet build --no-restore
```

### Error: Versión .NET
```powershell
dotnet --version
# Debe ser 8.x
```

---

## 📊 Endpoints Disponibles

| Endpoint | Descripción |
|----------|-------------|
| `/api/health` | Health check |
| `/api/v1/messages` | Mensajes v1 (recomendado) |
| `/api/messages` | Mensajes legacy |
| `/swagger` | Documentación UI |

---

## 🎯 Nuevas Features v1.0

✅ Circuit Breaker & Retry  
✅ Output Caching (30s)  
✅ API Key opcional  
✅ Headers X-Total-Count & Link  
✅ Swagger UI  
✅ Account Reference support  
✅ Fallback DNS  
✅ Logging seguro  

---

Ver **README.md** para documentación completa.

