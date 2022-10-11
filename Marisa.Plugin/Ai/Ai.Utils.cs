using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Marisa.Plugin.Ai;

public partial class Ai
{
    private const int Height = 640;
    private const int Width = 640;

    public static async Task<string> Img2Img(string s1, string s2, string imgB64, double r = 1)
    {
        await Lock.WaitAsync();

        var w = Width;
        var h = Height;

        if (r > 1)
            w = (int)(w / r) / 64 * 64;
        else
            h = (int)(h / (1 / r)) / 64 * 64;

        try
        {
            var session = StringExt.RandomString(8);

            using var request = new HttpRequestMessage(new HttpMethod("POST"), "http://localhost/api/predict/");
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en-GB;q=0.8,en;q=0.7");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            request.Headers.TryAddWithoutValidation("Referer", "http://localhost/");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"106\", \"Google Chrome\";v=\"106\", \"Not;A=Brand\";v=\"99\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");

            request.Content = new StringContent(
                $"{{\"fn_index\":31,\"data\":[0,\"{s1}\",\"{s2}\",\"None\",\"None\",\"data:image/png;base64,{imgB64}\",null,null,null,\"Draw mask\",20,\"Euler a\",4,\"original\",false,false,1,1,7,0.75,-1,-1,0,0,0,false,{h},{w},\"Just resize\",false,32,\"Inpaint masked\",\"\",\"\",\"None\",\"\",\"\",1,50,0,false,4,1,\"\",128,8,[\"left\",\"right\",\"up\",\"down\"],1,0.05,128,4,\"fill\",[\"left\",\"right\",\"up\",\"down\"],false,false,null,\"\",\"\",64,\"None\",\"Seed\",\"\",\"Steps\",\"\",true,false,null,\"\",\"\"],\"session_hash\":\"{session}\"}}");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var response = await Client.SendAsync(request);
            var str      = await response.Content.ReadAsStringAsync();
            return new Regex(@"data:image/png;base64,(?<base64>.*?)""]").Match(str).Groups["base64"].Value;
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task<string> Txt2Img(string s1, string s2, double r = 1)
    {
        await Lock.WaitAsync();

        var w = Width;
        var h = Height;

        if (r > 1)
            w = (int)(w / r) / 64 * 64;
        else
            h = (int)(h / (1 / r)) / 64 * 64;

        try
        {
            var session = StringExt.RandomString(8);

            using var request = new HttpRequestMessage(new HttpMethod("POST"), "http://localhost/api/predict/");

            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en-GB;q=0.8,en;q=0.7");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            request.Headers.TryAddWithoutValidation("Referer", "http://localhost/");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"106\", \"Google Chrome\";v=\"106\", \"Not;A=Brand\";v=\"99\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");

            request.Content = new StringContent(
                $"{{\"fn_index\":12,\"data\":[\"{s1}\",\"{s2}\",\"None\",\"None\",28,\"Euler a\",false,false,1,1,7,-1,-1,0,0,0,false,{h},{w},false,false,0.7,\"None\",false,false,null,\"\",\"Seed\",\"\",\"Steps\",\"\",true,false,null,\"{{\\\"prompt\\\": \\\"{s1}\\\"], \\\"negative_prompt\\\": \\\"{s2}\\\", \\\"seed\\\": 108064068, \\\"all_seeds\\\": [108064068], \\\"subseed\\\": 1239389591, \\\"all_subseeds\\\": [1239389591], \\\"subseed_strength\\\": 0, \\\"width\\\": 512, \\\"height\\\": 512, \\\"sampler_index\\\": 0, \\\"sampler\\\": \\\"Euler a\\\", \\\"cfg_scale\\\": 7, \\\"steps\\\": 28, \\\"batch_size\\\": 1, \\\"restore_faces\\\": false, \\\"face_restoration_model\\\": null, \\\"sd_model_hash\\\": \\\"ab21ba3c\\\", \\\"seed_resize_from_w\\\": 0, \\\"seed_resize_from_h\\\": 0, \\\"denoising_strength\\\": null, \\\"extra_generation_params\\\": {{}}, \\\"index_of_first_image\\\": 0, \\\"infotexts\\\": [\\\"extremely detailed CG unity 8k wallpaper,black long hair,cute face,1 adlut girl,happy, green skirt dress, flower pattern in dress,solo,green gown,art of light novel,in field, Anne Dewailly, Franciszek Smuglewicz, Domenico Pozzi\\\\nSteps: 28, Sampler: Euler a, CFG scale: 7, Seed: 108064068, Size: 512x512, Model hash: ab21ba3c, Eta: 0.68, Clip skip: 2\\\"], \\\"styles\\\": [\\\"None\\\", \\\"None\\\"], \\\"job_timestamp\\\": \\\"20221010114321\\\", \\\"clip_skip\\\": 2}}\",\"\"],\"session_hash\":\"{session}\"}}");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var response = await Client.SendAsync(request);
            var str      = await response.Content.ReadAsStringAsync();

            return new Regex(@"data:image/png;base64,(?<base64>.*?)""]").Match(str).Groups["base64"].Value;
        }
        finally
        {
            Lock.Release();
        }
    }
}