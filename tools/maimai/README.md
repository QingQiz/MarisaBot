# 曲目称号资源

`generate_song_titles.py` 扫描完整资源包中的 Title、MusicGroup 和 Music XML，按游戏的 A 层 → N 层覆盖顺序取最终有效数据。不会按新增版本过滤，也不会把歌曲 ID 取模或从称号文案猜测缺失的歌曲组。

输出目录是 `Marisa.Frontend/public/assets/maimai`：

| 文件 | 用途 |
|---|---|
| `song_titles.json` | 生成时公开曲库已收录歌曲的称号 |
| `song_titles_pending.json` | 尚未收录歌曲的完整称号关系，供以后上线直接查询 |
| `song_titles_source.json` | 按称号 ID 保存原始条件、XML 路径、哈希、组成员和版本来源 |
| `song_titles_unresolved.json` | 源资源缺少组定义的称号；不猜 ID，不加入歌曲索引 |
| `song_titles_manifest.json` | 生成口径、公开曲库快照 ID 和输出数量 |

当前／预存只是生成时的文件分区。前端每次查称号会按完整歌曲 ID 合并两份表；预存表没有永久禁用名单。因此，歌曲以后进入水鱼或落雪实时曲库，选曲后可以直接读取已保存的称号，无需移动文件或重新生成。不能单凭曲目上线断言每一个日服称号国服已经开放。

`maititle` 选曲前按需刷新两家的公共曲库，缓存有效期为 30 分钟。同 ID 保持水鱼资料优先，落雪先上线的歌曲按其真实 SD／DX／宴谱资料补入。某个服务失败会沿用该服务上次成功的数据；初次水鱼失败仍使用原 `SongInfo.json` fallback。刷新重建歌曲和别名索引，不改用户别名文件，也不改变取玩家成绩的权限策略。

数据来自日服资源，`source.region` 和 `source.version` 保留这一事实。N100 历史补充曲目保留来源；不把日服谱面、假 chart ID 或假 note 数据加入 `SongInfo.json`。

## 更新

先准备公开水鱼 `music_data` 原响应，或水鱼／落雪完整 ID 的并集 JSON 数组（落雪普通 DX 为 `10000 + 基础 ID`，普通 SD 为基础 ID；宴保留其独立 ID）。也可以使用已有 manifest 中的 `catalog.songIds` 重现相同分区。

```powershell
py -3 tools/maimai/generate_song_titles.py `
    --resources E:/sdez/SDEZ170_nobga.zip `
    --catalog path/to/current_catalog_ids.json
```

多份完整 ID 快照可同时传给 `--catalog`，生成器取并集。`--output` 可指定隔离目录用于复核，不覆盖仓库资源。生成过程不需要玩家凭据。

## 解锁条件

- 多个有效条件与歌曲组成员都是 AND；组内全部歌曲必须满足条件。
- Total 的 `ALL` 只指 BASIC、ADVANCED、EXPERT、MASTER；额外 REM 条件才要求 Re:MASTER。
- `check.difficultyIndices` 明确指定需要验证的谱。缺少指定谱不能视为达成。
- MISS 是恰好指定次数；SPEED 1、SONIC 和镜像按结构字段区分。
- 多曲组、同次投币、重复次数、多人、设置、MISS、速度等条件保留 `check=null`，不从单曲最佳成绩误推达成。
- `norm` 是原始展示文案，可能与组成员不一致；显示条件与歌曲索引以结构化 XML 为准。

2026-10-11 的 SDEZ170 资源可解析 2258 条关系，另有 17 个称号缺少原始组定义，已保存在 unresolved 文件。它们需要取得相应 `MusicGroup.xml` 才能补入，不能按文案推断 ID。

## 验证

前端回归执行实际 TypeScript 判定函数，涵盖四谱／五谱、BLACKBOX、多曲仅展示、未玩、未知 DX 分数、预存曲目查表以及 SD/DX ID 分离：

```powershell
cd Marisa.Frontend
node --test tests/song_titles.cjs
```

资源集合、稀有度、原文条件和判定范围还应与完整 XML 独立逐条比对；不要仅用当前公开曲库的无映射数量代替资源覆盖检查。

```powershell
py -3 tools/maimai/validate_song_titles.py `
    --resources E:/sdez/SDEZ170_nobga.zip `
    --catalog path/to/current_catalog_ids.json

dotnet test Marisa.Plugin.Test/Marisa.Plugin.Test.csproj --no-restore `
    --filter "MaiMaiSongCatalogTest|SongDbResetTest"
```

XML 验证器独立重建关系与条件，不导入生成器实现。后端测试只用公共资料 fixture、模拟响应和隔离文件，覆盖新歌上线、服务失败缓存、ID 转换、真实物量和别名保持。
