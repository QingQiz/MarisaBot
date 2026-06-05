# QQBOT

> QQ机器人（试作型）

## 功能

本bot使用反射实时生成文档。具体文档请使用 `help` 指令获取 / [link](https://bot.qingqiz.uk/help)

## 部署

- `git clone https://github.com/QingQiz/MarisaBot/`
- 安装依赖
    - 安装 dotnet 8.0
    - 安装 Node.js（>= 18，用于编译前端页面，构建时会自动执行 `npm install && npx vite build`）
    - 安装 [NapCat](https://napneko.github.io/guide/install)
- 配置 NapCat
    - 安装 NapCat.Shell（参考 NapCat 官方文档）
    - 修改 NapCat 的 OneBot v11 配置文件（位于 NapCat 目录下的 `napcat/config/onebot11_{qq}.json`），开启反向 WebSocket 服务器：
        ```json
        {
          "network": {
            "websocketServers": [
              {
                "name": "marisa",
                "enable": true,
                "host": "127.0.0.1",
                "port": 31001,
                "messagePostFormat": "array",
                "reportSelfMessage": false,
                "token": "这里填写你的连接密钥",
                "enableForcePushEvent": true,
                "heartInterval": 30000
              }
            ],
            "websocketClients": []
          }
        }
        ```
- 改项目的配置文件`Marisa.StartUp/config.yaml`
    - 需要修改里面的各种路径，
        - 其中`resourceRoot`为资源根目录，默认指向`Marisa.Frontend\public\assets`
        - 其中`tempPath`为临时文件根目录，可随意选用，用于存放Bot执行过程中的缓存
        - 其中`ffmpegPath`为FFmpeg的路径，用于处理音频文件
    - 需要补充里面的一些token（可以先不补充，但是有些功能会失效）
        - `clientId`和`clientSecret`为osu!的API的token，可以在osu的用户设置界面进行申请
        - `divingFish.devToken` 为水鱼的开发者 token，需要联系水鱼本人获取
    - 配置 Marisa 连接 NapCat（需要与 NapCat 配置一致）：
        ```yaml
        napCat:
          endpoint: ws://127.0.0.1:31001
          token: 这里填写你的连接密钥
          selfId: 你的机器人QQ号
        ```
- 编译
    - `cd Marisa.StartUp`
    - `dotnet build -c Release`
- 运行
    - `cd bin/Release/net8.0/`
    - 运行 `Marisa.StartUp.exe`
