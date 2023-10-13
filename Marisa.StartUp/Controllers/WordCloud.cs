using Flurl.Http;
using JiebaNet.Segmenter;
using JiebaNet.Segmenter.PosSeg;
using Marisa.Plugin.Shared.Configuration;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Marisa.StartUp.Controllers;

[ApiController]
public class WordCloud : Controller
{
    private JiebaSegmenter _segmenter;
    private PosSegmenter _posSegmenter;

    public WordCloud()
    {
        ConfigManager.ConfigFileBaseDir = @"C:\Users\sofee\.nuget\packages\jieba.net\0.42.2\Resources";

        _segmenter = new JiebaSegmenter();

        const string dictUrl = "https://raw.githubusercontent.com/anderscui/jieba.NET/master/ExtraDicts/dict.txt.big";
        // dict to download to
        var destPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "dict.txt.big");

        if (!System.IO.File.Exists(destPath))
        {
            var dict = dictUrl.GetStringAsync().Result;
            System.IO.File.WriteAllText(destPath, dict);
        }
        
        _segmenter.AddWord("舞萌", 100, "n");
        _segmenter.AddWord("中二", 100, "n");

        _segmenter.LoadUserDict(destPath);

        _posSegmenter = new PosSegmenter(_segmenter);
    }

    [HttpGet]
    [Route("Api/[controller]")]
    public string Get(long groupId, int days = 7)
    {
        var path = ConfigurationManager.Configuration.WordCloud.TempPath;
        path = Path.Combine(path, groupId.ToString());

        var today = DateTime.Today;
        var files = new List<string>();

        for (var i = 0; i < days; i++)
        {
            var date = today.AddDays(-i);
            var file = Path.Combine(path, $"{date:yyyy-MM-dd}.txt");
            if (System.IO.File.Exists(file))
            {
                files.Add(file);
            }
        }

        var sentences = new List<string>();
        foreach (var file in files)
        {
            sentences.AddRange(System.IO.File.ReadAllLines(file).Select(Uri.UnescapeDataString));
        }

        // var cut = _posSegmenter.CutInParallel(sentences).Select(x => x.ToList()).ToList();

        // return JsonConvert.SerializeObject(sentences.Zip(cut).Select(x => (x.First, x.Second)).ToList());
        return JsonConvert.SerializeObject(sentences.Select(x => (x, _posSegmenter.Cut(x).ToList())).ToList());
    }
}