# Autenticación y roles

## Flujo de login

1. El usuario abre `http://localhost:8088`.
2. El portal envía usuario y contraseña a `POST /api/identity/login`.
3. `AuthenticationService` valida credenciales y estado de la cuenta.
4. `TokenService` emite un JWT.
5. `RolePanelService` determina el panel autorizado.
6. El portal guarda la sesión y redirige a la ruta devuelta por backend.
7. Cada panel vuelve a validar la sesión mediante `/api/identity/me`.
8. Cada API aplica políticas de autorización independientemente del frontend.

## Roles

| Rol técnico | Perfil | Panel |
| --- | --- | --- |
| `admin` | Administrador | `/panel/administracion/` |
| `caregiver` | Cuidador | `/panel/cuidador/` |
| `resident` | Adulto mayor | `/panel/adulto-mayor/` |
| `family` | Familiar | `/panel/familiar/` |

## Claims principales del JWT

- `sub`: ID del usuario.
- `unique_name`: usuario.
- `name`: nombre visible.
- `role`: rol.
- `resident_id`: residente vinculado, cuando corresponde.

`resident_id` se emite para las cuentas del adulto mayor y familiar. El backend
lo utiliza para autorización por recurso.

## Políticas

Las políticas se encuentran en `src/BuildingBlocks/SeniorCare.Shared/Auth`.
Entre ellas:

- `AdminOnly`;
- `CareTeam`;
- `ResidentOnly`;
- `FamilyOnly`;
- `ResidentOrCareTeam`;
- `FamilyOrCareTeam`;
- `ResidentFamilyOrCareTeam`.

Ocultar un botón nunca se considera un control de seguridad. Las rutas de API
aplican la política correspondiente y, cuando existe un `residentId`, también
verifican la relación con ese residente.

## Sesión web

El helper compartido `assets/js/auth/session.js` centraliza token, usuario,
panel, validación y cierre de sesión. Los paneles no implementan logins
independientes.

## Regla para nuevas áreas

Al agregar un nuevo rol:

1. declararlo en `SeniorCareRoles`;
2. definir la política si necesita una nueva;
3. asignar su ruta en `RolePanelService`;
4. crear/proteger el panel;
5. proteger los endpoints del backend;
6. agregar pruebas positivas y negativas de autorización.
