# QQBOT

> QQ机器人（试作型）

## 功能

本bot使用反射实时生成文档。具体文档请使用 `help` 指令获取

## 部署

- `git clone https://github.com/QingQiz/MarisaBot/`
- 安装依赖
    - 安装 dotnet 8.0
    - 安装 Node.js
- 改项目的配置文件`Marisa.StartUp/config.yaml`
    - 需要修改里面的各种路径，
        - 其中`resourceRoot`为资源根目录，默认指向`Marisa.Frontend\public\assets`
        - 其中`tempPath`为临时文件根目录，可随意选用，用于存放Bot执行过程中的缓存
        - 其中`ffmpegPath`为FFmpeg的路径，用于处理音频文件
    - 需要补充里面的一些token（可以先不补充，但是有些功能会失效）
        - `clientId`和`clientSecret`为osu!的API的token，可以在osu的用户设置界面进行申请
        - `divingFish.devToken` 为水鱼的开发者 token，需要联系水鱼本人获取
- 编译
    - `cd Marisa.StartUp`
    - `dotnet build -c Release`
- 运行
    - `cd bin/Release/net8.0/`
    - 运行 `Marisa.StartUp.exe`
