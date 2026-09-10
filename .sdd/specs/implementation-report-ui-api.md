# Reporte de implementación UI/API

## Regla de requisitos

Se realizó el cruce entre `SRS-EBR-BPM.md` y `SRS-EBR-BPM-V2.md`. El SRS base conserva el alcance funcional y V2 prevalece en seguridad, roles, ámbito, concurrencia, evidencias, parámetros y fichas. La matriz consolidada está en `srs-functional-traceability.md`.

## 1. Navegación y enrutamiento

### Sidebar y rutas

- `src/frontend/src/App.tsx`
- `src/frontend/src/components/layout/AppLayout.tsx`
- `src/frontend/src/components/layout/Sidebar.tsx`
- `src/frontend/src/components/layout/AccessDenied.tsx`
- `src/frontend/src/components/routing/ProtectedRoute.tsx`
- `src/frontend/src/components/layout/Sidebar.test.tsx`
- `src/frontend/src/components/routing/ProtectedRoute.test.tsx`

### RBAC y acceso a usuarios

- `src/frontend/src/contexts/AuthContext.tsx`
- `src/frontend/src/contexts/auth-context.ts`
- `src/frontend/src/contexts/useAuth.ts`
- `src/frontend/src/lib/rbac.ts`
- `src/backend/SIGERSA.Api/Controllers/UsersController.cs`
- `src/backend/SIGERSA.Application/Users/UserManagementContracts.cs`
- `src/backend/SIGERSA.Application/Users/UserManagementService.cs`
- `src/backend/SIGERSA.Application/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/backend/SIGERSA.Domain/Repositories/IUsuarioRepository.cs`
- `src/backend/SIGERSA.Domain/Security/UserManagementModels.cs`
- `src/backend/SIGERSA.Domain/Exceptions/ForbiddenException.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/UsuarioRepository.cs`
- `src/backend/SIGERSA.Infrastructure/DependencyInjection.cs`
- `src/backend/SIGERSA.Tests/Application/UserManagementServiceTests.cs`

### Perfil, rol, cierre de sesión y notificaciones

- `src/frontend/src/components/layout/Header.tsx`
- `src/frontend/src/components/layout/Header.test.tsx`
- `src/frontend/src/components/layout/AppLayout.tsx`
- `src/frontend/src/contexts/AuthContext.tsx`
- `src/frontend/src/features/operations/ModuleViews.tsx`

## 2. UI/UX

### Etiquetas y experiencia de Fichas BPM

- `src/frontend/src/features/admin/AllItemsAdmin.tsx`
- `src/frontend/src/features/admin/AllItemsAdmin.test.tsx`
- `src/frontend/src/features/inspection/DynamicInspectionForm.tsx`

### SweetAlert2 global

- `src/frontend/src/lib/alerts.ts`
- `src/frontend/src/main.tsx`
- `src/frontend/package.json`
- `src/frontend/pnpm-lock.yaml`
- `src/frontend/src/features/admin/AllItemsAdmin.tsx`
- `src/frontend/src/features/admin/UsersManagement.tsx`
- `src/frontend/src/features/auth/LoginPage.tsx`
- `src/frontend/src/features/auth/PasswordRecoveryPage.tsx`
- `src/frontend/src/features/inspection/DynamicInspectionForm.tsx`

### Gestión funcional de usuarios

- `src/frontend/src/features/admin/UsersManagement.tsx`
- `src/frontend/src/features/admin/UserFormModal.tsx`
- `src/frontend/src/features/admin/UsersManagement.test.tsx`
- `src/frontend/src/lib/api.ts`

### Módulos operativos y alcance consolidado

- `src/frontend/src/features/operations/OperationalModulePage.tsx`
- `src/frontend/src/features/operations/ModuleViews.tsx`
- `src/frontend/src/App.tsx`
- `src/frontend/src/components/layout/Sidebar.tsx`
- `src/frontend/src/lib/rbac.ts`
- `src/frontend/src/features/requests/RequestsManagement.tsx`
- `src/frontend/src/features/cases/CasesManagement.tsx`
- `src/frontend/src/features/scheduling/SchedulingManagement.tsx`
- `src/frontend/src/features/evaluations/EvaluationsManagement.tsx`
- `src/frontend/src/features/evidences/EvidenceManagement.tsx`
- `src/frontend/src/features/corrections/CorrectionsManagement.tsx`
- `src/frontend/src/offline/database.ts`
- `src/frontend/src/offline/syncQueue.ts`

Las rutas consolidadas cubren Resumen, Empresas, Establecimientos, Parámetros, Solicitudes, Alertas/denuncias, Casos, Programación, Evaluaciones, Fichas BPM, Hallazgos, Evidencias, Correcciones, Reportes, Usuarios, Auditoría y Perfil. Solicitudes, Casos, Programación, Evaluaciones, Evidencias y Correcciones consumen persistencia real; sus altas offline aplicables se encolan de forma idempotente y las respuestas/evidencias se sincronizan al recuperar conexión.

### Verticales API/BD operativos

- Controladores: `RequestsController`, `CasesController`, `SchedulingController`, `EvaluationsController`, `EvidenceController` y `CorrectionsController`.
- Servicios de aplicación con autorización por rol/ámbito y concurrencia.
- Repositorios Dapper con todas las tablas calificadas mediante el esquema `SIGERSA`.
- Evidencias subidas y descargadas exclusivamente mediante `SupabaseStorageAdapter`; PostgreSQL conserva metadatos y referencias.

## 3. Recuperación de contraseña

- `src/backend/SIGERSA.Application/Authentication/AuthService.cs`
- `src/backend/SIGERSA.Infrastructure/Email/SmtpEmailSender.cs`
- `src/backend/SIGERSA.Infrastructure/DependencyInjection.cs`
- `src/backend/SIGERSA.Domain/Exceptions/EmailDeliveryException.cs`
- `src/backend/SIGERSA.Api/ExceptionHandling/GlobalExceptionHandler.cs`
- `src/backend/SIGERSA.Api/appsettings.json`
- `src/backend/SIGERSA.Tests/Application/AuthServiceTests.cs`
- `src/frontend/src/features/auth/PasswordRecoveryPage.tsx`
- `src/frontend/src/features/auth/PasswordRecoveryPage.test.tsx`
- `src/frontend/.env.example`

## 4. Contratos y trazabilidad

- `.sdd/specs/srs-functional-traceability.md`
- `.sdd/specs/api-contracts.md`
- `.sdd/specs/openapi-v1.json`
- `.sdd/specs/implementation-report-ui-api.md`

## 5. Evidencia de verificación

- Backend: compilación sin advertencias y 78 pruebas aprobadas.
- Frontend: ESLint y TypeScript aprobados; 27 pruebas aprobadas.
- Compilación PWA de producción aprobada.
- Las consultas de lectura de los seis módulos se ejecutaron contra PostgreSQL configurado: SQL válido y esquema disponible.
- API ejecutándose y `GET /api/v1/system/status` responde HTTP 200.
- Frontend ejecutándose y responde HTTP 200.
- Las rutas privadas redirigen al login sin sesión; la navegación Login/Recuperación funciona.

## 6. Inventario exacto por entrega

### Router, Sidebar y sesión

- `src/frontend/src/App.tsx`
- `src/frontend/src/lib/rbac.ts`
- `src/frontend/src/lib/rbac.test.ts`
- `src/frontend/src/components/routing/ProtectedRoute.tsx`
- `src/frontend/src/components/routing/ProtectedRoute.test.tsx`
- `src/frontend/src/components/layout/AccessDenied.tsx`
- `src/frontend/src/components/layout/Sidebar.tsx`
- `src/frontend/src/components/layout/Sidebar.test.tsx`
- `src/frontend/src/components/layout/Header.tsx`
- `src/frontend/src/components/layout/Header.test.tsx`
- `src/frontend/src/components/layout/AppLayout.tsx`
- `src/frontend/src/contexts/AuthContext.tsx`
- `src/frontend/src/contexts/auth-context.ts`
- `src/frontend/src/contexts/useAuth.ts`
- `src/frontend/src/hooks/useSyncStatus.ts`
- `src/frontend/src/features/operations/ModuleViews.tsx`
- `src/frontend/src/index.css`

### Solicitudes

- `src/frontend/src/features/requests/RequestsManagement.tsx`
- `src/backend/SIGERSA.Api/Controllers/RequestsController.cs`
- `src/backend/SIGERSA.Application/Requests/InspectionRequestContracts.cs`
- `src/backend/SIGERSA.Application/Requests/InspectionRequestService.cs`
- `src/backend/SIGERSA.Domain/Entities/InspectionRequestModels.cs`
- `src/backend/SIGERSA.Domain/Repositories/IInspectionRequestRepository.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/InspectionRequestRepository.cs`
- `src/backend/SIGERSA.Tests/Application/InspectionRequestServiceTests.cs`

### Casos

- `src/frontend/src/features/cases/CasesManagement.tsx`
- `src/backend/SIGERSA.Api/Controllers/CasesController.cs`
- `src/backend/SIGERSA.Application/Cases/CaseContracts.cs`
- `src/backend/SIGERSA.Application/Cases/CaseService.cs`
- `src/backend/SIGERSA.Domain/Entities/CaseModels.cs`
- `src/backend/SIGERSA.Domain/Repositories/ICaseRepository.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/CaseRepository.cs`
- `src/backend/SIGERSA.Tests/Application/CaseServiceTests.cs`

### Programación

- `src/frontend/src/features/scheduling/SchedulingManagement.tsx`
- `src/backend/SIGERSA.Api/Controllers/SchedulingController.cs`
- `src/backend/SIGERSA.Application/Scheduling/SchedulingContracts.cs`
- `src/backend/SIGERSA.Application/Scheduling/SchedulingService.cs`
- `src/backend/SIGERSA.Domain/Entities/SchedulingModels.cs`
- `src/backend/SIGERSA.Domain/Repositories/ISchedulingRepository.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/SchedulingRepository.cs`
- `src/backend/SIGERSA.Tests/Application/SchedulingServiceTests.cs`

### Evaluaciones

- `src/frontend/src/features/evaluations/EvaluationsManagement.tsx`
- `src/frontend/src/features/inspection/DynamicInspectionForm.tsx`
- `src/backend/SIGERSA.Api/Controllers/EvaluationsController.cs`
- `src/backend/SIGERSA.Application/Evaluations/EvaluationWorkflowService.cs`
- `src/backend/SIGERSA.Domain/Entities/EvaluationWorkflowModels.cs`
- `src/backend/SIGERSA.Domain/Repositories/IEvaluationWorkflowRepository.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/EvaluationWorkflowRepository.cs`

### Evidencias

- `src/frontend/src/features/evidences/EvidenceManagement.tsx`
- `src/backend/SIGERSA.Api/Controllers/EvidenceController.cs`
- `src/backend/SIGERSA.Application/Evidences/EvidenceService.cs`
- `src/backend/SIGERSA.Domain/Entities/EvidenceRecord.cs`
- `src/backend/SIGERSA.Domain/Repositories/IEvidenceRepository.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/EvidenceRepository.cs`
- `src/backend/SIGERSA.Tests/Application/EvidenceServiceTests.cs`

### Correcciones

- `src/frontend/src/features/corrections/CorrectionsManagement.tsx`
- `src/backend/SIGERSA.Api/Controllers/CorrectionsController.cs`
- `src/backend/SIGERSA.Application/Corrections/CorrectionContracts.cs`
- `src/backend/SIGERSA.Application/Corrections/CorrectionService.cs`
- `src/backend/SIGERSA.Domain/Entities/CorrectionModels.cs`
- `src/backend/SIGERSA.Domain/Repositories/ICorrectionRepository.cs`
- `src/backend/SIGERSA.Infrastructure/Persistence/CorrectionRepository.cs`
- `src/backend/SIGERSA.Tests/Application/CorrectionServiceTests.cs`

### Integración compartida y sincronización offline

- `src/frontend/src/lib/api.ts`
- `src/frontend/src/offline/database.ts`
- `src/frontend/src/offline/syncQueue.ts`
- `src/backend/SIGERSA.Application/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/backend/SIGERSA.Infrastructure/DependencyInjection.cs`
- `tools/DbSmoke/DbSmoke.csproj`
- `tools/DbSmoke/Program.cs`

### OTP, skeletons, máscaras, estados y contraseñas

- `src/frontend/src/components/forms/OtpInput.tsx`
- `src/frontend/src/components/forms/OtpInput.test.tsx`
- `src/frontend/src/components/feedback/Skeletons.tsx`
- `src/frontend/src/components/feedback/PasswordValidatorUI.tsx`
- `src/frontend/src/components/feedback/PasswordValidatorUI.test.tsx`
- `src/frontend/src/lib/formatters.ts`
- `src/frontend/src/lib/formatters.test.ts`
- `src/frontend/src/lib/passwordPolicy.ts`
- `src/frontend/src/features/auth/PasswordRecoveryPage.tsx`
- `src/frontend/src/features/auth/PasswordRecoveryPage.test.tsx`
- `src/frontend/src/features/admin/UserFormModal.tsx`
- `src/frontend/src/features/admin/UsersManagement.tsx`
- `src/frontend/src/features/admin/UsersManagement.test.tsx`
- `src/backend/SIGERSA.Application/Authentication/AuthContracts.cs`

### Contratos y documentación

- `.sdd/specs/api-contracts.md`
- `.sdd/specs/openapi-v1.json`
- `.sdd/specs/srs-functional-traceability.md`
- `.sdd/specs/implementation-report-ui-api.md`
