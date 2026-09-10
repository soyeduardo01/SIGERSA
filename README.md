# SIGERSA - Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria

SIGERSA digitaliza el ciclo completo de las evaluaciones e inspecciones de Buenas Prácticas de Manufactura: registro de casos, planificación, trabajo de campo, evidencias, cálculo de riesgo, revisión, correcciones, cierre y trazabilidad histórica. El proyecto se desarrolla mediante Spec-Driven Development (SDD), usando las especificaciones versionadas como base para la arquitectura, la implementación y las pruebas.

## Stack tecnológico

- **Backend:** .NET 10 Web API con Clean Architecture, Dapper y Npgsql.
- **Frontend:** React PWA con TypeScript y Tailwind CSS.
- **Persistencia:** PostgreSQL 15 o superior.
- **Archivos:** Supabase Storage para evidencias, informes y demás binarios.

## Restricción arquitectónica

**ADVERTENCIA: todo desarrollo de base de datos se realizará obligatoriamente bajo el esquema PostgreSQL `SIGERSA`; ninguna tabla de negocio deberá crearse en `public` ni en otro esquema.**

## Inicio rápido

### Requisitos

- .NET SDK 10.
- Node.js y un gestor de paquetes compatible con el frontend.
- PostgreSQL 15 o superior instalado y en ejecución.
- Puerto local `5432` disponible, salvo que se configure otro puerto.

### Preparar PostgreSQL

Configure una instancia accesible de PostgreSQL y cree la base de datos transaccional:

```sql
CREATE DATABASE sigersa_db;
```

Las tablas y demás objetos de negocio se crearán exclusivamente dentro del esquema obligatorio `SIGERSA` mediante las migraciones SQL del proyecto.

### Configurar la aplicación

La cadena de conexión deberá suministrarse mediante configuración local o variables de entorno. No almacene credenciales reales en el repositorio.

Defina los secretos fuera del repositorio. Por ejemplo, para la API:

```powershell
$env:Database__ConnectionString = "Host=localhost;Port=5432;Database=sigersa_db;Username=postgres;Password=SU_CLAVE"
$env:Supabase__Url = "https://SU_PROYECTO.supabase.co"
$env:Supabase__Key = "SU_CLAVE_SECRETA_DE_SERVIDOR"
```

Restaure, compile, pruebe e inicie el backend:

```powershell
dotnet restore src/backend/SIGERSA.sln --configfile NuGet.Config
dotnet build src/backend/SIGERSA.sln --no-restore
dotnet test src/backend/SIGERSA.sln --no-build
dotnet run --project src/backend/SIGERSA.Api
```

Swagger UI estará disponible en `/swagger` durante el desarrollo. La aplicación no
ejecuta migraciones automáticamente: aplique, en orden por nombre, todos los archivos
SQL de `src/backend/Database/Migrations`, desde `001_initial_schema_sigersa.sql`.

### Iniciar el frontend PWA

Copie `src/frontend/.env.example` como `src/frontend/.env.local` y complete las variables públicas de Supabase. La clave usada en el navegador debe ser la clave publicable o `anon`; nunca utilice una clave de servicio.

```powershell
cd src/frontend
pnpm install
pnpm dev
```

La PWA mantiene evaluaciones, fichas y una cola de sincronización en IndexedDB. Las respuestas y evidencias pendientes conservan una `idempotency_key` para evitar duplicados al recuperar la conexión. Cuando hay red, la API autoriza una ruta temporal después de validar la asignación; el navegador carga mediante `supabase-js` y el backend verifica el objeto antes de confirmar sus metadatos.

Para validar un cambio del frontend:

```powershell
pnpm format:check
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

### Calidad y OpenAPI

`dotnet format` forma parte del SDK .NET 10. El CLI de Swashbuckle se instala como herramienta local del repositorio:

```powershell
dotnet tool restore
dotnet format src/backend/SIGERSA.sln --verify-no-changes
dotnet swagger tofile --output .sdd/specs/openapi-v1.json src/backend/SIGERSA.Api/bin/Debug/net10.0/SIGERSA.Api.dll v1
```

Nunca registre cuerpos de solicitudes, contraseñas, tokens ni claves de Supabase. La clave de servidor de Supabase debe permanecer únicamente en configuración segura del backend.

## Estructura SDD

- `.sdd/specs/`: requisitos, contratos y especificaciones verificables.
- `.sdd/architecture/`: decisiones y modelos de arquitectura.
- `.sdd/tests/`: criterios y especificaciones de prueba.
- `.agents/`: espacios de trabajo y utilidades de los agentes especializados.
