# Verificación de Fase 3

## Controles automatizados

1. La solución completa compila con `net10.0`, analizadores activos y advertencias tratadas como errores.
2. Los validadores de FluentValidation rechazan rutas relativas de Supabase.
3. El validador de archivos acepta firmas válidas y rechaza inconsistencias MIME/firma.
4. La protección SQL acepta objetos `"SIGERSA"."TABLA"` y rechaza tablas sin esquema.
5. El documento OpenAPI v1 puede generarse con la herramienta local de Swashbuckle.

## Límites de esta fase

Las pruebas no abren conexiones, no crean la base `sigersa_db`, no ejecutan migraciones y no realizan llamadas reales a Supabase.
