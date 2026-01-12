# 🔐 Cómo configurar las credenciales de Esendex

## Para hacer pruebas locales

### Opción 1: Archivo de configuración (Recomendado para pruebas)

1. Abre el archivo `appsettings.Local.json` que ya está creado
2. Reemplaza los valores de ejemplo con tus credenciales reales:

```json
{
  "Esendex": {
    "Username": "tu.email@empresa.com",
    "ApiPassword": "EX1234567890abcdefghijk"
  }
}
```

3. Guarda el archivo
4. Ejecuta: `dotnet run`

✅ **Este archivo NO se subirá a Git** (está en `.gitignore`)

---

### Opción 2: Variables de entorno

Si prefieres usar variables de entorno, en PowerShell ejecuta:

```powershell
$env:ESENDEX_USER = "tu.email@empresa.com"
$env:ESENDEX_API_PASSWORD = "EX1234567890abcdefghijk"
dotnet run
```

---

## 🔍 ¿Dónde obtener las credenciales?

1. **Username**: Tu email/usuario de cuenta Esendex
2. **ApiPassword**: 
   - Inicia sesión en https://www.esendex.es/ o https://www.esendex.com/
   - Ve a **Settings** → **API Access** (o Configuración → Acceso API)
   - Copia el **API Password** o genera uno nuevo
   - **NOTA**: El API Password es diferente de tu contraseña de inicio de sesión web

---

## ✅ Verificar que funciona

Después de configurar las credenciales:

1. Ejecuta la aplicación:
```powershell
dotnet run
```

2. Verifica el health check:
```powershell
curl http://localhost:5080/api/health
```

Deberías ver:
```json
{
  "status": "ok",
  "esendexConfigured": true
}
```

Si ves `"esendexConfigured": false`, las credenciales no se cargaron correctamente.

3. Prueba obtener mensajes:
```powershell
curl "http://localhost:5080/api/messages?direction=inbound&page=1&pageSize=50"
```

---

## 🚫 Modo Mock (sin credenciales)

Si NO configuras credenciales, la API funcionará igual pero devolverá **datos de ejemplo** en lugar de conectarse a Esendex real. Útil para desarrollo sin gastar créditos SMS.

---

## ⚠️ IMPORTANTE

- **NUNCA** hagas commit del archivo `appsettings.Local.json` con credenciales reales
- **NUNCA** compartas tus credenciales de API
- En producción, usa Azure Key Vault o AWS Secrets Manager (ver PRODUCTION-GUIDE.md)

