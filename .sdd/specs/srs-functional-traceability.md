# Trazabilidad funcional consolidada — SRS base + SRS V2

## Regla de consolidación

El SRS base define el alcance funcional completo de SIGERSA. El SRS V2 conserva ese alcance y añade requisitos verificables de seguridad, ámbito, auditoría, fichas, operación offline y evidencias. Cuando exista una contradicción, prevalece SRS V2, según su sección 1.

## Matriz de capacidades y módulos

| Capacidad consolidada | Fuente base | Regla V2 aplicable | Módulo UI | Ruta |
| --- | --- | --- | --- | --- |
| Resumen operativo e indicadores | OB-03, OB-07 | Matriz §10; RNF-001/003 | Resumen | `/resumen` |
| Empresas y perfil empresarial | Alcance §4 | RF-AUTZ-001/002/006; matriz §10 | Empresas | `/empresas` |
| Establecimientos | Alcance §4 | RF-AUTZ-001/002; matriz §10 | Establecimientos | `/establecimientos` |
| Usuarios, roles y permisos | Alcance §4 | RF-AUTZ-006/007/008; matriz §10 | Usuarios | `/usuarios` |
| Parámetros y catálogos | OB-05; Alcance §4 | RF-PAR-001..007 | Parámetros | `/parametros` |
| Solicitudes | Alcance §4 | Flujo §8; matriz §10 | Solicitudes | `/solicitudes` |
| Alertas y denuncias | Alcance §4 | Flujo §8; matriz §10 | Alertas y denuncias | `/alertas-denuncias` |
| Casos, análisis y priorización | Alcance §4 | Flujo §8; RF-AUTZ-005 | Casos | `/casos` |
| Programación y asignación | Alcance §4 | Flujo §8; matriz §10 | Programación | `/programacion` |
| Ejecución de evaluaciones | Alcance §4 | RF-AUTZ-003/004; RF-DAT-002/005 | Evaluaciones | `/evaluaciones` |
| Fichas dinámicas y versionado | OB-05; Alcance §4 | RF-FIC-001..008 | Fichas BPM | `/fichas-bpm` |
| Hallazgos y no conformidades | Alcance §4 | Flujo §8; matriz §10 | Hallazgos | `/hallazgos` |
| Evidencias y geolocalización | Alcance §4 | RF-EVI-001..009 | Evidencias | `/evidencias` |
| Planes y respuestas de corrección | Alcance §4 | Flujo §8; matriz §10 | Correcciones | `/correcciones` |
| Informes, revisión e histórico | OB-04/07; Alcance §4 | RF-DAT-003/004; matriz §10 | Informes y reportes | `/reportes` |
| Auditoría | OB-04; Alcance §4 | RF-AUT-008; RF-FIC-008; RNF-004 | Auditoría | `/auditoria` |
| Perfil del usuario | Seguridad por rol y alcance | RF-AUTZ-001; matriz §10 | Mi perfil | `/perfil` |

## Criterios transversales de implementación

1. Denegar acceso por defecto; el menú y las rutas consumen una misma matriz RBAC.
2. Una ruta visible no sustituye la autorización del API: cada operación valida rol, ámbito y propiedad.
3. Ningún módulo visible puede ser una vista vacía; debe ofrecer al menos estado, búsqueda/filtros, tabla o flujo principal, acciones y formularios/modales acordes al permiso.
4. Las operaciones destructivas requieren confirmación; éxitos se comunican como toast y errores mediante alertas estandarizadas.
5. Las escrituras deben conservar auditoría y control de concurrencia; las mutaciones offline serán idempotentes.
6. Evidencias permanecen en Supabase Storage privado; la base de datos conserva sólo metadatos.
