# AGENTS

## Toolchain and Build
- Use Windows `dotnet`, not WSL `dotnet`, for real builds/tests. The repo targets `net8.0`, but `global.json` is stale (`5.0`) and WSL may pick an unsupported SDK.
- The main app build is `dotnet build Marisa.StartUp/Marisa.StartUp.csproj --no-restore` from Windows. `Marisa.StartUp.csproj` runs `npm install` and `npx vite build` in `Marisa.Frontend` before build, so Node/npm must be available.
- For focused backend verification that avoids the frontend build hook, build project files directly, e.g. `dotnet build Marisa.Plugin/Marisa.Plugin.csproj --no-restore` or `dotnet build Marisa.Plugin.Shared/Marisa.Plugin.Shared.csproj --no-restore`.
- `Marisa.StartUp.csproj` copies `Marisa.Frontend/dist/**` into `wwwroot` on both build and publish. If frontend assets are missing at runtime, check the startup project output, not just `Marisa.Frontend/dist`.

## Runtime Wiring
- The only active QQ backend is `Marisa.Backend.NapCat`. `Program.cs` wires the bot with `NapCatBackend.Config(Utils.Assembly().GetTypes())` and serves ASP.NET on `http://0.0.0.0:14311`.
- `Marisa.Plugin.Utils.Assembly()` is the plugin assembly entrypoint used for discovery. If plugin loading looks broken, start there.
- NapCat config is loaded from `Marisa.StartUp/config.yaml` under `napCat`. Runtime config is not env-driven.

## Configuration Rules
- Config lives in `Marisa.Configuration` and the namespace is `Marisa.Configuration`.
- `ConfigurationManager` is the source of truth for path resolution. `tempPath` and `resourceRoot` are the global roots; feature temp/resource paths are derived from them.
- `databasePath: bot.db` is intentionally resolved relative to global `tempPath`, not the startup directory.
- Missing required config is supposed to throw `MissingConfigurationException` from config property getters and be surfaced to users via plugin error handling. Preserve that pattern instead of adding ad hoc null checks.
- Current shared token layout uses top-level `divingFish.devToken`, not `chunithm.devToken`.

## Storage
- EF Core has been replaced by direct Realm usage in `Marisa.Database`; do not add new EF patterns.
- Open databases via `BotDbContext.OpenRealm()`. For numeric IDs, use `BotDbContext.NextId<T>(realm)`.
- Realm models are flattened `IRealmObject`s; inheritance is intentionally avoided because Realm source generation here does not support it.

## Frontend and Assets
- Runtime assets are under `Marisa.Frontend/public/assets`. `resourceRoot` in config should point there.
- Image/font code relies on bundled fonts under `public/assets/font`; do not assume Windows system fonts are present.

## Tests
- Dispatcher/integration-style bot tests are in `Marisa.BotDriver.Test`. They create an isolated temp config by copying `Marisa.StartUp/config.yaml` and overriding `tempPath`/`databasePath` via `ConfigurationManager.SetConfigFilePath(...)`.
- Plugin/integration tests are in `Marisa.Plugin.Test` and many of them read the real `Marisa.StartUp/config.yaml`. They are not all hermetic.
- The maimai DivingFish integration test in `Marisa.Plugin.Test/MaiMaiDxTest.cs` is conditional: it skips unless `divingFish.devToken` is configured, then uses `username = laplaze`.
- Focused test commands that are known to work:
  - `dotnet test E:\MarisaBot\Marisa.BotDriver.Test\Marisa.BotDriver.Test.csproj --no-restore --filter DialogManagerTest`
  - `dotnet test E:\MarisaBot\Marisa.Plugin.Test\Marisa.Plugin.Test.csproj --no-restore --filter DivingFish_Should_Fetch_Rating_By_Username_When_DevToken_Configured`

## Known Gotchas
- Parallel Windows builds can fail with file-lock errors on `Marisa.Configuration` / `Marisa.Database` outputs. If that happens, rerun the build serially before assuming the code is broken.
- `Dialog` fallback logic is sensitive to dialog key reuse. If you touch dialog behavior, re-check `Marisa.Plugin/Dialog.cs` and `Marisa.Plugin.Shared/Dialog/DialogManager.cs` together.
- NapCat private-message mapping is intentionally customized in `NapCatBackend.PrivateMessageType(...)`. Do not “simplify” message type mapping without checking dialog/private command behavior.
