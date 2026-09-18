# Guía para comprobar el flujo de reinspección

Esta guía permite verificar que SIGERSA crea una reinspección para corregir y volver a evaluar las no conformidades de una inspección anterior.

## Requisitos previos

- Use un usuario con rol **Técnico evaluador** para ejecutar la ficha.
- Use un usuario con rol **Coordinador** para revisar, no aprobar y cerrar la evaluación.
- Debe existir una evaluación en estado **En ejecución**.
- La evaluación debe contener al menos una respuesta **IT · Incumplimiento total**, con criticidad C, M o Me.

## Paso a paso

1. Inicie sesión como **Técnico evaluador**.
2. Abra **Evaluaciones e inspecciones**.
3. Localice una evaluación en estado **En ejecución** y pulse **Realizar inspección**.
4. Responda la ficha. Para provocar el flujo de reinspección, marque al menos un punto como **IT · Incumplimiento total** y seleccione su nivel de criticidad.
5. Registre una observación que permita reconocer la no conformidad y, si lo desea, adjunte evidencia.
6. Pulse **Calcular evaluación**. Confirme que se muestra el porcentaje, el riesgo y la frecuencia.
7. Si el porcentaje es menor de 81 %, registre las medidas correctivas y sus fechas de cumplimiento.
8. Pulse **Finalizar evaluación**.
9. Desde la lista de evaluaciones, pulse **Enviar a revisión**.
10. Cierre sesión e ingrese como **Coordinador**.
11. Abra **Evaluaciones e inspecciones**, localice la evaluación enviada y pulse **Comenzar revisión** si aparece esa acción.
12. Pulse **No aprobar**.
13. En la evaluación con estado **No aprobada**, pulse **Cerrar y programar reinspección**.
14. Confirme la acción. SIGERSA debe cerrar el expediente anterior y crear automáticamente:
    - un caso de seguimiento;
    - una programación para 30 días después;
    - una evaluación nueva en estado **Asignada**;
    - un alcance de **Seguimiento** con motivo **Inspección de control**.
15. Abra la nueva evaluación y pulse **Iniciar** con el técnico asignado.
16. Abra la ficha de la reinspección. Deben mostrarse únicamente los puntos que fueron no conformes en la inspección anterior.
17. Corrija las respuestas según lo comprobado en campo, adjunte nuevas evidencias si corresponde, calcule y finalice la reinspección.

## Resultado esperado

- La inspección original queda cerrada y conserva sus respuestas, hallazgos y evidencias.
- La reinspección queda vinculada a la evaluación anterior.
- La ficha de seguimiento exige y califica solo las no conformidades anteriores.
- Si todos los puntos corregidos cumplen, la reinspección puede enviarse a revisión y aprobarse mediante el flujo normal.

## Prueba sin conexión

1. Entre al sistema con conexión y abra **Evaluaciones e inspecciones**. Las evaluaciones en ejecución se sincronizan automáticamente con el dispositivo; no existe un paso manual de “Preparar offline”.
2. Desactive la conexión de red.
3. Abra una evaluación que ya haya sido sincronizada automáticamente.
4. Registre respuestas, observaciones y evidencias.
5. Guarde los datos complementarios y pulse **Calcular evaluación**. El sistema debe mostrar el resultado local y marcarlo para sincronización.
6. Puede pulsar **Finalizar evaluación**; la finalización quedará en cola.
7. Reactive la conexión. Compruebe que el contador de sincronización vuelve a cero y que las respuestas, evidencias, cálculo, frecuencia y finalización aparecen en la base de datos.

> Un dispositivo que nunca recibió los datos de una evaluación no puede inventar esa ficha sin red. La descarga se realiza de forma automática cuando hay conexión, sin una acción previa del usuario.

## Criterios funcionales que debe observar

| Escenario | Comportamiento esperado |
| --- | --- |
| Solicitud/renovación de Permiso Sanitario o certificación BPM | Ficha completa; aprobación con 81 % o más, sin NC crítica y con menos de tres NC mayores. |
| Inspección programada con Permiso Sanitario válido | Ficha calificable desde 1.1.3. |
| Seguimiento o control | Solo las NC de la inspección anterior. |
| Denuncia | Alcance discrecional del inspector; se califican los puntos respondidos. |
| Resultado menor de 81 % | Notificar NC y establecer fechas de corrección. |
| Resultado igual o menor de 60 % | Mostrar recomendación de considerar el cierre. |
| Riesgo total | Bajo: 1.0–3.6; medio: >3.6–6.3; alto: >6.3. |
