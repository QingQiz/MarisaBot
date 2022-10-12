using System.Net;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Ai;
using Marisa.Utils.Cacheable;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Ai;

[MarisaPluginDoc("AI 相关")]
[MarisaPluginCommand("ai")]
public partial class Ai : MarisaPluginBase
{
    private const int PublicLimit = 15;
    private const int PrivateLimit = 30;
    private static readonly TimeSpan Frequency = TimeSpan.FromMinutes(1);

    private static readonly SemaphoreSlim Lock = new(1, 1);

    private static readonly Dictionary<long, DateTime> LastRequestTime = new();

    private static readonly HttpClient Client = new(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });

    [MarisaPluginDoc("ai作画，参数为：[h:w,]prompt[|negative prompt]")]
    [MarisaPluginCommand("draw")]
    private static async Task<MarisaPluginTaskState> Handler(Message message)
    {
        await Lock.WaitAsync();

        try
        {
            var uid = message.Sender!.Id;

            var dbContext = new BotDbContext();

            var limit = dbContext.AiDrawLimits.FirstOrDefault(x => x.UId == uid && x.DateTime == DateTime.Today) ?? new AiDrawLimit(uid);

            // 防止疯狂刷屏吧
            if (LastRequestTime.ContainsKey(uid))
            {
                if (DateTime.Now - LastRequestTime[uid] <= Frequency)
                {
                    message.Reply("频率限制！");
                    return MarisaPluginTaskState.CompletedTask;
                }
            }

            LastRequestTime[uid] = DateTime.Now;

            // 防止疯狂刷屏吧
            switch (message.Type)
            {
                case MessageType.GroupMessage when limit.UsedInPublic >= PublicLimit:
                    message.Reply("今日公开作画次数已达上限");
                    return MarisaPluginTaskState.CompletedTask;
                case MessageType.FriendMessage when limit.UsedInPrivate >= PrivateLimit:
                    message.Reply("今日私聊作画次数已达上限");
                    return MarisaPluginTaskState.CompletedTask;
            }

            var img = message.MessageChain!.Messages.FirstOrDefault(x => x.Type == MessageDataType.Image) as MessageDataImage;

            const string s = "本图片由人工智能模型自动生成，图片内容不代表作者的观点和立场，仅供娱乐。";

            var sSplit = message.Command.Split('|');

            var s1 = sSplit[0];
            var s2 = sSplit.Skip(1).FirstOrDefault() ??
                "sex, nsfw, r18, lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, jpeg artifacts, signature, watermark, username, blurry";

            var l = new List<string> { "nude", "nsfw", "porn", "r18", "sex", "politics", "testis", "testicle", "penis", "vaginal", "vagina" };

            l.ForEach(x => s1 = s1.Replace(x, "", StringComparison.OrdinalIgnoreCase));

            var r = 1.0;

            var scale = s1.Split(',').FirstOrDefault() ?? "";
            if (scale.Contains(':'))
            {
                var ratio = scale.Split(':');
                if (ratio.Length == 2)
                {
                    if (double.TryParse(ratio[0], out var x1) && double.TryParse(ratio[1], out var x2))
                    {
                        r  = x1 / x2;
                        s1 = string.Join(',', s1.Split(',').Skip(1));
                    }
                }
            }

            if (img == null && string.IsNullOrWhiteSpace(s1))
            {
                message.Reply("额");
                return MarisaPluginTaskState.CompletedTask;
            }

            if (message.Type == MessageType.GroupMessage)
            {
                limit.UsedInPublic++;
            }
            else if (message.Type == MessageType.FriendMessage)
            {
                limit.UsedInPrivate++;
            }
            
            var limitPrompt = "为防止滥用，您今日公开作画次数：" + limit.UsedInPublic + "/" + PublicLimit + "，私聊作画次数：" + limit.UsedInPrivate + "/" + PrivateLimit;

            dbContext.AiDrawLimits.InsertOrUpdate(limit);
            await dbContext.SaveChangesAsync();

            if (img == null)
            {
                message.Reply(new MessageDataText(s + "\n" + limitPrompt), MessageDataImage.FromBase64(await Txt2Img(s1, s2, r)));
            }
            else
            {
                var tempPath = Path.GetTempPath();
                var tempName = img.Url!.GetMd5Hash() + ".png";
                var p        = Path.Join(tempPath, tempName);

                var download = new CacheableImage(p, () =>
                {
                    var _ = img.Url!
                        .WithHeader("User-Agent",
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36")
                        .DownloadFileAsync(tempPath, tempName).Result;
                    return Image.Load(p);
                }).Value;

                message.Reply(new MessageDataText(s + "\n" + limitPrompt), MessageDataImage.FromBase64(await Img2Img(s1, s2, download.ToB64(), r)));
            }
        }
        finally
        {
            Lock.Release();
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}