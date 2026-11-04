MSMultilider.Finanzas
========================================
Microservicio independiente junto a backend, frontend, Seguridad y Propiedades.
.NET 10 / ASP.NET Core / EF Core 10 / SQL Server. Puerto local: 5214.
Fecha de creación: 4 de noviembre de 2026 (America/La_Paz).

MÓDULOS IMPLEMENTADOS
- Registro idempotente de ventas desde la API de Comercial.
- Cobranzas al contado y cuota inicial; pagos completos de cuotas en orden.
- Plan francés mensual con tasa anual, importes decimales y ajuste final de centavos.
- Amortización extraordinaria que recalcula SOLO capital pendiente.
- Reprogramación desde la primera cuota pendiente conservando cuotas reemplazadas.
- Ingresos automáticos por pago y manuales, egresos manuales, desembolsos y anulaciones.
- Comisión pendiente creada desde la venta, liquidación y consulta por asesor/moneda.
- Métodos de pago, cuentas bancarias, saldo y comprobantes PDF internos no fiscales.
- Reversión de pago ordinario: revierte ingreso y reabre obligación; no borra el historial.
- No se revierten automáticamente amortizaciones ni comisiones ya desembolsadas.

EJECUCIÓN EN VISUAL STUDIO
1. Abre MSMultilider.Finanzas.sln en Visual Studio 2026 con SDK .NET 10 compatible.
2. Espera la restauración de NuGet y establece MSMultilider.Finanzas como proyecto de inicio.
3. Clic derecho en el proyecto > Administrar secretos de usuario.
   Configura Jwt:Key, Jwt:Issuer y Jwt:Audience con los valores efectivos de Seguridad.
   Usa la misma clave real (mínimo 32 bytes UTF-8); no copies claves de ejemplo del repositorio.
   No uses los valores de autenticación de las pruebas como credenciales de desarrollo.
   Puedes sobrescribir ConnectionStrings:SqlServer allí si necesitas otra instancia/usuario.
4. appsettings.Development.json propone Windows Authentication a localhost y una base NUEVA:
   multilider_finanzas_dev.
   TrustServerCertificate=True se limita al desarrollo local.
   Esta creación no ha aplicado migraciones sobre tu base de desarrollo ni sobre tecnoweb.
5. Desde una terminal ubicada en esta carpeta:
   dotnet tool restore
   dotnet tool run dotnet-ef -- database update
   El usuario SQL debe poder crear la base/tablas o un administrador debe prepararlas.
   Las migraciones iniciales están incluidas en Data/Migrations.
6. Presiona F5 con el perfil http. Swagger: http://localhost:5214/swagger
   Alternativa de terminal:
   dotnet run --project MSMultilider.Finanzas.csproj --launch-profile http

Ejemplo de secrets.json: sustituye los marcadores y NO lo guardes en el repositorio.
{
  "Jwt": {
    "Key": "MISMA_CLAVE_PRIVADA_DE_SEGURIDAD_MINIMO_32_BYTES",
    "Issuer": "MSMultilider.Seguridad",
    "Audience": "MSMultilider.Frontend"
  }
}
Si Seguridad usa otros issuer/audience, copia los suyos. No es obligatorio usar los ejemplos.
Secret Manager es almacenamiento local de desarrollo, no un almacén cifrado de producción.
No hay usuarios/contraseñas de prueba sembrados ni migración automática de datos Laravel.
La API NO emite tokens: inicia sesión en Seguridad y usa ese JWT en Authorize de Swagger.
Health: GET /health. Readiness autenticada: GET /health/ready, verifica conexión y migraciones.
Swagger está habilitado solamente en Development.

INTEGRACIÓN ENTRE SERVICIOS
- Comercial consulta GET Propiedades /api/propiedades/{id}, con el Bearer del solicitante.
- Finanzas registra POST /api/ventas/{id}/registrar y consulta Comercial
  GET /api/ventas/{id}/financiacion. Se generan saldo, plan, pago inicial y comisión.
- Para registrar una venta en Finanzas, el JWT necesita acceso_pagos y acceso_ventas.
- Para anular una venta, Comercial invoca POST Finanzas /api/ventas/{id}/anular.
  También necesita ambos permisos. No se anula si hay cobros o comisiones pagadas.
- Finanzas conserva un marcador de anulación incluso si todavía no recibió la venta,
  evitando que un reintento tardío vuelva a crear su deuda.
- Esta coordinación usa solicitudes HTTP explícitas; no hay bus de eventos ni outbox.
  La inscripción financiera es un paso explícito y reintentable, no un efecto automático
  confirmado al crear una venta. Ante un fallo, reintenta con el mismo identificador.
- No hay claves foráneas entre bases ni acceso SQL a las bases de los otros servicios.
- UserId corresponde al identificador externo de Seguridad y no crea un usuario local.

CONFIGURACIÓN
ConnectionStrings:SqlServer / ConnectionStrings__SqlServer
Jwt:Key / Jwt__Key; Jwt:Issuer / Jwt__Issuer; Jwt:Audience / Jwt__Audience
Services:Propiedades = http://127.0.0.1:5212/
Services:Comercial   = http://127.0.0.1:5213/
Services:Finanzas    = http://127.0.0.1:5214/
Cors:AllowedOrigins = lista explícita de orígenes frontend
Storage:Path        = directorio de documentos privados de Comercial
En producción: archivos privados de entorno, permisos limitados y certificado SQL válido.
No incluir claves ni cadenas con contraseñas en Git, archivos .http ni logs.

CONTRATOS DE API
JSON de entrada/salida: snake_case. Monedas: BOB y USD. Dinero: decimal y 2 decimales.
Tasas: porcentaje anual para interés y máximo 2 decimales.
Listado paginado: data, total, page_number, per_page, last_page; per_page entre 1 y 100.
SolicitudId: UUID nuevo para una operación nueva; conservarlo si reintentas la MISMA.
El mismo UUID con otros datos produce 409, nunca vuelve a cobrar ni a reprogramar.
400 = estructura/validación, 401 = JWT inválido, 403 = falta permiso,
404 = registro ausente, 409 = conflicto/transición, 422 = regla de negocio,
503 = dependencia no disponible. No se devuelven trazas ni credenciales.
Concurrencia: versión por registro, índices únicos y transacciones financieras serializables.
Los endpoints están protegidos con los claims permission emitidos por Seguridad.
acceso_clientes / acceso_asesores / acceso_ventas / acceso_historial_ventas /
acceso_contratos / acceso_entregas / acceso_pagos / acceso_ingresos / acceso_egresos.

ENDPOINTS
POST               /api/ventas/{id}/registrar
POST               /api/ventas/{id}/anular
GET                /api/ventas/{id}/resumen
GET                /api/ventas/{id}/plan-pago
POST               /api/planes-pago/{id}/amortizar
POST               /api/planes-pago/{id}/reprogramar
GET                /api/planes-pago/{id}/reprogramaciones
GET/POST           /api/pagos
GET                /api/pagos/{id}
POST               /api/pagos/{id}/procesar
PUT                /api/pagos/{id}/cancelar
GET                /api/pagos/{id}/comprobante
GET/POST           /api/ingresos
GET/PUT            /api/ingresos/{id}
PUT                /api/ingresos/{id}/anular
GET                /api/ingresos/resumen
GET/POST           /api/egresos
GET/PUT            /api/egresos/{id}
PUT                /api/egresos/{id}/pagar
PUT                /api/egresos/{id}/anular
GET                /api/egresos/resumen
GET                /api/comisiones-asesores
GET                /api/comisiones-asesores/{asesorId}/impagas
GET                /api/comisiones-asesores/{asesorId}/pagadas
GET                /api/comisiones-asesores/egreso/{id}/comprobante
GET/POST           /api/metodos-pago
PUT                /api/metodos-pago/{id}
GET/POST           /api/cuentas-bancarias
PUT                /api/cuentas-bancarias/{id}
GET                /api/cuentas-bancarias/{id}/saldo

LÍMITES DE LA MIGRACIÓN
Esta entrega crea servicios nuevos funcionales, no cambia las rutas de Laravel ni Vue.
No está desplegada en el VPS ni copia datos comerciales reales.
Antes de activar el tráfico real hay que mapear IDs/datos/permisos, adaptar Laravel/Vue,
y verificar el flujo de extremo a extremo con Seguridad y Propiedades reales.
Comercial evita ventas activas duplicadas en SU base y consulta disponibilidad,
pero el servicio Propiedades existente no ofrece reserva atómica. No actualiza su
estado global ni evita ventas concurrentes realizadas por el Laravel antiguo.
Se requiere añadir esa operación en Propiedades antes de habilitar ambos escritores.
Mantén un único origen de escritura durante la transición; no actives la sincronización
antigua de dos minutos sobre datos que todavía se modifican en ambos sistemas.
El PDF de contrato es un resumen BORRADOR, no la plantilla legal del Laravel actual.
Documentos adjuntos: PDF hasta 10 MiB, privados y descargables con autorización.
Se conservan versiones de archivo antiguas; incluir storage en respaldos/retención.
TasaMora se conserva como dato; no se inventan ni aplican penalidades automáticas.
No hay integración con Libélula ni movimientos bancarios reales: son registros internos.
No se trasladaron en esta entrega reportes globales, correo ni portal del cliente.

VERIFICACIÓN
dotnet test MSMultilider.Finanzas.sln
La mayoría de pruebas usan SQLite en memoria y dobles HTTP para dependencias externas.
Prueba opcional de SQL Server local: crea y elimina SOLO una base temporal con UUID:
  PowerShell: $env:MULTILIDER_SQLSERVER_TESTS='1'
  dotnet test MSMultilider.Finanzas.sln
Verifica migraciones reales, índices únicos y concurrencia.
Las claves JWT de tests están marcadas como fixtures sin validez fuera de esas pruebas.

PUBLICACIÓN POSTERIOR
dotnet publish MSMultilider.Finanzas.csproj -c Release -o publish
Desplegar como proceso/usuario independiente con base propia y permisos limitados.
deploy/ contiene plantillas de systemd y entorno; NO se han instalado en el VPS.
No configurar actualizaciones automáticas de Git.
Respaldar base SQL y, en Comercial, Storage:Path antes de una actualización.

