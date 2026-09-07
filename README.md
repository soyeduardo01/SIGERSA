# SIGERSA - Sistema PWA para Evaluación Basada en Riesgo (EBR/BPM)

SIGERSA digitaliza el ciclo completo de las evaluaciones e inspecciones de Buenas Prácticas de Manufactura: registro de casos, planificación, trabajo de campo, evidencias, cálculo de riesgo, revisión, correcciones, cierre y trazabilidad histórica. El proyecto se desarrolla mediante Spec-Driven Development (SDD), usando las especificaciones versionadas como base para la arquitectura, la implementación y las pruebas.

## Stack tecnológico

- **Backend:** .NET 9 Web API con Clean Architecture y Dapper.
- **Frontend:** React PWA con TypeScript y Tailwind CSS.
- **Persistencia:** PostgreSQL 15 o superior.
- **Archivos:** Supabase Storage para evidencias, informes y demás binarios.

## Restricción arquitectónica

**ADVERTENCIA: todo desarrollo de base de datos se realizará obligatoriamente bajo el esquema PostgreSQL `SIGERSA`; ninguna tabla de negocio deberá crearse en `public` ni en otro esquema.**

## Inicio rápido

### Requisitos

- .NET SDK 9.
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

Los comandos de ejecución del backend y del frontend se documentarán cuando sus proyectos sean inicializados.

## Estructura SDD

- `.sdd/specs/`: requisitos, contratos y especificaciones verificables.
- `.sdd/architecture/`: decisiones y modelos de arquitectura.
- `.sdd/tests/`: criterios y especificaciones de prueba.
- `.agents/`: espacios de trabajo y utilidades de los agentes especializados.
