# VeriClaim Sessions API

Moves legacy ASP.NET `Session["..."]` storage out of IIS memory into MongoDB, **with one session per browser tab**,
without changing any V2 page code.

```
 V2 website (.NET 4.6.2)                         Sessions API (.NET 10)                MongoDB
 Session["PracID"] = ...  ──► SessionServiceStateStore ──HTTP + X-Api-Key──► SessionsController ──► Sessions collection
                              (VeriClaim.SessionState.Client.dll)                                  TTL index on expires
```

## How the requirements are met

| Requirement | How |
|---|---|
| Old pages keep using `Session["x"]` | Custom ASP.NET session-state provider, switched on in `web.config` only. |
| Each browser tab is its own session | `cookieless="UseUri"`: the id lives in the URL (`/(S(id))/Page.aspx`), so every tab carries its own. |
| A new login never sees old values | A new tab has no id, so it gets a brand-new empty session. Logout (`Session.Abandon()`) deletes the document. |
| Leaked / copied URLs | Each session is bound to an HttpOnly browser cookie (`VC_SessionBinding`); only its SHA-256 goes to the API. A URL opened in another browser gets a new empty session. This also blocks session fixation. |
| Browser closed without logout | Sliding expiry (the `timeout` in `web.config`). The binding cookie has no expiry, so it dies with the browser. |
| Database must not grow | TTL index `ttl_expires` (`expireAfterSeconds: 0`): Mongo deletes each session once it expires (checked about once a minute). |
| Two requests at once in one tab | Atomic Mongo lock per session, same semantics as ASP.NET's SQL Server provider. |

## Solution layout (`src/Sessions`)

| Project | Purpose |
|---|---|
| `Sessions.Service` | .NET 10 Web API: controller, API-key filter, DI (`Initializations/ServiceInitializer.cs`). |
| `Sessions.Service.Infrastructure` | Requests, responses, options, `ISessionStateService`. |
| `Sessions.Service.Library` | `SessionStateService`: validation, timeouts, lock rules. |
| `Sessions.Repo.Infrastructure` | `SessionDocument`, repo requests, `ISessionRepoService`. |
| `Sessions.Repo.Library` | `SessionRepoService`: Mongo queries, TTL index. |
| `VeriClaim.SessionState.Client` | **net462** DLL loaded by V2: provider, browser binding, `TabSessionModule`. No third-party dependencies. |
| `Sessions.Tests` | API tests against a real throwaway mongod (EphemeralMongo), plus the net462 client's HTTP code against the API over a real socket. |

```bash
dotnet test src/Sessions                                          # 26 tests
dotnet pack src/Sessions/VeriClaim.SessionState.Client -c Release   # NuGet for the V2 Bin
```

## API

All routes need the `X-Api-Key` header. Session data is opaque base64; the API never deserializes it.

| Method | Route | Provider call |
|---|---|---|
| POST | `/api/sessions/{app}/{id}/acquire` `{exclusive, browserKeyHash}` | `GetItem` / `GetItemExclusive` → `{status: Found\|Locked\|NotFound, lockId, lockAgeSeconds, timeoutMinutes, data, isUninitialized}` |
| PUT | `/api/sessions/{app}/{id}` `{lockId, data, timeoutMinutes, isNew, browserKeyHash}` | `SetAndReleaseItemExclusive` → `{saved}` |
| POST | `/api/sessions/{app}/{id}/uninitialized` `{timeoutMinutes, browserKeyHash}` | `CreateUninitializedItem` |
| POST | `/api/sessions/{app}/{id}/release` `{lockId}` | `ReleaseItemExclusive` |
| DELETE | `/api/sessions/{app}/{id}?lockId=` | `RemoveItem` (`Session.Abandon()`) |
| POST | `/api/sessions/{app}/{id}/touch` `{browserKeyHash}` | `ResetItemTimeout` |
| GET | `/health` | (anonymous) |

Configuration (`appsettings.json`): `Mongo:ConnectionString|DatabaseName|CollectionName`, `Sessions:MinTimeoutMinutes|MaxTimeoutMinutes|MaxDataBytes`,
`SessionApi:ApiKeys` (one per calling website; empty = every request refused).

## Turning it on in V2 (config only)

1. Add the `VeriClaim.SessionState.Client` NuGet package (it lands in `Bin` like the other `.refresh` packages).
2. In `web.config`, add the API key to `appSettings`, swap the `sessionState` element, and register the module:

```xml
<appSettings>
  <add key="SessionApiKey" value="(from the vault)" />
</appSettings>

<system.web>
  <sessionState mode="Custom" customProvider="SessionService"
                cookieless="UseUri" regenerateExpiredSessionId="true" timeout="120">
    <providers>
      <add name="SessionService"
           type="VeriClaim.SessionState.Client.SessionServiceStateStore, VeriClaim.SessionState.Client"
           serviceUrl="https://sessions.internal.example/"
           apiKeyAppSetting="SessionApiKey"
           applicationName="VeriClaimWebsite"
           bindingCookieSameSite="Lax" />
    </providers>
  </sessionState>
</system.web>

<system.webServer>
  <modules>
    <add name="TabSession" type="VeriClaim.SessionState.Client.TabSessionModule, VeriClaim.SessionState.Client" />
  </modules>
</system.webServer>
```

Optional provider attributes: `requestTimeoutMs` (10000), `maxConnections` (100), `compressionEnabled` (true),
`bindToBrowser` (true), `bindingCookieName` (`VC_SessionBinding`), `bindingCookieSameSite` (`Lax`; use `None` when
the V2 pages are framed by a different site, e.g. Angular on localhost against dev).

Roll back by restoring the old `<sessionState timeout="120" cookieName="ASP.NET_VC_dev" />` line.

## Known behaviour changes / to verify before production

- **`Session_End` in `Global.asax` no longer fires** (ASP.NET raises it only for InProc). The `OnlineUsers` counter,
  the active-session gauge and the `Session_End` log entry will drift; count live documents in Mongo instead.
- **Everything put in Session must be `[Serializable]`.** No unmarked `new X()` assignments were found, but values
  returned from methods are only checked at runtime ("Unable to serialize the session state").
- **Root-relative links** (119 JavaScript `location = '/x.aspx'`, 6 HTML) drop the URL id; `TabSessionModule`
  re-attaches it from the Referer. `Response.Redirect` is handled by ASP.NET itself. Absolute
  `https://…vericlaim.co.za/x.aspx` links (29) are also covered when same-origin.
- **V3 Angular** calls V2 pages directly (`/Services/GetTopNavCounters.aspx`, `AngularBridge.aspx`, the iframe). Those
  requests have no `/(S(id))/` segment, so Angular needs to learn the tab's id (e.g. from the bridge page) and
  prefix V2 URLs with it. Not done yet.
- The subdomain-per-tab workaround in `Default.aspx` (`MaxTabsOpen`) becomes unnecessary but is harmless.
- Every page request now makes 2 HTTP calls to the API; host it close to the web servers.
