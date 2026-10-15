# Space systems in one `WaaS.Space` project

## Decision

The Space system (shared base, Classic, Stretch) is one library, `WaaS.Space`, plus one
deployable per variant (`WaaS.Space.Classic.Worker`). It replaces eight projects:
`WaaS.Space.{DesiredState,ViewModel,Workflow}`, `WaaS.Space.Classic.{DesiredState,ViewModel,Workflow,Api}`
and `WaaS.Space.Stretch.{DesiredState,ViewModel}`.

```
src/Systems/Space/
  WaaS.Space/
    DesiredState/  ViewModel/  Workflow/  Api/        shared base
    Classic/{DesiredState,ViewModel,Workflow,Api}/
    Stretch/{DesiredState,ViewModel}/
  WaaS.Space.Classic.Worker/
```

## Why

- Every consumer (WebApi, Classic Worker) already referenced all layers; the split enforced nothing
  in practice and cost eight csproj files and cross-project `public` visibility.
- Classic and Stretch share most of their code; they are variants of one system.
- The generic `ToViewModel<T>` base mapping now sits with the shared code instead of a Classic-only project.

## Constraints kept

- **Namespaces are unchanged.** ObjectCompare persists `Type.FullName` in change records and
  resolves it by name, so renaming a DesiredState namespace would orphan stored records.
  Folders follow the namespaces, the RootNamespace is `WaaS.Space`.
- **`LookupResourceExtractor.IsDesiredStateType`** used the assembly name containing
  `DesiredState`. That no longer holds for `WaaS.Space`, so it now checks for a `DesiredState`
  namespace segment. Same set of types as before. The extractor is a prototype and is to be revisited.
- **Global usings are neutral only** (serialization, MVC, Temporal, Persistence). The DesiredState and
  ViewModel namespaces collide (`Account`, `CronTab`, `Metadata`, `ICredential`), so each file imports
  its own layer.

## Trade-offs accepted

- The compiler no longer stops DesiredState from referencing ViewModel (or Classic from Stretch).
  Folder and namespace discipline replaces it.
- A Stretch change rebuilds the Classic worker image.
- If the API contracts are ever published as a client package, ViewModel has to be split out again.

## Webshield follows the same rule

`WaaS.Webshield.{DesiredState,ViewModel,Workflow}` became one `WaaS.Webshield` library
(same folder-per-layer layout, namespaces unchanged). `WaaS.Webshield.Worker` stays separate.

The Webshield controller and its `Apply(mappings)` extension moved from `WaaS.WebApi` into
`WaaS.Webshield/Api/` (namespace `WaaS.Webshield.Api`), like the Classic controller. The library
therefore references `Microsoft.AspNetCore.App`; WebApi discovers the controller as an MVC
application part.

`WaaS.Webshield.ProtoBuf` also stays separate: it is generated code (protogen runs as a build
target) and the webshield-node mock references it without needing the rest of the system.
The Webshield global usings are neutral only (hosting, logging, Common.Workflow, Persistence); the
DesiredState and ProtoBuf namespaces are imported per file where needed.

## Wiring: one `Add...` extension per role, shared plumbing in `WaaS.Common.Workflow`

Each `Program.cs` used to carry its own copy of the stores, password client, Temporal runtime and
health check. They are now:

| Extension | Lives in | Registers |
|---|---|---|
| `AddWaasCore` | `WaaS.Common.Workflow` | stack instance, notification and tenant stores, password client, DB readiness check |
| `AddWaasTemporalClient` | `WaaS.Common.Workflow` | injectable Temporal client on the shared Prometheus runtime |
| `AddWaasTemporalWorker` | `WaaS.Common.Workflow` | hosted worker on the shared runtime (called by the worker extensions below) |
| `AddClassicWebspaceApi` / `AddWebshieldApi` | `WaaS.Space` / `WaaS.Webshield` | desired state store + `WaasService<T>` for the controller |
| `AddClassicWebspaceWorker` / `AddWebshieldWorker` | `WaaS.Space` / `WaaS.Webshield` | store, backend clients, activities, workflow, task queue (Webshield: RabbitMQ and listener too) |

They extend `IServiceCollection` (not `WebApplicationBuilder`) so tests can call them, like the existing
`AddTenantStore`. API and worker wiring are separate because they differ: the API needs the store and
`WaasService<T>` only; the worker also needs the backend client, activities and workflow.

The Temporal runtime is a process-wide lazy singleton in `WaasServiceCollectionExtensions`, since it owns
the Prometheus listener on :9000 and the client and worker must share it.

Task queue names are constants on the workflows (`PublishClassicWebspaceWorkflow.DefaultTaskQueue`,
`PublishWebshieldWorkflow.DefaultTaskQueue`); the literal `"space-classic"` is gone from the controller,
`WorkflowExecutor` and the worker.
