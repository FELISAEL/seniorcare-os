# Matriz de cumplimiento

| Requisito de la guía | Implementación |
| --- | --- |
| Aplicación web | Portal público único y paneles por rol (adulto mayor, familiar, cuidador y administrador) |
| Microservicios | Identity, Care, Emergency, Communication y Analytics |
| ETL | Worker que extrae cuidados y emergencias y carga `analytics_db` |
| Aplicación Android e iOS | Proyecto compartido .NET MAUI |
| Docker | Imágenes por servicio y `docker-compose.yml` |
| Kubernetes | Manifiestos en `deploy/k8s` |
| Servidor de monitoreo | Aspire Dashboard y OpenTelemetry |
| Alojamiento en la nube | Manifiestos preparados; requiere proveedor y credenciales |
| Git | Repositorio con código modular y `.gitignore` |
| Documentación técnica | `docs/manual-tecnico.md` |
| Manual de usuario | `docs/manual-usuario.md` |
| Metodología ágil | Backlog y sprints en `docs/scrum.md` |
| UML | Diagramas en `docs/uml` |
| Íconos grandes | Tarjetas principales de la PWA |
| Interfaz simplificada | Cuatro acciones principales y lenguaje claro |
| Recordatorios de medicamentos | Horarios diarios y confirmación de toma |
| Botón de emergencia | Flujo SOS con confirmación y seguimiento |
| Videollamadas simplificadas | Creación de sala con un solo botón |

## Pendiente de una decisión externa

La publicación en una nube real requiere seleccionar proveedor, dominio,
presupuesto y credenciales. El proyecto ya contiene contenedores y manifiestos
para efectuar el despliegue cuando esa decisión sea autorizada.

