# AGENTS

## Toolchain
- Use Windows `dotnet` for real builds/tests. `global.json` still pins SDK `5.0`, but the active projects target `net8.0`.
- Main app build from repo root: `dotnet build Marisa.StartUp/Marisa.StartUp.csproj --no-restore`.
- `Marisa.StartUp.csproj` always runs `npm install` and `npx vite build` in `Marisa.Frontend` before build. For backend-only verification, build or test the specific project instead of `Marisa.StartUp`.
- There are no checked-in CI workflows and no npm `lint`, `test`, or `typecheck` scripts. Do not guess repo-wide validation commands that are not wired in the tree.

## Repo Shape
- `Marisa.StartUp` hosts ASP.NET, serves `wwwroot`, falls back to `index.html`, and listens on `http://0.0.0.0:14311`.
- Runtime message transport is `Marisa.Backend.NapCat`; startup wires `NapCatBackend.Config(Utils.Assembly().GetTypes())`.
- Plugin discovery is reflection-based. `BotDriver.Config(...)` only registers types from the `Marisa.Plugin` assembly with `[MarisaPlugin]`, skips `[MarisaPluginDisabled]`, and orders them by plugin priority.
- `Marisa.Plugin` contains concrete bot plugins. `Marisa.Plugin.Shared` and `Marisa.Plugin.Shared.FSharp` hold shared game/data logic. `Marisa.BotDriver` owns dispatch and plugin exception handling.
- For `Marisa.Plugin/MaiMaiDx`, keep helper/utility functions in `MaiMaiDx.Utils.cs`; keep `MaiMaiDx.cs` focused on plugin wiring and lifecycle.
- Do not add backward-compatibility shims for removed config or legacy behavior unless the task explicitly requires them; prefer clean removal of obsolete paths.
- In message handlers, if a local helper function is only used by one method, prefer placing it after the main flow's `return` so the primary logic stays top-to-bottom.

## Configuration
- `Marisa.Configuration.ConfigurationManager` is the source of truth. Relative paths are resolved from repo/config-root heuristics, not just the current working directory.
- Runtime config is file-based through `Marisa.StartUp/config.yaml`; there is no environment-variable binding layer in startup.
- `tempPath` and `resourceRoot` are the global roots; feature temp/resource paths are derived from them in `ResolveGamePaths(...)` and `ResolveResourcePaths(...)`.
- `databasePath: bot.db` is resolved relative to global `tempPath`, not the startup directory.
- If `resourceRoot` is empty, default lookup prefers `Marisa.Frontend/public/assets`, then published `wwwroot/assets`.
- Required config is supposed to fail through `MissingConfigurationException`; `MarisaPluginBase.ExceptionHandler(...)` turns that into a user-facing reply. Preserve that flow instead of adding ad hoc null checks.
- Shared DivingFish auth lives at top-level `divingFish.devToken`.
- If you touch `Marisa.StartUp/config.yaml`, scrub real tokens, IDs, and machine-local paths before commit. The committed file should keep structure, not live secrets.
- `Marisa.StartUp/config.yaml` may be locally marked `assume-unchanged`. When a config change must be committed, first run `git update-index --no-assume-unchanged Marisa.StartUp/config.yaml`, then stage and commit or amend only the scrubbed file, and finally restore the flag with `git update-index --assume-unchanged Marisa.StartUp/config.yaml`.
- After committing the scrubbed `config.yaml`, if local runtime secrets are still needed, restore them only in the working tree; do not put them back into Git history or the index.

## Storage
- Active persistence is Realm in `Marisa.Database`; `Marisa.EntityFrameworkCore` is not part of `Marisa.sln`.
- Open databases with `BotDbContext.OpenRealm()`. When inserting numeric-keyed objects, allocate IDs with `BotDbContext.NextId<T>(realm)`.

## Frontend Assets
- Frontend is Vue 3 + TypeScript + Vite in `Marisa.Frontend`.
- Runtime assets live under `Marisa.Frontend/public/assets`; bundled fonts are under `public/assets/font`.
- `vite.config.ts` keeps old files in `dist` (`emptyOutDir: false`) and emits stable names under `dist/js` and `dist/assets`. Clean stale output manually if asset behavior looks impossible.
- `Marisa.StartUp.csproj` copies `Marisa.Frontend/dist/**` into startup `wwwroot` on both build and publish. If frontend assets are missing at runtime, inspect the startup output, not only `dist`.

## Tests
- `Marisa.BotDriver.Test` is the safest focused suite. `DispatcherTest` copies `Marisa.StartUp/config.yaml`, rewrites `tempPath` and `databasePath`, and points `ConfigurationManager` at the temp config.
- Many `Marisa.Plugin.Test` cases point directly at `Marisa.StartUp/config.yaml` and can depend on real local config or external services.
- Focused commands:
  - `dotnet test Marisa.BotDriver.Test/Marisa.BotDriver.Test.csproj --no-restore --filter DialogManagerTest`
  - `dotnet test Marisa.BotDriver.Test/Marisa.BotDriver.Test.csproj --no-restore --filter DispatcherTest`
  - `dotnet test Marisa.Plugin.Test/Marisa.Plugin.Test.csproj --no-restore --filter DivingFish_Should_Fetch_Rating_By_Username_When_DevToken_Configured`
- The DivingFish test is conditional: it ignores itself unless `divingFish.devToken` is configured, then queries username `laplaze`.

## Behavior Traps
- Dialog behavior is split across `Marisa.Plugin/Dialog.cs` and `Marisa.Plugin.Shared/Dialog/DialogManager.cs`. The dialog plugin falls back from `(group,user)` to `(group,null)` and then tries to restore the old handler if downstream plugins do not permanently claim the key.
- NapCat private sessions with subtype `group` are intentionally mapped to `FriendMessage` in `NapCatBackend.PrivateMessageType(...)`; dialog and private-command behavior depends on this.
