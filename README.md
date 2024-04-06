# QQBOT

> QQ机器人（试作型）

## 功能

> **注意**，本bot使用反射实时生成文档，该模块可能过时。具体文档请使用 `help` 指令获取

**来个人和我一块维护啊（震声**

<details>
<summary> 展开 </summary>

### 舞萌 DX

命令前缀为 `舞萌`/`maimai`/`mai`

| 功能   | 子命令                       | 参数                        | 功能                                              |
|:-----|:--------------------------|:--------------------------|-------------------------------------------------|
| 查分   | `查分`/`b40`                | [名字] / @某人                |                                                 |
|      | `b50`                     | [名字] / @某人                | 查 best 50                                       |
| 查歌   | `search`/`song`/`搜索`      | 名字/id/`id`+id             |                                                 | 
| list | `list/ls` `base`/`定数`/`b` | 定数1[-定数2]                 | 定数随歌, 列出区间 `[定数1,定数2]` 的歌 <br/>（定数2可忽略，默认等于定数1） |
|      | `list/ls` `bpm`           | bpm1[-bpm2]               | 与 base 参数类似，只不过是筛选 bpm                          |
|      | `list/ls` `level/lv/等级`   | 乐曲等级                      | 选择等于 `乐曲等级` 的歌                                  |
|      | `list/ls` `charter`/`c`   | 谱师                        | 选择该谱师的歌                                         |
|      | `list/ls` `artist`/`a`    | 曲师                        | 选择该曲师的歌                                         |
| 猜曲   | `猜曲`/`猜歌`                 |                           | 开启猜曲模式，可以发送 `答案` 或 `结束猜曲` 来关闭会话                 |
|      | `猜曲`/`猜歌`                 | `c:`正则表达式                 | 开启猜曲模式，并使用传入的正则表达式过滤歌曲类别                        |
|      | `猜曲`/`猜歌`                 | `v2`                      | 开启猜曲模式，不过是听歌而不是猜封面                              |
|      | `猜曲`/`猜歌`                 | `排名`                      | 查看猜曲排名                                          |
| 推荐歌曲 | `什么`/`打什么`/`打什么歌`         | `推分`/`加分`/`上分`/`恰分`/任意字符串 | 推荐恰分歌曲/随机给出一个歌                                  |
| 别名   | `alias get`               | 乐曲 (别) 名                  | 获取歌曲的所有别名                                       |
|      | `alias set`               | 乐曲**原名**`:=`新的别名          | 添加别名                                            |
| 统计   | `summary` `lv`/`level`    | 乐曲等级                      | 给出乐曲等级的统计                                       |
|      | `summary` `base`/`b`      | 定数1`-`定数2                 | 给出乐曲定数的统计                                       |
|      | `summary` `version`/`ver` | 版本                        | 给出乐曲版本的统计                                       |
|      | `summary` `genre`/`type`  | 类别                        | 给出乐曲类别的统计                                       |
| 容错率  | `tolerate` / `容错率`        | 歌名                        | 给出指定达成率的容错，bot会询问难度和预期达成率，跟着提示走就行               |

**注**: 
- 该功能的所有命令均大小写**不**敏感
- 猜曲功能仅群中使用

---

### osu!

命令前缀为 `osu`

> 兼容猫猫的指令，就懒得写了

| 功能      | 子命令                 | 参数    | 功能      |
|:--------|:--------------------|:------|---------|
| 绑定      | bind                | 游戏名字  | 字面义     |
| 信息      | info                |       | 给出账户的信息 |
| 设置查分模式  | setmode / set mode  | 游戏模式  | 字面义     |

**注**:
- 该功能的所有命令均大小写**不**敏感
- 这个插件还未开发完成

### Arcaea


命令前缀为 `arcaea`/`arc`/`阿卡伊`

| 功能  | 子命令                  | 参数                | 功能                              |
|:----|:---------------------|:------------------|---------------------------------|
| 查歌  | `search`/`song`/`搜索` | 名字                |                                 | 
| 猜曲  | `猜曲`/`猜歌`            |                   | 开启猜曲模式，可以发送 `答案` 或 `结束猜曲` 来关闭会话 |
|     | `猜曲`/`猜歌`            | `v2`              | 开启猜曲模式，但是听歌猜曲                   |
|     | `猜曲`/`猜歌`            | `排名`              | 查看猜曲排名                          |
| 别名  | `alias get`          | 乐曲 (别) 名          | 获取歌曲的所有别名                       |
|     | `alias set`          | 乐曲**原名**`:=`新的别名  | 添加别名                            |

**注**: 
- 该功能的所有命令均大小写**不**敏感，~~不提供查分功能，SB616谁爱伺候谁伺候~~
- 猜曲功能仅群中使用

---

### Ping

> 测试 bot 是否存活

  触发：`:ping`

**注**: 该功能的所有命令均大小写**敏感**

---

### 吃啥

> 这是一个用来解决「中午吃什么」这一被人类公认排在人生 N 大难题前列的问题的功能

触发条件：`吃什么` 或 `吃啥` 字符串

---

### Select

> 解决选择困难症的功能

触发条件：`请问(A)还是(B)还是(C)`

从 `A` `B` `C` 随机选一个，支持多个还是并列

---

### Peek

> 偷窥作者屏幕（？

触发：`:peek`

作者可以使用 `:peek0/1` 禁止/允许偷窥

---

### 五兆亿

> 生成 `五兆亿` 图片

触发：`生成`top`/`bottom

---

### 今日运势

> 字面义

触发：`今日运势`/`jrys`

---

### 随机图片

> 从作者的图库中随机抽取一张图片（应该是有一点色图的）

触发1：`抽图`/`ct`
触发2：`看看`/`kk`+图库名

**注**: 不加图库名则给出所有图库

---

### 帮助

> 获取 bot 的使用文档

命令前缀为 `帮助`/`help`

---

### 复读

> bot 会自动复读或打断复读

触发：复读

---

### 指令

前缀为 `:cmd`

| 功能     | 子命令      | 参数  | 功能                  |
|:-------|:---------|:----|---------------------|
| 重启bot  | `reboot` |     | 重启bot以重新加载某些资源      |
| SHELL  | `shell`  |     | 启动一个 cmd 的交互式 SHELL |

---

### 黑名单

前缀为 `:ban`

| 功能        | 子命令          | 参数         | 功能     |
|:----------|:-------------|:-----------|--------|
| ban某人     |              | qq/@某人     | ban掉某人 |
| 列出被ban的人  | `list`/`ls`  |            | 字面义    |

</details>

## 部署

- `git clone https://github.com/QingQiz/MarisaBot/`
- 安装依赖
    - 安装 dotnet 7.0
    - 下载 ffmpeg.exe
    - 安装 Node.js
    - 安装`Marisa.Plugin.Shared\Resource\Font`下的所有字体
- 选择一个QQ机器人框架，如Mirai/Go-CQ，以Mirai为例
    - 依照Mirai的[官方文档](https://github.com/mamoe/mirai/blob/dev/docs/ConsoleTerminal.md)部署Mirai
    - 安装Mirai的[HTTP插件](https://github.com/project-mirai/mirai-api-http)
    - 配置Mirai的HTTP插件，这里给出一个例子
      ```yaml
      adapters:
        - ws
      enableVerify: true
      ## 用于验证的 key，填你自己的Key
      verifyKey: KEY
      debug: false
      singleMode: false
      cacheSize: 4096
      adapterSettings:
        ws:
          host: localhost
          port: 18080
          reservedSyncId: -1
      ```
- 在 Mirai 里登陆 bot
- 改项目的配置文件`Marisa.StartUp/config.yaml`
    - 需要修改里面的各种路径，
        - 其中`resourcePath`中的资源为源码中自带的，只需要改前缀
        - 其中`tempPath`为临时文件夹，可随意选用，用于存放Bot执行过程中的缓存
        - 其中`ffmpegPath`为FFmpeg的路径，用于处理音频文件
    - 需要补充里面的一些token（可以先不补充，但是有些功能会失效）
        - `clientId`和`clientSecret`为osu!的API的token，可以在osu的用户设置界面进行申请
        - `devToken`为水鱼的token，需要联系水鱼本人获取
- 创建数据库
    - 安装 dotnet ef `dotnet tool install --global dotnet-ef`
    - `cd Marisa.EntityFrameworkCore`
    - `dotnet ef database update`
    - 需要注意的是
        - 需要开启数据库的tcp访问
        - 需要开启数据库的Windows身份验证
        - 需要手动创建名为`QQBOT_DB`的数据库
        - 若不想注意上面这些，则需要修改[该行](https://github.com/QingQiz/MarisaBot/blob/984715273c73aa9d3d9717c0a333f2569123df62/Marisa.EntityFrameworkCore/BotDbContext.cs#L26)来更换数据库和认证方式
- 编译
    - `cd Marisa.StartUp`
    - `dotnet build -c Release`
- 运行
    - `cd bin/Release/net7.0/`
    - 运行 `Marisa.StartUp.exe` 命令行参数如下（顺序敏感）：
        - [Mirai-API-http](https://github.com/project-mirai/mirai-api-http) 的服务地址，如 `ws://127.0.0.1:18080`
        - bot的QQ账号，如 `123456789`
        - Mirai-API-http 的认证密钥，如 <https://github.com/project-mirai/mirai-api-http#settingyml%E6%A8%A1%E6%9D%BF> 中的 `verifyKey`
        - （可选）传入`gocq`字符串则以Go-CQ为框架，否则以Mirai为框架


## 备忘

<details>
<summary> 展开 </summary>

- 环境变量
  - DEV:
    - 设置了则前端地址指定为 `http://localhost:3000`
    - 未设置则前端地址指定为 `https://localhost:14311`
  - RESPONSE:
    - 设置了则 bot 只会相应该用户的消息

</details>