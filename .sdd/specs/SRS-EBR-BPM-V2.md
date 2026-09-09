# SRS V2 — SIGERSA (Evaluación Basada en Riesgo EBR/BPM)

| Campo | Valor |
| --- | --- |
| Documento | Especificación de Requisitos de Software |
| Versión | 2.0 |
| Estado | Línea base para desarrollo |
| Metodología | Spec-Driven Development (SDD) |
| Base de datos | PostgreSQL, esquema exclusivo `SIGERSA` |

## 1. Propósito

Este documento es la especificación maestra del backend de SIGERSA. Define el comportamiento verificable para administrar empresas y establecimientos, planificar y ejecutar inspecciones EBR/BPM, mantener fichas configurables, calcular riesgo, gestionar evidencias, revisar resultados y conservar trazabilidad.

Cuando un documento anterior contradiga esta versión, prevalece SRS V2. Los contratos API, pruebas automatizadas, migraciones y decisiones de arquitectura deberán ser trazables a los requisitos identificados aquí.

## 2. Alcance y contexto

SIGERSA será una PWA para escritorio y dispositivos móviles, preparada para conectividad intermitente. El backend expondrá una API REST, PostgreSQL será la fuente transaccional de verdad y Supabase Storage será el único repositorio de archivos binarios.

El alcance incluye:

- Empresas, establecimientos, usuarios, roles y permisos por ámbito.
- Solicitudes, alertas, denuncias, casos, programación y asignación de inspecciones.
- Ejecución de fichas BPM, respuestas, hallazgos, correcciones e informes.
- Cálculo reproducible de puntuación y nivel de riesgo.
- Operación offline con sincronización idempotente y resolución controlada de conflictos.
- Auditoría de operaciones sensibles y conservación del historial.

Quedan fuera de la primera entrega la facturación, el almacenamiento de binarios en el servidor de la API y la edición directa de datos productivos por fuera de los casos de uso autorizados.

## 3. Principios obligatorios

1. Todos los objetos de negocio residirán en el esquema PostgreSQL `SIGERSA`; no se crearán tablas de negocio en `public`.
2. No se crearán tipos `ENUM`. Los catálogos administrables se almacenarán en `SIGERSA.ParametersControl`.
3. Las eliminaciones de parámetros serán lógicas: `Status = false`; un registro activo tendrá `Status = true`.
4. La aplicación podrá modificar la plantilla operativa de la ficha, pero una evaluación iniciada conservará una instantánea inmutable de la versión utilizada.
5. Los archivos de evidencia se almacenarán exclusivamente en buckets privados de Supabase Storage. PostgreSQL guardará solo metadatos, integridad y referencia lógica.
6. Toda autorización será denegada por defecto y se evaluará por rol, permiso, ámbito y propiedad del recurso.

## 4. Actores

| Actor | Responsabilidad principal |
| --- | --- |
| Administrador | Gobierno global de usuarios, seguridad, catálogos, fichas y configuración. |
| Coordinador | Gestión operativa de casos, programación, asignación, revisión y seguimiento. |
| Técnico | Ejecución de inspecciones asignadas, captura de respuestas, hallazgos y evidencias. |
| Empresa | Gestión de su perfil y establecimientos, solicitudes y respuesta a correcciones dentro de su ámbito. |
| Servicio de correo | Entrega de OTP y notificaciones transaccionales. |
| Supabase Storage | Custodia privada de archivos binarios y entrega mediante acceso temporal autorizado. |

## 5. Autenticación y seguridad

### 5.1 Inicio de sesión

- **RF-AUT-001:** La API autenticará por correo normalizado y contraseña, sin revelar si una cuenta existe en respuestas de error.
- **RF-AUT-002:** Las contraseñas se almacenarán únicamente como hash resistente a fuerza bruta, con sal individual; nunca se registrarán en logs, auditoría o eventos.
- **RF-AUT-003:** Solo usuarios activos y no bloqueados podrán iniciar sesión. Los intentos fallidos incrementarán un contador y aplicarán bloqueo temporal configurable.
- **RF-AUT-004:** Una autenticación válida emitirá un token de acceso de vida corta y un refresh token rotativo. En cada rotación se invalidará el token anterior; la detección de reutilización revocará la familia completa.
- **RF-AUT-005:** Cerrar sesión revocará el refresh token o la familia de sesión correspondiente.

### 5.2 Recuperación con OTP por correo

1. El usuario solicita recuperación indicando su correo.
2. La API siempre responde de forma neutral para impedir enumeración de cuentas.
3. Si la cuenta es elegible, la API genera un OTP criptográficamente aleatorio, almacena únicamente su hash y lo envía al correo registrado.
4. El OTP tendrá expiración, máximo de intentos y límite de reenvíos configurables mediante `ParametersControl`.
5. Una nueva emisión invalidará los OTP anteriores aún activos para el mismo usuario y propósito.
6. La validación correcta marcará el OTP como usado y emitirá un comprobante de recuperación de un solo uso y duración corta.
7. El cambio de contraseña exigirá ese comprobante, revocará todas las sesiones vigentes y notificará al usuario por correo.

- **RF-AUT-006:** El OTP no se almacenará ni registrará en texto claro.
- **RF-AUT-007:** Un OTP expirado, usado, invalidado o bloqueado no podrá reutilizarse.
- **RF-AUT-008:** Solicitud, emisión, validación, bloqueo y cambio de contraseña producirán eventos de auditoría sin secretos.
- **RF-AUT-009:** Los endpoints de acceso y recuperación aplicarán limitación de frecuencia por cuenta y origen.

### 5.3 Controles transversales

- TLS será obligatorio en tránsito y los secretos residirán fuera del repositorio.
- La API validará entrada, tipo, tamaño y autorización antes de ejecutar un caso de uso.
- Los errores externos no expondrán consultas, trazas, credenciales ni rutas internas.
- Las operaciones de escritura incluirán actor, fecha, identificador de correlación y control de concurrencia cuando corresponda.
- La auditoría será inmutable para usuarios de aplicación y registrará accesos o cambios sensibles.

## 6. Arquitectura de parámetros

### 6.1 Modelo `ParametersControl`

`SIGERSA.ParametersControl` sustituye los ENUM y centraliza catálogos y valores configurables. `KeyWord` identifica el conjunto semántico; `CompanyCode`, `OCode` y `CCode` permiten ámbito y códigos heredados; las columnas tipadas (`NumericData`, `DoubleData`, `StringData`, `BooleanData`, `DateData`) contienen el valor aplicable.

Los antiguos catálogos `ESTADO_CASO`, `ESTADO_FICHA`, `ESTADO_EVALUACION`, `TIPO_ITEM_FICHA`, `TIPO_RESPUESTA`, `NIVEL_RIESGO` y `ESTADO_SINCRONIZACION` se precargan con `KeyWord` igual al nombre del catálogo, `StringData` igual al código y `NumericData` como orden de presentación.

### 6.2 Reglas

- **RF-PAR-001:** No se crearán tipos PostgreSQL `ENUM`; las columnas consumidoras serán `VARCHAR` y la capa de aplicación validará el código contra un parámetro activo.
- **RF-PAR-002:** Las consultas ordinarias devolverán solo registros con `Status = true`.
- **RF-PAR-003:** Eliminar un parámetro cambiará `Status` a `false` y completará `DUser` y `DDate`; no realizará borrado físico.
- **RF-PAR-004:** Crear y modificar parámetros completará los campos de auditoría `CUser/CDate` y `MUser/MDate`, respectivamente.
- **RF-PAR-005:** Los valores inactivos permanecerán disponibles para interpretar registros históricos, pero no podrán seleccionarse en nuevas operaciones.
- **RF-PAR-006:** Los catálogos podrán tener valores globales y, cuando aplique, valores específicos de empresa. Un valor específico prevalecerá sobre su equivalente global.
- **RF-PAR-007:** Solo un Administrador podrá crear, modificar o desactivar parámetros; los demás roles tendrán lectura según necesidad funcional.

La migración ofrece funciones para alta, actualización, eliminación lógica y consulta activa. El backend las invocará dentro de transacciones y tratará un resultado `false` como registro inexistente o ya inactivo.

## 7. Fichas mutables

### 7.1 Plantilla inicial `AllItems`

`SIGERSA.AllItems` precarga la estructura BPM entregada. `Items` es la identidad técnica y el orden base; `ItemsId` es el identificador visible heredado; `Description` contiene el texto; `SectionType` clasifica categoría, sección, subsección, agrupación o ítem; `Parents` expresa la relación jerárquica heredada.

`ItemsId` no será único porque la fuente contiene identificadores repetidos válidos. La relación jerárquica se resolverá por `Parents` y el orden de `Items`, con validación de ciclos y referencias desde la aplicación.

### 7.2 Edición y publicación

- **RF-FIC-001:** La aplicación cargará `AllItems` como estructura inicial de una ficha nueva.
- **RF-FIC-002:** Administradores podrán agregar, editar, reordenar y remover nodos, incluidos texto, tipo y jerarquía.
- **RF-FIC-003:** El sistema impedirá ciclos, nodos huérfanos involuntarios y tipos de sección incompatibles con su posición.
- **RF-FIC-004:** Antes de remover un nodo padre, el usuario deberá elegir entre remover su subárbol o reubicar sus hijos.
- **RF-FIC-005:** Cada publicación creará una versión inmutable con instantánea y hash de definición; no editará una versión ya publicada.
- **RF-FIC-006:** Al iniciar una evaluación, se fijará la versión de ficha. Cambios posteriores no alterarán evaluaciones en curso ni históricas.
- **RF-FIC-007:** Coordinadores podrán consultar y proponer ajustes, pero la publicación o retiro corresponderá al Administrador.
- **RF-FIC-008:** Toda mutación y publicación registrará actor, fecha, motivo y versión afectada.

## 8. Flujo operativo EBR/BPM

1. La Empresa registra o actualiza sus datos y presenta una solicitud cuando corresponda.
2. El Coordinador analiza el origen del caso, prioriza, programa y asigna uno o más Técnicos.
3. El Técnico descarga la evaluación y su ficha versionada, trabaja en línea u offline y registra respuestas, ubicación, hallazgos y evidencias.
4. La sincronización envía operaciones idempotentes; la API valida versión, propiedad y estado antes de aceptar cada cambio.
5. El motor calcula puntuación BPM y riesgo a partir de reglas versionadas y conserva entradas, resultado, versión y hash.
6. El Coordinador revisa, solicita correcciones o aprueba. La Empresa atiende únicamente las correcciones que le sean notificadas y habilitadas.
7. El cierre conserva ficha, respuestas, evidencias, cálculos, decisiones y auditoría como historial consultable.

## 9. Infraestructura documental y evidencias

- **RF-EVI-001:** Supabase Storage será el único almacenamiento persistente de fotografías, documentos, videos, audios e informes binarios.
- **RF-EVI-002:** Los buckets serán privados. Ningún cliente recibirá credenciales de servicio ni acceso público permanente.
- **RF-EVI-003:** La API emitirá autorizaciones o URLs firmadas de duración corta después de validar rol, ámbito y relación con la evaluación.
- **RF-EVI-004:** PostgreSQL almacenará bucket, ruta lógica, nombre original y seguro, tamaño, MIME, hash, autor, marcas de tiempo, geolocalización opcional y estado de sincronización; no almacenará el binario.
- **RF-EVI-005:** Antes de confirmar una evidencia se validarán límite de tamaño, extensión, MIME real, firma del archivo, hash e integridad de la carga.
- **RF-EVI-006:** Las cargas usarán rutas no predecibles, sin datos personales, separadas por ambiente y evaluación.
- **RF-EVI-007:** Una eliminación funcional será lógica y auditable. La purga física se realizará solo por una política de retención aprobada y un proceso de servicio autorizado.
- **RF-EVI-008:** La cola offline podrá conservar temporalmente archivos en el dispositivo hasta sincronizarlos; después de confirmación e integridad, deberá eliminarlos conforme a la política local.
- **RF-EVI-009:** Si el archivo llega a Storage pero falla la transacción de metadatos, un proceso de reconciliación detectará y resolverá el huérfano.

Se prohíben carpetas locales del servidor, volúmenes persistentes de la API, blobs en PostgreSQL y proveedores documentales alternativos para evidencias.

## 10. Matriz de roles y permisos

Leyenda: **T** = total en el ámbito global; **A** = administrar/decidir; **O** = operar recursos asignados o propios; **L** = lectura; **—** = denegado.

| Recurso o acción | Administrador | Coordinador | Técnico | Empresa |
| --- | :---: | :---: | :---: | :---: |
| Usuarios internos, roles y permisos | T | L | — | — |
| Usuarios de la propia empresa | T | L | — | O |
| Parámetros y catálogos | T | L | L | L |
| Plantillas de ficha: crear/editar | T | L/proponer | — | — |
| Plantillas de ficha: publicar/retirar | T | — | — | — |
| Empresas y establecimientos | T | A | L asignados | O propios |
| Solicitudes | T | A | L asignadas | O propias |
| Alertas y denuncias | T | A | L asignadas | L relacionadas, si se habilita |
| Casos: crear, analizar y priorizar | T | A | L asignados | L propios |
| Programar y asignar inspecciones | T | A | L propias | L propias |
| Ejecutar evaluación y respuestas | T | L | O asignadas | — |
| Crear hallazgos/no conformidades | T | L | O asignadas | L propias |
| Cargar evidencias de inspección | T | L | O asignadas | — |
| Ver evidencias | T | A según ámbito | L asignadas | L propias autorizadas |
| Solicitar correcciones | T | A | L asignadas | L propias |
| Responder correcciones | T | L | O asignadas | O propias habilitadas |
| Revisar/aprobar/rechazar evaluación | T | A | — | — |
| Cerrar/reabrir caso | T | A | — | — |
| Informes e indicadores | T | A según ámbito | L propios | L propios |
| Auditoría | T | L según ámbito | — | — |

Reglas complementarias:

- **RF-AUTZ-001:** El rol nunca bastará por sí solo; se comprobará el ámbito global, territorial, empresarial, de establecimiento o de asignación.
- **RF-AUTZ-002:** Una Empresa no podrá consultar información de otra empresa ni datos internos de análisis no publicados.
- **RF-AUTZ-003:** Un Técnico solo modificará evaluaciones que tenga asignadas y estén en un estado operable.
- **RF-AUTZ-004:** Quien ejecuta una evaluación no podrá aprobarla; la separación de funciones se aplicará al Coordinador revisor.
- **RF-AUTZ-005:** Reabrir un caso cerrado será excepcional, requerirá permiso explícito, justificación y auditoría.

## 11. Datos, auditoría y concurrencia

- **RF-DAT-001:** Las entidades transaccionales usarán claves técnicas, marcas UTC, actor de creación/modificación y versión de fila.
- **RF-DAT-002:** Las escrituras verificarán la versión esperada; un conflicto devolverá HTTP 409 sin sobrescribir silenciosamente.
- **RF-DAT-003:** Fechas y horas se persistirán como `TIMESTAMPTZ`; la interfaz las presentará en la zona del usuario.
- **RF-DAT-004:** Los cálculos y documentos conservarán la versión de reglas y parámetros efectiva para su reproducción histórica.
- **RF-DAT-005:** Identificadores de operación offline serán únicos y la repetición de una solicitud confirmada devolverá el mismo resultado lógico.

## 12. Requisitos no funcionales

| ID | Requisito verificable |
| --- | --- |
| RNF-001 | La API deberá responder el percentil 95 de lecturas ordinarias en menos de 2 s y escrituras ordinarias en menos de 3 s, excluyendo transferencia de archivos y dependencias externas. |
| RNF-002 | La disponibilidad mensual objetivo será 99.5 %, excluyendo mantenimiento anunciado. |
| RNF-003 | La interfaz cumplirá WCAG 2.2 nivel AA en los flujos críticos. |
| RNF-004 | Logs estructurados incluirán correlación y resultado, sin contraseñas, OTP, tokens ni contenido sensible de evidencias. |
| RNF-005 | Backups y restauración de PostgreSQL y políticas de Storage se probarán periódicamente; RPO y RTO serán configurables por ambiente. |
| RNF-006 | Migraciones y despliegues serán repetibles, revisados y ejecutados con privilegio mínimo. |
| RNF-007 | Las pruebas automatizadas cubrirán autorización por ámbito, OTP, parámetros activos, versionado de fichas, integridad de evidencias, idempotencia y concurrencia. |

## 13. Criterios de aceptación de Fase 1

- **CA-01:** La migración crea todos los objetos de negocio en `SIGERSA` y no contiene ningún `CREATE TYPE ... ENUM`.
- **CA-02:** `ParametersControl` contiene todos los campos requeridos, auditoría y funciones operativas de alta, modificación, baja lógica y consulta activa.
- **CA-03:** Los siete catálogos que antes eran ENUM se encuentran precargados como parámetros activos.
- **CA-04:** `AllItems` contiene los 90 registros base suministrados, conserva su orden y admite nuevas altas mediante su secuencia.
- **CA-05:** Una baja de parámetro cambia `Status` de `true` a `false`, conserva el registro y completa auditoría de eliminación.
- **CA-06:** La documentación define el login, la recuperación OTP por correo, los parámetros sin ENUM, la ficha mutable, Supabase Storage exclusivo y los permisos de los cuatro roles.

## 14. Trazabilidad mínima para desarrollo

Cada historia o cambio posterior deberá citar al menos un requisito `RF-*` o `RNF-*`. Cada endpoint deberá figurar en el contrato OpenAPI; cada tabla o función deberá figurar en la especificación de base de datos; y cada criterio de aceptación deberá tener evidencia automatizada o un procedimiento de verificación reproducible.
