# Flujo de Git durante el semestre

La guía solicita historial de commits. El historial debe reflejar trabajo real,
por lo que no se deben crear commits retroactivos o con fechas inventadas.

## Rutina recomendada

Al terminar una unidad pequeña:

```powershell
git status
git add .
git commit -m "feat: agregar descripción breve del cambio"
```

Ejemplos:

```text
feat: agregar registro de medicamentos
feat: conectar alertas SOS con el panel
test: validar flujo de confirmación de tomas
docs: agregar diagrama de secuencia de emergencia
fix: conservar alertas durante pérdida de conexión
```

## Ramas

- `main`: versión estable para demostración.
- `develop`: integración del sprint.
- `feature/nombre`: desarrollo de una historia de usuario.

No subir `.env`, contraseñas reales, llaves ni archivos de producción.

