# CONTEXT — RESTAPIFFISA (API)

Documento de contexto para desarrolladores y agentes. Resume qué hace esta API,
cómo está montada, con qué entorno trabaja y qué tener en cuenta antes de tocarla.

---

## 1. Qué es

API **ASP.NET MVC 5** (.NET Framework **4.8.1**, **x64**) que encapsula la lógica
de **SAP Business One** para dos consumidores:

1. **App móvil** (ventas, inventarios, compras, impresión de etiquetas ZPL).
2. **Web FISFIBER** (`C:\Paradox\FISFIBER\WEB\FISFIBER`), para generar órdenes de fabricación.

> No es Web API: son controladores MVC (`Controller`) que devuelven **XML
> serializado** o `string` plano. No hay Swagger ni documentación generada.

---

## 2. Arquitectura

```
        App móvil                 Web FISFIBER (:44351)
             │                            │
             │  XML en el body            │  POST /WebApp/GenerarOF
             ▼                            ▼
        ┌──────────────────────────────────────────┐
        │      RESTAPIFFISA  (IIS Express :52585)  │
        │                                          │
        │   Service Layer (HTTP)  ◄──┐             │
        │   DI API (COM, x64)     ◄──┤             │
        │   SQL directo           ◄──┘             │
        └───────┬───────────────┬──────────────────┘
                │               │
                ▼               ▼
         SAP B1 Service     SQL Server
         Layer :50000       WIN-RAFPRITOIEC / FISYS1
```

Rutas: ruta MVC por defecto `{controller}/{action}/{id}` (`App_Start/RouteConfig.cs`).

---

## 3. Entorno de trabajo actual

| Componente | Valor |
|---|---|
| API | IIS Express `http://localhost:52585` (SSL configurado en 44333) |
| SQL Server | `WIN-RAFPRITOIEC` / base `FISYS1` (SQL Server 2016) |
| SAP Service Layer | `https://WIN-RAFPRITOIEC:50000/b1s/v1` |
| DI API | Requiere **SAP B1 Client 64-bit** instalado (referencia COM `SAPbobsCOM`) |
| Web que la consume | `http://localhost:44351` |

Credenciales: ver `RESTAPIFFISA/Web.config` (`FFISAConnection`, `SapUser`,
`SapPassword`). No se duplican aquí.

Producción (referencia): `DAENERYS` / `PARADOX` / SL `https://daenerys:50000/b1s/v1`.
La cadena anterior está comentada en el propio `Web.config`.

---

## 4. Build y ejecución

```powershell
# El repo YA incluye packages/ versionado; no hace falta restore.
msbuild RESTAPIFFISA.sln /t:Build /p:Configuration=Debug /p:Platform=x64

# Ejecutar
iisexpress /site:RESTAPIFFISA /config:.vs\RESTAPIFFISA\config\applicationhost.config
```

- **Plataforma obligatoria x64**: el interop de `SAPbobsCOM` es de 64 bits.
  Compilar en `Any CPU` puede fallar en runtime con `BadImageFormatException`.
- `bin/`, `obj/` y `.vs/` están ignorados en git (se limpiaron en el commit `0d8e27b`).

---

## 5. Configuración (`Web.config`)

### `connectionStrings`

| Nombre | Uso |
|---|---|
| `FFISAConnection` | SQL directo a la base de la compañía (`FISYS1`). Aquí viven los SPs `SpPdxFF_*`. |

### `appSettings` en uso

| Clave | Uso |
|---|---|
| `SapServer` | Host del SQL/SAP para DI API |
| `SapDatabase` | Company DB de SAP |
| `SapUser` / `SapPassword` | Usuario SAP (Service Layer y DI API) |
| `ServiceLayer` | URL base del Service Layer (`https://host:50000/b1s/v1`) |
| `EmailHost` / `EmailPort` / `EmailFrom` / `EmailPassword` | SMTP Gmail para notificaciones |
| `ConsecutivosRuta` | JSON de consecutivos (entregas) |
| `ConsecutivosED*` / `ConsecutivosEntradasDirectasMP` | JSON de consecutivos de entradas directas |
| `PlantillasRuta` | Plantillas de etiquetas/entradas |
| `FechaVigencia` | Fecha límite que valida `HomeController.FechaActiva` |
| `EndPointActualizacionRollos` | **Callback a la web** (`http://localhost:44351/api/UpdateData`) |
| `EndPointNuevaEntrega` | **Callback a la web** (`http://localhost:44351/api/NuevaEntrega`) |

### Claves definidas pero **no usadas** (candidatas a limpieza)

`SapDbServerType`, `TimeOutSapConnect`, `User`, `Pass`, `ImpresoraPruebas`.

> `DbServerType` está **hardcodeado** como `BoDataServerTypes.dst_MSSQL2016` en
> `Logica.cs` (2 lugares) y `LogicaVentasMovilDIAPI.cs` (1 lugar).

---

## 6. Contrato de comunicación

### 6.1 Entrada: **XML**, no JSON

Los POST leen el body crudo y deserializan con `XmlSerializer`:

```csharp
using (var reader = new StreamReader(Request.InputStream))
{
    string xmlData = reader.ReadToEnd();
    var serializer = new XmlSerializer(typeof(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV));
    RequestData = (EntregasMercanciaVentasHHEMOV)serializer.Deserialize(new StringReader(xmlData));
}
```

Aplica a `VentasMovil`, `InventariosMovil`, `Compras`, `ImpresionEtiquetas` y `Home`.
Enviar JSON rompe el binding.

Excepciones:
- `LoginController.ValidaUsuario`: `GET` con headers **`Usuario`** y **`Password`**.
- `LoginController.CerrarSesionActiva`: `GET` con header **`Usuario`**.
- `WebAppController.GenerarOF`: `POST` **form-urlencoded** (`PlanProduccion`).

### 6.2 Salida: XML de `JsonResponse`

```xml
<JsonResponse>
  <Status>OK|NO|ERROR</Status>
  <Message>texto</Message>
  <Data>...XML serializado de la lista...</Data>
</JsonResponse>
```

Clases: `AccesoDatos.JsonResponse`, `JsonResponse2` (con `TotalRegistros` /
`TotalPaginas`). `Data` viaja como string XML para evitar problemas de serialización.

Excepción: `WebApp/GenerarOF` devuelve texto plano: `"OK"` o `"Error: ..."`.

---

## 7. Mapa de endpoints (89 acciones)

| Controller | Módulo | Acciones representativas |
|---|---|---|
| `LoginController` | Sesión | `ValidaUsuario`, `CerrarSesionActiva` |
| `HomeController` | Utilidades | `ValidaUsuario`, `FechaActiva` |
| `VentasMovilController` | Entregas de venta | `GenerarEntregaMercanciaOV`, `CancelarEntregaMercancia`, `GetOrdenesVenta`, `GetRollosHHEMOV`, `ValidarOrdenVentaHHEMOV` |
| `InventariosMovilController` | Inventarios | `GenerarEntradaMercancia`, `GenerarSalidaMercancia`, `GenerarTransferenciaStock`, `GenerarRecuentoInventarios`, + CRUD de encabezados/líneas |
| `ComprasController` | Compras / EM | `GetOrdenesCompra`, `CreateEMByOC`, `CreateEMD`, `AutorizarSolicitudHHOC`, `ValidarCantidadEMvsOC` |
| `ImpresionEtiquetasController` | Etiquetas ZPL | `Crear_ZPL_Pacas`, `Crear_ZPL_Tubos`, `GetImpresorasRed`, `EnviarZPLConValidacion` |
| `WebAppController` | Integración web | `GenerarOF` (POST form-urlencoded) |

---

## 8. Acceso a datos

Cuatro clases de comandos en `Models/`, con **95 SPs distintos** `SpPdxFF_*`:

| Archivo | SPs | Dominio |
|---|---|---|
| `AccesoDatos.cs` | 26 | Compras / entradas directas |
| `AccesoDatosInventarios.cs` | 49 | Entradas, salidas, traspasos, recuentos |
| `AccesoDatosVentas.cs` | 18 | Entregas de venta |
| `AccesoDatosWeb.cs` | 2 | Órdenes de fabricación (web) |

Se ejecutan con `GlobalCommands.ExecuteProcedure` contra `FFISAConnection`.
Los SPs viven **en la misma company DB de SAP** (`FISYS1`), no en una base aparte.

---

## 9. Integraciones externas

### 9.1 Doble vía con SAP

- **Service Layer** (`LoginServiceLayer`, `HttpClient` + cookies `B1SESSION`):
  usada por ventas, inventarios, web.
- **DI API** (`SAPbobsCOM`, COM x64): usada por `Logica.cs` (entradas de
  mercancía de compras) y `LogicaVentasMovilDIAPI.cs`.

`LoginServiceLayer` desactiva la validación de certificado:

```csharp
ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
```

### 9.2 Callbacks hacia la Web

La API notifica a la web (SignalR del lado web) en:

- `EndPointActualizacionRollos` → `POST http://localhost:44351/api/UpdateData`
- `EndPointNuevaEntrega` → `POST http://localhost:44351/api/NuevaEntrega`

Ambos endpoints existen en `WEB\FISFIBER\Fisfiber\Controllers\ApiController.cs`.
Si la web está apagada, la API solo lo registra en el log y continúa.

### 9.3 Correo y etiquetas

- SMTP Gmail (`Email*`) para autorizaciones y notificaciones.
- ZPL a impresoras de red (`ImpresionEtiquetasController`).
- Consecutivos en archivos JSON bajo `C:\Paradox\FFISA\...`.

---

## 10. Perfiles

La API **no autoriza**: solo devuelve `u_perfil` desde `SpPdxFF_ValidaUsuario`.
Los valores válidos y su significado están documentados en
`WEB\FISFIBER\CONTEXT.md` (sección 3). El único nombre de administrador es
**`Admin`** (no `ADMIN` ni `Administrador`).

---

## 11. Gotchas / deuda técnica

1. **Sin autenticación ni autorización**: no hay `[Authorize]`, API key ni token.
   Cualquiera con acceso al puerto puede invocar los 89 endpoints y mover
   inventario/ventas en SAP.
2. **Entrada XML obligatoria** en los POST; no hay soporte JSON.
3. **log4net mal configurado**: el `<root>` referencia un appender
   `RollingFileAppender` que **no está definido** (solo existen
   `AppenderVentasMovil` y `AppenderInventariosMovil`). El log raíz no escribe.
4. **Certificado del Service Layer sin validar** (MITM posible en producción).
5. **`DbServerType` hardcodeado** a SQL 2016 en el código; la clave
   `SapDbServerType` no se lee.
6. **Faltan 3 SPs en `FISYS1`** respecto al código:
   `SpPdxFF_ActualizaDocumentosHHEMD`, `SpPdxFF_GetDetailsDocumentosHHEMD`,
   `SpPdxFF_GetLotesDocumentosHHEMD`.
7. **`FISYS1` sin datos maestros** (0 artículos, 0 socios): los flujos reales
   fallarán aunque las credenciales sean correctas.
8. El Service Layer **no** está en el puerto 30000 (ese es SLD/licencias);
   el correcto es **50000**.

---

## 12. Historial reciente

| Commit | Cambio |
|---|---|
| `0d8e27b` | Ignorar `bin/` y `obj/` y dejar de rastrearlos (324 archivos) |
| `c8e73cf` | Dejar de rastrear `.vs/` |
| `475356b` | Credenciales a `WIN-RAFPRITOIEC` / `FISYS1` / SQL 2016 |
| `450cf81` | Corregir puerto del Service Layer a `50000` |
| `17351ac` | Apuntar `SapDatabase` y `FFISAConnection` a `FISYS1` |
