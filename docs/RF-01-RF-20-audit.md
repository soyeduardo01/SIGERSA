# Auditoría funcional SIGERSA — RF-01 a RF-20

Fecha de cierre técnico: 2026-09-14

## Resultado ejecutivo

La revisión se realizó de extremo a extremo sobre el esquema PostgreSQL, repositorios Dapper, servicios de aplicación, API ASP.NET Core y frontend PWA. Los faltantes detectados fueron implementados y las consultas principales fueron ejecutadas contra la base configurada. Los módulos operativos usan datos persistidos; únicamente el resumen del dashboard puede mostrar el conjunto demostrativo autorizado cuando aún no existen métricas reales.

| Requisito | Estado final | Evidencia técnica verificada |
|---|---|---|
| RF-01 Autenticación | Cumple | Inicio, refresh rotativo, cierre, recuperación OTP, cambio de clave y bloqueo por intentos están en `AuthService`, `AuthenticationRepository` y `ProfileService`. Las claves nuevas usan bcrypt con factor 12 y lectura compatible de hashes históricos. El 2FA por OTP de correo es configurable mediante `Authentication:Flow:TwoFactorEnabled`, con propósito separado por la migración 014. Los correos de recuperación y segundo factor usan la plantilla HTML adaptativa de SIGERSA, enlace contextual y alternativa en texto plano. |
| RF-02 Usuarios | Cumple | El registro multipágina público `/registro.html` permite solicitar exclusivamente `ADMINISTRADOR_EMPRESA` o `USUARIO_DELEGADO`, con datos personales, contraseña segura, consentimiento y carta obligatoria. La carta se carga al almacenamiento privado de Supabase y la migración 016 persiste rol solicitado y consentimiento. Gestión de Usuarios muestra la solicitud y permite al administrador consultar la evidencia, asignar empresa y pasarla entre los estados visibles exactos `Pendiente Validación`, `Aprobado` y `Rechazado`. No puede aprobarse sin documento vigente ni empresa asignada. |
| RF-03 Empresas | Cumple | CRUD real en `CompanyRepository`/`CompaniesController`; migración 013 agrega dirección y municipio, con provincia derivada por FK. Los contactos legal, calidad y principal se persisten en `CONTACTO`. Crear/editar genera auditoría transaccional y la interfaz enlaza actividad y evaluaciones asociadas. |
| RF-04 Dashboards por rol | Cumple | `OperationalRepository.GetDashboardAsync` calcula métricas reales y respeta ámbito global, empresarial o asignado. `DashboardOverview` presenta tarjetas específicas para empresa, coordinador y técnico, además de gráfico de barras y distribución tipo donut. Cuando la base aún no tiene actividad, solo este resumen usa valores demostrativos claramente identificados. |
| RF-05 Solicitudes BPM | Cumple | Borrador y envío idempotentes, empresa/establecimiento/tipo/motivo/observaciones persistidos. `SOLICITUD_DOCUMENTO` registra soporte obligatorio; el servicio y SQL impiden enviar sin documento. La migración 015 usa el estado final exacto `PENDIENTE_ASIGNACION`. |
| RF-06 Casos | Cumple | `CaseRepository` valida y enlaza las cuatro fuentes: solicitud, programación institucional, alerta LAPCH y denuncia. La creación es transaccional e idempotente y la UI filtra los orígenes disponibles. |
| RF-07 Programación | Cumple | Programar, reprogramar y cancelar están implementados con control de concurrencia. `PROGRAMACION_HISTORIAL` (migración 013) conserva valores anterior/nuevo, estado, prioridad, motivo, actor y fecha. |
| RF-08 Alertas LAPCH | Cumple | `OperationalRepository` y `SurveillanceManagement` registran número, fecha, producto, empresa/establecimiento, descripción, prioridad y decisión. Los casos solo pueden originarse en alertas con `PROCEDE_EVALUACION`; el cierre se gestiona desde el caso vinculado. |
| RF-09 Denuncias | Cumple | Registro de tipo, recepción, canal, denunciante protegido, descripción, anonimato/confidencialidad y resultados `PROCEDE`, `NO_PROCEDE` y `REMITIDA_OTRO_PROCESO`. Un resultado procedente queda disponible como origen de caso/evaluación. |
| RF-10 Asignación | Cumple | `SchedulingRepository` asigna y reasigna técnicos, revoca asignaciones anteriores y registra motivo/actor. Las opciones y el calendario muestran solicitud cuando existe, empresa, prioridad y origen del caso. |
| RF-11 Calendario | Cumple | El técnico dispone de acceso de solo lectura y ve las vistas día, semana y mes. Cada evento expone empresa, establecimiento, dirección/municipio, horario, técnicos, prioridad y estado. |
| RF-12 Ejecución | Cumple | `EvaluationWorkflowRepository` publica y sirve la ficha jerárquica desde `AllItems`; inicio, borrador parcial, cálculo/finalización y envío tienen transiciones y control de versión. La captura se organiza dinámicamente por las secciones de la ficha vigente. |
| RF-13 Formulario basado en riesgo | Cumple | Cada respuesta conserva calificación, observación y comentario; la evidencia puede ligarse al ítem exacto mediante `EVIDENCIA_RESPUESTA`. Se mantienen `CUMPLE`, `NO_CUMPLE`, `NO_APLICA` y, por decisión funcional explícita, `CUMPLE_PARCIAL` con valor 0.5. Las secciones proceden de la plantilla BPM publicada. |
| RF-14 Motor de riesgo | Cumple | `InspectionRiskService` calcula y `EvaluationWorkflowRepository.SaveCalculationAsync` persiste porcentaje, riesgos, nivel, frecuencia, snapshot y hash al finalizar. Pruebas de frontera validan 3.6 como bajo/anual, >3.6–6.3 como medio/semestral y >6.3 como alto/trimestral. |
| RF-15 Evidencias | Cumple | PDF/JPG/PNG/MP4 con límite y hash, Storage privado y confirmación idempotente. Latitud, longitud y precisión opcionales se capturan con permiso del dispositivo y se persisten. IndexedDB conserva archivo y metadatos; la cola reintenta y sincroniza automáticamente al recuperar conectividad. |
| RF-16 Informe | Cumple | `ReportService` genera PDF estructurado con resumen, riesgo, hallazgos/no conformidades, recomendaciones y anexo completo. El generador pagina sin truncar hallazgos o evidencias y una prueba cubre un informe de varias páginas. |
| RF-17 Revisión | Cumple | Envío, revisión y aprobación están restringidos por rol y estado. Las respuestas solo admiten cambios en `EN_EJECUCION` o `EN_CORRECCION`; tras enviar quedan bloqueadas hasta una corrección formal. |
| RF-18 Correcciones | Cumple | `CORRECCION_CAMPO` conserva ítem, razón, respuesta anterior/nueva y estado. La UI muestra observaciones puntuales, permite elegir criterios y reenviar; aceptar devuelve a revisión y rechazar mantiene el ciclo de corrección. |
| RF-19 Cierre | Cumple | El informe oficial solo se emite con evaluación aprobada. El cierre exige una versión oficial existente, registra fecha de cierre y cierra el caso en la misma operación. La UI permite generar, emitir y descargar el PDF. |
| RF-20 Histórico | Cumple | Búsqueda por empresa, solicitud, caso/evaluación, rango y estado; resultados incluyen puntaje, cumplimiento, riesgo, frecuencia e informe. La línea de tiempo une creación, transiciones, correcciones, evidencias e informes. |

## Cambios de persistencia

- `013_complete_functional_support.sql`: dirección/municipio de empresa, comentarios de respuesta, cartas de autorización, documentos de solicitud y trazabilidad de programación.
- `014_two_factor_authentication.sql`: propósito persistido e indexado para separar OTP de recuperación y de segundo factor.
- `015_request_pending_assignment_status.sql`: normalización del estado enviado a `PENDIENTE_ASIGNACION`, incluida la migración de registros existentes y restricciones de integridad.
- `016_public_user_registration.sql`: rol empresarial solicitado, aceptación de términos, integridad de estados y consulta eficiente de solicitudes públicas pendientes.

Las cuatro migraciones son idempotentes y fueron aplicadas sobre la base configurada. Las consultas reales de solicitudes, casos, programación, evaluaciones, evidencias, correcciones, empresas, dashboard, vigilancia, hallazgos, histórico, auditoría, activación y documentos se ejecutaron sin error.

## Verificación reproducible

```text
Backend: 106 pruebas superadas, 0 fallidas.
Frontend: 41 pruebas superadas, 0 fallidas.
Frontend lint: 0 errores, 0 advertencias.
Build frontend: producción generada; service worker y manifiesto incluidos.
SQL smoke: consultas de todos los módulos operativos ejecutadas contra PostgreSQL.
API en ejecución: el registro público respondió validación 400 en lugar de autorización 401 ante una solicitud incompleta, confirmando que la ruta es anónima; el resto de los endpoints conserva autenticación y ámbito por rol.
```

El aviso de tamaño del paquete frontend es una recomendación de optimización de carga, no un error de compilación ni de funcionamiento.
