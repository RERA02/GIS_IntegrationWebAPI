# ReraGIS.Api

ASP.NET Core (.NET 8 LTS) Web API that exposes the `Get_ProjectDetailsForGIS` SQL Server
stored procedure over a REST endpoint, using ADO.NET (`Microsoft.Data.SqlClient`) — **no Entity
Framework**.

> ⚠ **Current security posture:** by explicit request, `GET /api/ProjectDetailsForGIS/{projectId}/{currentLogInUserId}/{schemeId}`
> no longer requires a JWT. `projectId`, `currentLogInUserId`, and `schemeId` are all passed
> directly by the caller as route parameters, and the endpoint is `[AllowAnonymous]`. This is a deliberate change
> from the original design (where `currentLogInUserId` could only come from a signed, validated
> token) — see section 6 below for what that trades away and how to switch back. The JWT
> infrastructure (`POST /api/Auth/login`, `JwtTokenService`, `ClaimsPrincipalExtensions`) is still
> in the project, just unused by this endpoint for now.

## Project layout

```
ReraGIS.Api/
├── Controllers/
│   ├── AuthController.cs                     POST /api/Auth/login (no input; hardcoded demo user)
│   └── ProjectDetailsForGISController.cs     GET  /api/ProjectDetailsForGIS/{projectId}/{currentLogInUserId}/{schemeId}
├── Data/
│   └── SqlConnectionFactory.cs               Builds SqlConnection from the configured connection string
├── DTOs/
│   ├── ApiResponse.cs                        Standard { success, message, data } envelope
│   ├── LoginRequestDto.cs
│   ├── LoginResponseDto.cs
│   └── ProjectDetailsForGISDto.cs
├── Services/
│   ├── Interfaces/
│   │   └── IProjectDetailsService.cs         Also defines ProjectDetailsQueryResult/Status
│   ├── ProjectDetailsService.cs              ADO.NET stored-procedure execution + mapping
│   └── JwtTokenService.cs                    Issues JWTs
├── Helpers/
│   └── ClaimsPrincipalExtensions.cs          Reads the user ID from NameIdentifier/sub/userId/UserId
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
└── ReraGIS.Api.csproj
```

## 1. Required NuGet packages

Already declared in `ReraGIS.Api.csproj`:

| Package | Purpose |
|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer authentication handler |
| `System.IdentityModel.Tokens.Jwt` | `JwtSecurityToken` / `JwtSecurityTokenHandler` used to issue tokens |
| `Microsoft.Data.SqlClient` | ADO.NET SQL Server provider (explicitly **not** Entity Framework) |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI UI with a Bearer "Authorize" button |

Restore them with:

```bash
dotnet restore
```

> **Note on this build environment:** this project was authored and reviewed in a sandbox whose
> outbound network access to `api.nuget.org` is blocked by organizational policy, so `dotnet
> restore`/`dotnet build` could not be executed against the real packages here. Every file was
> instead compiled successfully against hand-written stand-ins that mirror the exact APIs used
> (ADO.NET types, `JwtBearerOptions`, `TokenValidationParameters`, `JwtSecurityToken`,
> `Microsoft.OpenApi.Models` types, Swashbuckle's `SwaggerGenOptions`), catching any syntax or
> logic errors. The project originally targeted `net10.0` and was retargeted to `net8.0` (LTS)
> for Visual Studio compatibility (see below) — no C# source changed, only the
> `TargetFramework`/package version numbers in the `.csproj`, and the ADO.NET/JWT/OpenAPI APIs
> used are unchanged across .NET 8/9/10. Run `dotnet restore && dotnet build` on your own machine
> (with normal internet access) as the final verification step.
>
> **Note on the target framework:** the project targets `net8.0` (the current LTS release)
> rather than `net10.0`/`net9.0` because Visual Studio versions below 17.16 cannot target
> .NET 10, and versions below ~17.12 cannot target .NET 9. `net8.0` is supported starting with
> VS 17.8, which covers the widest range of installed VS versions. If your Visual Studio is
> 17.16+ and you'd rather use .NET 10, change `<TargetFramework>net8.0</TargetFramework>` to
> `net10.0` in `ReraGIS.Api.csproj` and bump `Microsoft.AspNetCore.Authentication.JwtBearer` to
> a `10.0.x` version — nothing else in the code needs to change.

## 2. Configuration & migration instructions

1. Ensure SQL Server already has: the `Get_ProjectDetailsForGIS` procedure (from your `ALTER
   PROCEDURE` script), the `checkBDlog` table it logs into, and the `Project`, `UserProfile`,
   `[user]`, and `User_MemberRole_Mapping` tables it joins against. This API does not create or
   migrate schema (there is no EF Core involved), so schema/procedure deployment stays with your
   normal SQL deployment process (a `.sql` migration script, a DB project, SSDT, Flyway, etc.).
2. Set `ConnectionStrings:DefaultConnection` in `appsettings.json` (or, better, in a secret store —
   see below) to point at that database.
3. Set the `Jwt` section: `Key` (32+ random characters), `Issuer`, `Audience`, `ExpiryMinutes`.
4. Restore and run:
   ```bash
   dotnet restore
   dotnet run
   ```
   By default this launches on `https://localhost:7198` (see `Properties/launchSettings.json`).

### Secret management (do not commit real secrets)

`appsettings.json` in this deliverable contains **placeholder** values only. For real
environments, supply the connection string and `Jwt:Key` through one of:

- **Environment variables** — `ConnectionStrings__DefaultConnection`, `Jwt__Key`, etc. (double
  underscore is the .NET configuration binder's section separator).
- **.NET User Secrets** (local development only): `dotnet user-secrets set "Jwt:Key" "..."`.
- **Azure Key Vault** (or your cloud provider's equivalent) via
  `builder.Configuration.AddAzureKeyVault(...)` in `Program.cs`.
- Secure server/IIS configuration (see the IIS section below).

Never commit a real connection string, JWT key, or any other secret to source control.

## 3. Swagger testing instructions

1. `dotnet run` (Development environment enables Swagger by default in this project).
2. Open `https://localhost:7198/swagger`.
3. Expand `GET /api/ProjectDetailsForGIS/{projectId}/{currentLogInUserId}/{schemeId}`, click **Try it out**,
   fill in all three IDs (real values from your database), and **Execute**. No token/Authorize step
   is needed — the endpoint is anonymous.
4. The `POST /api/Auth/login` endpoint still works if you want a token for something else, but it's
   not required for the call above.

The collapsible **Schemas** section that Swagger UI normally renders at the very bottom of the
page (listing every model — `ApiResponse`, `ProjectDetailsForGISDto`, etc.) is turned off via
`options.DefaultModelsExpandDepth(-1);` in `Program.cs`'s `UseSwaggerUI(...)` call. If you ever
want it back, remove that line (or set it to `0`/`1`).

To also expose Swagger outside Development, move (or duplicate) the
`app.UseSwagger()/app.UseSwaggerUI()` block in `Program.cs` outside the
`if (app.Environment.IsDevelopment())` guard — and make sure you don't leave it reachable
anonymously on a public production deployment without additional protection.

## 4. Postman testing instructions

1. **GET** `https://localhost:7198/api/ProjectDetailsForGIS/10/1/2`
   (10 = `projectId`, 1 = `currentLogInUserId`, 2 = `schemeId`; substitute real values from your database.)
   No Authorization header needed.
2. Send. You should get `200 OK` with the project payload, `404` for an unknown project ID, and
   `400` for any of the three IDs being ≤ 0.

If your local dev certificate isn't trusted by Postman, either run
`dotnet dev-certs https --trust` first, or disable "SSL certificate verification" in Postman
settings for local testing only.

## 5. Example requests

```bash
curl -k https://localhost:7198/api/ProjectDetailsForGIS/10/1/2
```

```json
{
  "success": true,
  "message": "Project details retrieved successfully.",
  "data": {
    "id": 91453,
    "projectName": "test",
    "promoterName": "Aksy",
    "plotNo": "12121",
    "area": 15000,
    "phaseArea": 10000,
    "reraRegistrationNumber": "",
    "applicationStatus": "Edit",
    "returnURL": "https://rera.rajasthan.gov.in/Home"
  }
}
```

The demo login endpoint (unrelated to the call above, kept for future use) still works, but takes
no body at all now -- username/password are hardcoded server-side:

```bash
curl -k -X POST https://localhost:7198/api/Auth/login
```

## 6. `CurrentLogInUserID` — current behavior vs. the original design

**As it stands now:** `currentLogInUserId` is a plain route parameter, exactly like `projectId`
(`GET /api/ProjectDetailsForGIS/{projectId}/{currentLogInUserId}/{schemeId}`). The controller only checks
that it's a positive integer, then passes it straight through to
`IProjectDetailsService.GetProjectDetailsForGISAsync(...)` and on to the `@CurrentLogInUserID` SQL
parameter. The endpoint is `[AllowAnonymous]` — no token is checked at all.

**What that trades away:** the stored procedure uses `@CurrentLogInUserID` to decide whether the
caller is a promoter (via `User_MemberRole_Mapping`) and returns `"Edit"` vs `"View"` in
`Application Status` accordingly. With this change, any caller can pass any user ID and get that
user's permission level for any project — there's no verification that the caller actually is
that user. That's fine for local development/testing (which is why this change was made), but is
not safe to expose publicly as-is.

**To restore JWT-derived enforcement later:** the pieces are all still in the project —
1. Add back `[Authorize]` on `ProjectDetailsForGISController` (replacing `[AllowAnonymous]`).
2. Drop `currentLogInUserId` from the route (e.g. `[HttpGet("{projectId:int}/{schemeId:int}")]`,
   keeping `schemeId`) and instead call `User.TryGetUserId(out var currentLogInUserId)` (from
   `Helpers/ClaimsPrincipalExtensions.cs`), returning `401` if it's missing/invalid — this is
   exactly what the controller looked like before `currentLogInUserId` became a route parameter.
3. Everything else (`JwtTokenService`, `AuthController`, the JWT bearer wiring in `Program.cs`)
   is untouched and ready to go.

## 6b. Adding `@SchemeID` to the stored procedure

The API code now always sends a third parameter, `@SchemeID` (as `SqlDbType.Int`), to
`Get_ProjectDetailsForGIS`. **The original `ALTER PROCEDURE` script you provided does not declare
this parameter**, so calling the endpoint against an unmodified copy of the procedure will fail
with a SQL error (surfaced to the client as a generic `500` — see section 10) until the procedure
is updated to accept it.

At minimum, the procedure's signature needs:

```sql
ALTER PROCEDURE Get_ProjectDetailsForGIS
    @ProjectID INT = 0,
    @CurrentLogInUserID INT = 0,
    @SchemeID INT = 0
AS
BEGIN
    ...
```

What the procedure *does* with `@SchemeID` is up to your schema and business rules — this project
doesn't know whether `Project` (or a related table) has a `SchemeID` column, so no filtering logic
was assumed or added on your behalf. Common options once the parameter exists:
- Filter the result: add `AND P.SchemeID = @SchemeID` (or the equivalent join condition) to the
  existing `WHERE P.ID = @ProjectID` clause, if `Project` has a `SchemeID` column.
- Log it: include it in the existing `checkBDlog` insert alongside `@ProjectID` and
  `@CurrentLogInUserID`, if it's informational only for now.
- Leave it accepted-but-unused temporarily while you decide, so the parameter count matches and
  the API doesn't error, then wire up real filtering later.

Until the procedure is updated, `ProjectDetailsService.cs` will send `@SchemeID` on every call
regardless — there's no code path that omits it.

## 6c. `returnURL` in the response

Every successful (single-row) response now includes a `returnURL` field, currently a fixed value
(`https://rera.rajasthan.gov.in/Home`), not part of the stored procedure's result set:

```json
"returnURL": "https://rera.rajasthan.gov.in/Home"
```

- Configured in `appsettings.json` / `appsettings.Development.json` under `AppUrls:ReturnUrl`, not
  hardcoded in `ProjectDetailsForGISController.cs` — change the value there (or override it via the
  `AppUrls__ReturnUrl` environment variable / User Secrets) rather than editing the C# source.
- The DTO property is named `ReturnURL` (not `ReturnUrl`) specifically so the default camelCase
  JSON serializer produces the key `returnURL` — .NET's camelCase policy only lowercases a
  *leading* run of capitals, so `ReturnURL` → `returnURL` while `ReturnUrl` would have serialized as
  `returnUrl`.
- It's currently a static value (not built from `projectId`/`schemeId`/etc.). If you later want it
  to vary per request (e.g. include the project ID as a query string), that logic belongs in
  `ProjectDetailsForGISController.WithReturnUrl(...)`.
- It's only set on the single-row success path; the "multiple rows" empty-array response
  (section 11) has no items to stamp it onto.

## 7. Replacing the demo login with real `[user]` table authentication

`AuthController.Login` currently takes **no input at all** and always issues a token for a single
hardcoded user (`admin`, user ID 1) — by explicit request, there's no username/password field for
a caller to fill in on Swagger/Postman. `LoginRequestDto` (`DTOs/LoginRequestDto.cs`) still exists
in the project, unreferenced, ready for when real login is wired up. To use the real `[user]`
table:

1. **Never store or compare plaintext passwords.** If the existing `[user]` table stores plaintext
   or reversibly-encrypted passwords, plan a migration to a proper hash
   (`Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` or BCrypt/Argon2) before going live.
2. Add a small ADO.NET lookup (same pattern as `ProjectDetailsService`) — e.g. an
   `IUserAuthenticationService` with:
   ```csharp
   public interface IUserAuthenticationService
   {
       Task<AuthenticatedUser?> ValidateCredentialsAsync(
           string userName, string password, CancellationToken cancellationToken);
   }

   public sealed record AuthenticatedUser(int UserId, string UserName);
   ```
3. Implement it with ADO.NET: select the user's ID, username, and password hash by username,
   verify the password against the stored hash (`PasswordHasher<object>().VerifyHashedPassword(...)`
   or your library's equivalent), and return `null` on any mismatch (username not found or bad
   password) — never distinguish the two in the response, to avoid username enumeration.
4. In `AuthController`, add back a `[FromBody] LoginRequestDto request` parameter to `Login(...)`,
   restore the `400` (missing username/password) and `401` (bad credentials) checks that were
   removed when the endpoint became hardcoded/input-less, and replace the `DemoUserName`/
   `DemoUserId` block with a call to `IUserAuthenticationService.ValidateCredentialsAsync(...)`; on
   `null`, return `401 Unauthorized`; on success, call
   `_jwtTokenService.GenerateToken(user.UserId, user.UserName)` exactly as today.
5. Register the new service in `Program.cs`:
   `builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();`

No other file needs to change — `JwtTokenService`, the claims helper, and the secured controller
already work purely off the resulting `userId`/`sub`/`NameIdentifier` claims.

## 8. Deploying to IIS

1. Install the **.NET 8 Hosting Bundle** on the IIS server (includes the ASP.NET Core Module,
   `ANCM`). Restart IIS afterwards (`net stop was /y && net start w3svc`) so it picks up the module.
2. Publish a self-contained-or-framework-dependent deployment:
   ```bash
   dotnet publish -c Release -o ./publish
   ```
3. Copy the contents of `./publish` to a folder on the IIS server (e.g.
   `C:\inetpub\ReraGIS.Api`).
4. In IIS Manager: create an **Application Pool** with **.NET CLR version: No Managed Code**
   (ASP.NET Core manages its own runtime), **Start Mode: AlwaysRunning**.
5. Create a **Site** (or **Application** under an existing site) pointing its physical path at the
   publish folder, bound to the desired host name/port, using that application pool.
6. Configure HTTPS: bind a certificate (from your CA, or IIS-managed) to an `https` binding on the
   site — do not rely on `dotnet dev-certs` in production.
7. Set real configuration values (connection string, `Jwt:Key`, etc.) via **IIS → Configuration
   Editor**, `web.config` `<environmentVariables>` inside `<aspNetCore>`, or machine-level
   environment variables — not by editing `appsettings.json` in place with production secrets
   checked into source control.
8. Grant the application pool identity read access to the publish folder, and (if using Windows
   Auth to SQL Server) appropriate SQL permissions; otherwise use a SQL login via the connection
   string.
9. Browse to the site; confirm `/swagger` (if enabled) and `/api/Auth/login` respond.

## 9. CORS (for an Angular / MVC front-end consuming this API)

If a browser-based Angular SPA or a server-rendered MVC app running on a different origin will
call this API directly from client-side JavaScript, add CORS in `Program.cs`:

```csharp
const string AngularClientPolicy = "AngularClient";

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientPolicy, policy =>
    {
        policy.WithOrigins("https://localhost:4200", "https://your-angular-app.example.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
        // Add .AllowCredentials() only if you actually use cookies/credentialed requests;
        // it cannot be combined with a wildcard origin.
    });
});
```

and, in the middleware pipeline, **before** `app.UseAuthentication()`:

```csharp
app.UseCors(AngularClientPolicy);
```

Notes:
- List explicit origins; avoid `AllowAnyOrigin()` for an authenticated API.
- A server-side MVC app calling this API from its own backend code (not from the browser) does
  **not** need CORS at all — CORS only governs browser-enforced cross-origin JavaScript calls.
- Swagger UI itself is served from the same origin as the API, so it needs no CORS entry.

## 10. Error handling summary

- **400** — invalid `projectId`, `currentLogInUserId`, or `schemeId` (any ≤ 0) on the project details
  endpoint; uses the `ApiResponse<object>` envelope (automatic model-validation failures elsewhere
  are also routed through that same envelope via `ApiBehaviorOptions.InvalidModelStateResponseFactory`
  in `Program.cs`).
- **401** — not currently reachable anywhere: the project details endpoint is anonymous (section 6),
  and `/api/Auth/login` always succeeds since it takes no input to get wrong (section 7). Both
  return again once JWT-derived enforcement and/or real credential checking are restored.
- **404** — the stored procedure returned no row for the given `projectId`.
- **500** — any unhandled exception (e.g. a SQL failure). The global handler in `Program.cs` logs
  the full exception server-side and returns a generic `ApiResponse<object>` message — no
  connection strings, stack traces, or secrets are ever included in the HTTP response.

## 11. The "multiple rows" edge case

The stored procedure filters on the primary key (`WHERE P.ID = @ProjectID`), so it should always
return at most one row. `ProjectDetailsService` still defensively checks for a second row after
mapping the first; if one is found (a data-integrity anomaly), the controller returns
`success: true` with `data: []` rather than an ambiguous single object, per the specification.
