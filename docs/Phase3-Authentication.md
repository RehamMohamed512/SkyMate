# SkyMate Phase 3: Authentication and Authorization

## Scope and existing database

Phase 3 adds Identity configuration, JWT authentication, role seeding, and the authentication API. It reuses the existing `ApplicationUser`, `AppDbContext`, Identity EF Core model, and Phase 2 database migration. The migration already creates the seven standard Identity tables, so this phase adds **no migration** and does not recreate or drop any database objects.

The current project copy did not contain a separate API Design or ER Diagram document. This document records the authentication implementation and leaves the existing source and approved database design unchanged. The database design remains documented by the Phase 2 entity configurations and migration.

## Architecture

- `Application` defines `IJwtTokenService` and the token result contract.
- `Infrastructure` configures Identity, owns the JWT options and token implementation, and provides the idempotent Identity role seeder.
- `API` owns JWT Bearer validation, middleware, Swagger configuration, request/response DTOs, and `AuthController`.
- `UserManager<ApplicationUser>` handles registration, password hashing/verification, lockout counters, and role assignment. The generic repository and Unit of Work remain for application data and are not used for Identity operations.
- Authentication uses stateless access tokens. Refresh tokens are not implemented in this phase.

## Identity configuration

`ApplicationUser : IdentityUser<int>` remains in Infrastructure and keeps its existing fields and navigation properties. `AppDbContext` remains an `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>` and keeps its existing entity configurations.

Identity uses the existing EF Core SQL Server store and enforces:

- Unique email addresses; `UserName` is set to the trimmed email during registration.
- Email confirmation is not required.
- Password minimum length 8, uppercase required, lowercase required, digit required, special character optional.
- New accounts have lockout enabled; after 5 invalid password attempts, login is blocked for 15 minutes.
- Passwords are hashed by ASP.NET Core Identity. Passwords and password hashes are never returned by the API.

At startup, `IdentityRoleSeeder` uses `RoleManager<IdentityRole<int>>` to create `User` and `Admin` if missing. It is safe to run on every startup. Registration always assigns `User`; request DTOs have no role field, so public registration cannot select `Admin`.

## Registration workflow

`POST /api/v1/auth/register` validates first name, last name, email, password, and password confirmation. It checks for an existing email, creates the Identity account, and adds the default `User` role. If role assignment fails, the newly created account is deleted; a cleanup failure is surfaced as an internal error for operators to investigate.

Successful registration returns HTTP 201 with the user ID, email, name, and roles. It does not issue a token. A duplicate email returns HTTP 409. Invalid input returns `ValidationProblemDetails` with HTTP 400.

Example request:

```json
{
  "firstName": "Reham",
  "lastName": "Example",
  "email": "reham@example.com",
  "password": "SkyMatePass1",
  "confirmPassword": "SkyMatePass1"
}
```

Example response (201):

```json
{
  "id": 1,
  "email": "reham@example.com",
  "firstName": "Reham",
  "lastName": "Example",
  "roles": ["User"]
}
```

## Login workflow

`POST /api/v1/auth/login` looks up the user by email, checks lockout and verifies the password with `UserManager`. A wrong password increments the Identity access-failure counter; a successful login resets it. Unknown email, wrong password, and a locked account all receive the same generic HTTP 401 response so the API does not disclose which email addresses exist.

On success, the API returns a signed JWT and a safe user profile. The API does not return Identity internals.

Example request:

```json
{
  "email": "reham@example.com",
  "password": "SkyMatePass1"
}
```

Example response (200; token shortened):

```json
{
  "accessToken": "eyJ...",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-11-03T15:00:00+00:00",
  "expiresInSeconds": 3600,
  "user": {
    "id": 1,
    "email": "reham@example.com",
    "firstName": "Reham",
    "lastName": "Example",
    "roles": ["User"]
  }
}
```

## JWT generation and validation

`JwtTokenService` signs tokens using HS256. The token includes:

- `sub`: integer user ID serialized as a string.
- `email`: account email.
- `role`: one claim for each assigned role.
- `jti`: unique token ID.
- Issuer and audience from configuration.
- `nbf` and expiry timestamps; the default expiry is 60 minutes.

Bearer validation checks the signing key, issuer, audience, and lifetime, with 30 seconds of clock skew. Inbound claim mapping is disabled; `NameClaimType` is `sub` and `RoleClaimType` is `role`, so both `[Authorize]` and `[Authorize(Roles = "Admin")]` use the token claims directly. Missing or invalid JWT configuration stops startup with a clear configuration error.

## Roles and protected endpoint

The `User` and `Admin` roles are stored in the existing `AspNetRoles` table. This phase does not create an admin account or provide an endpoint that grants roles. Assigning `Admin` to an account remains an operator-controlled database/administrative action.

`GET /api/v1/auth/me` is protected by `[Authorize]`. It gets the user ID from the JWT `sub` claim and returns the current account's basic profile and current roles; it does not accept an ID in the request.

## API endpoints

| Method | Path | Access | Success | Other responses |
|---|---|---|---|---|
| POST | `/api/v1/auth/register` | Anonymous | 201 | 400 validation, 409 duplicate email |
| POST | `/api/v1/auth/login` | Anonymous | 200 | 400 validation, 401 generic invalid credentials or lockout |
| GET | `/api/v1/auth/me` | Bearer token | 200 | 401 missing/invalid token or unavailable user |

Protected endpoints return 401 without a valid token and 403 when the token lacks a required role.

## Database changes

No database objects or migration files changed in Phase 3. The existing schema already includes:

- `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetRoleClaims`, and `AspNetUserTokens`.
- The 9 travel/application tables created in Phase 2.

The role seeder inserts only missing rows into `AspNetRoles`; it does not alter schema or touch travel data. Do not run a destructive database reset for this phase.

## Configuration and User Secrets

The non-secret issuer, audience, and 60-minute expiry are in `SkyMate.API/appsettings.json`. `Jwt:SigningKey` is intentionally absent from source configuration. The `http` and `https` launch profiles set both environment variables to `Development`, ensuring User Secrets load even if the machine has a conflicting `DOTNET_ENVIRONMENT`. In local development, generate a random key and store it with User Secrets from the repository root:

```powershell
$jwtKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project .\SkyMate.API\SkyMate.API.csproj
Remove-Variable jwtKey
```

The API project already has a `UserSecretsId`; this command does not replace the existing SQL Server connection string secret. For deployment, provide the equivalent environment variables through the deployment secret store, such as `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__ExpiryMinutes`. Never commit the signing key.

Configure browser origins in `Cors:AllowedOrigins` in deployment configuration. The checked-in value is an empty array, which allows no cross-origin browser origins by default.

## Swagger

In Development, Swagger UI is at `/swagger` and the document is at `/swagger/v1/swagger.json`. The UI has an HTTP Bearer security scheme and marks protected operations with it; anonymous register and login operations are not marked as requiring a token. In Swagger's **Authorize** dialog, paste the access token; the UI supplies the `Bearer` prefix.

The API listens on the existing launch profile URLs: `https://localhost:7280` and `http://localhost:5210`. The local HTTPS Swagger URL is `https://localhost:7280/swagger`.

## Tests and local commands

Authentication integration tests use a separate SQLite in-memory database and a randomly generated test-only signing key. They cover successful registration, password hashing and User-role assignment, duplicate email, invalid data, successful JWT login and its claims/expiry, wrong and unknown credentials, five-attempt lockout, `/me`, unauthorized and role-protected access, idempotent role seeding, and Swagger Bearer metadata. The existing Phase 2 persistence tests remain in the same suite.

From `D:\Skymate v1`:

```powershell
dotnet restore SkyMate.slnx
dotnet build SkyMate.slnx
dotnet test SkyMate.slnx
dotnet run --project .\SkyMate.API\SkyMate.API.csproj
```

The API expects the existing `ConnectionStrings:DefaultConnection` secret and JWT signing-key secret at runtime. Phase 3 does not create/apply a migration; the initial schema migration remains `20261103100516_InitialIdentityAndTravelSchema`.
