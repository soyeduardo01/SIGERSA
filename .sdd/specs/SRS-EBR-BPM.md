# Especificación base de SIGERSA (EBR/BPM)

## 1. Introducción

SIGERSA es una aplicación web progresiva (PWA) orientada a gestionar el ciclo integral de las Evaluaciones Basadas en Riesgo de establecimientos sujetos a inspecciones de Buenas Prácticas de Manufactura (EBR/BPM). La solución centralizará el registro de solicitudes y casos, la planificación y asignación de inspecciones, la ejecución en campo, la captura de evidencias, el cálculo de riesgo, la revisión de informes, las correcciones, el cierre y el seguimiento histórico.

La PWA deberá funcionar en equipos de escritorio y dispositivos móviles, y deberá tolerar conectividad intermitente mediante capacidades offline y sincronización controlada. El backend expondrá una API REST desacoplada y PostgreSQL será la fuente transaccional de verdad.

> **Regla arquitectónica obligatoria:** la base de datos PostgreSQL usará de forma mandatoria el esquema `SIGERSA` para todas sus tablas. No se crearán tablas de negocio en el esquema `public` ni en ningún otro esquema.

## 2. Propósito del documento

Este documento establece la base funcional y arquitectónica que guiará el análisis, el diseño, la implementación y las pruebas mediante Spec-Driven Development (SDD). Las especificaciones aprobadas serán la referencia verificable para los contratos, las decisiones técnicas y los criterios de aceptación.

## 3. Objetivos de negocio

| ID | Objetivo |
| --- | --- |
| OB-01 | Estandarizar y digitalizar de extremo a extremo el proceso EBR/BPM. |
| OB-02 | Reducir errores de transcripción y cálculos manuales mediante validaciones y reglas centralizadas. |
| OB-03 | Priorizar las inspecciones según un riesgo sanitario trazable y verificable. |
| OB-04 | Garantizar trazabilidad y auditoría desde el origen de cada caso hasta su cierre. |
| OB-05 | Permitir que las fichas de evaluación evolucionen y se publiquen sin modificar el código de la aplicación. |
| OB-06 | Mantener la continuidad operativa en campo mediante trabajo offline y sincronización controlada. |
| OB-07 | Facilitar la supervisión, revisión y toma de decisiones con información e indicadores oportunos. |

## 4. Alcance inicial

- Administración de empresas, establecimientos, usuarios, roles, casos y evaluaciones.
- Gestión de solicitudes, programación institucional, alertas y denuncias.
- Diseño y versionado de fichas dinámicas de evaluación.
- Captura de respuestas, hallazgos, evidencias y geolocalización opcional.
- Cálculo de calificación BPM, no conformidades, riesgo y frecuencia de inspección.
- Generación y revisión de informes, planes de corrección e histórico auditable.
- Notificaciones por correo y controles de seguridad gestionados por el backend.

## 5. Principios de la solución

- PWA instalable, responsiva y preparada para operación offline.
- Separación entre frontend, API, aplicación, dominio e infraestructura.
- Seguridad basada en roles y alcance sobre cada recurso.
- Versionado inmutable de fichas y parámetros usados por evaluaciones históricas.
- Persistencia transaccional exclusivamente en tablas del esquema PostgreSQL `SIGERSA`.
