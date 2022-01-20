using System.Collections;
using System.Globalization;

namespace QQBot.Plugin.Shared.Util;

/// <summary>
/// from https://gist.github.com/greatcodeeer/2d2cbe5b5c6e9a7c102d
/// </summary>
public static class ChinaDate
{
    private static readonly ChineseLunisolarCalendar China = new ChineseLunisolarCalendar();
    private static readonly Hashtable GHoliday = new Hashtable();
    private static readonly Hashtable NHoliday = new Hashtable();

    private static readonly string[] Jq =
    {
        "小寒", "大寒", "立春", "雨水", "惊蛰", "春分", "清明", "谷雨", "立夏", "小满", "芒种", "夏至", "小暑", "大暑", "立秋", "处暑", "白露", "秋分",
        "寒露", "霜降", "立冬", "小雪", "大雪", "冬至"
    };

    private static readonly int[] JqData =
    {
        0, 21208, 43467, 63836, 85337, 107014, 128867, 150921, 173149, 195551, 218072, 240693, 263343, 285989, 308563,
        331033, 353350, 375494, 397447, 419210, 440795, 462224, 483532, 504758
    };

    static ChinaDate()
    {
        //公历节日
        GHoliday.Add("0101", "元旦");
        GHoliday.Add("0214", "情人节");
        GHoliday.Add("0305", "雷锋日");
        GHoliday.Add("0308", "妇女节");
        GHoliday.Add("0312", "植树节");
        GHoliday.Add("0315", "消费者权益日");
        GHoliday.Add("0401", "愚人节");
        GHoliday.Add("0501", "劳动节");
        GHoliday.Add("0504", "青年节");
        GHoliday.Add("0601", "儿童节");
        GHoliday.Add("0701", "建党节");
        GHoliday.Add("0801", "建军节");
        GHoliday.Add("0910", "教师节");
        GHoliday.Add("1001", "国庆节");
        GHoliday.Add("1224", "平安夜");
        GHoliday.Add("1225", "圣诞节");

        //农历节日
        NHoliday.Add("0101", "春节");
        NHoliday.Add("0115", "元宵节");
        NHoliday.Add("0505", "端午节");
        NHoliday.Add("0815", "中秋节");
        NHoliday.Add("0909", "重阳节");
        NHoliday.Add("1208", "腊八节");
    }

    /// <summary>
    /// 获取农历
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string GetChinaDate(DateTime dt)
    {
        if (dt > China.MaxSupportedDateTime || dt < China.MinSupportedDateTime)
        {
            //日期范围：1901 年 2 月 19 日 - 2101 年 1 月 28 日
            throw new Exception(
                $"日期超出范围！必须在{China.MinSupportedDateTime.ToString("yyyy-MM-dd")}到{China.MaxSupportedDateTime.ToString("yyyy-MM-dd")}之间！");
        }

        var str   = $"{GetYear(dt)} {GetMonth(dt)}{GetDay(dt)}";
        var strJq = GetSolarTerm(dt);
        if (strJq != "")
        {
            str += " (" + strJq + ")";
        }

        var strHoliday = GetHoliday(dt);
        if (strHoliday != "")
        {
            str += " " + strHoliday;
        }

        var strChinaHoliday = GetChinaHoliday(dt);
        if (strChinaHoliday != "")
        {
            str += " " + strChinaHoliday;
        }

        return str;
    }

    /// <summary>
    /// 获取农历年份
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string GetYear(DateTime dt)
    {
        var          yearIndex = China.GetSexagenaryYear(dt);
        const string yearTg    = " 甲乙丙丁戊己庚辛壬癸";
        const string yearDz    = " 子丑寅卯辰巳午未申酉戌亥";
        // const string yearSx    = " 鼠牛虎兔龙蛇马羊猴鸡狗猪";
        // var          year      = China.GetYear(dt);
        var          yTg       = China.GetCelestialStem(yearIndex);
        var          yDz       = China.GetTerrestrialBranch(yearIndex);

        // var str = string.Format("[{1}]{2}{3}{0}", year, yearSx[yDz], yearTg[yTg], yearDz[yDz]);
        var str = $"{yearTg[yTg]}{yearDz[yDz]}年";
        return str;
    }

    /// <summary>
    /// 获取农历月份
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string GetMonth(DateTime dt)
    {
        var year        = China.GetYear(dt);
        var iMonth      = China.GetMonth(dt);
        var leapMonth   = China.GetLeapMonth(year);
        var isLeapMonth = iMonth == leapMonth;
        if (leapMonth != 0 && iMonth >= leapMonth)
        {
            iMonth--;
        }

        const string szText   = "正二三四五六七八九十";
        var          strMonth = isLeapMonth ? "闰" : "";

        strMonth += iMonth switch
        {
            <= 10 => szText.Substring(iMonth - 1, 1),
            11    => "十一",
            _     => "腊"
        };
        return strMonth + "月";
    }

    /// <summary>
    /// 获取农历日期
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string GetDay(DateTime dt)
    {
        var          iDay    = China.GetDayOfMonth(dt);
        const string szText1 = "初十廿三";
        const string szText2 = "一二三四五六七八九十";
        string       strDay;

        switch (iDay)
        {
            case 20:
                strDay = "二十";
                break;
            case 30:
                strDay = "三十";
                break;
            default:
                strDay =  szText1.Substring((iDay - 1) / 10, 1);
                strDay += szText2.Substring((iDay - 1) % 10, 1);
                break;
        }

        return strDay;
    }

    /// <summary>
    /// 获取节气
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string GetSolarTerm(DateTime dt)
    {
        var dtBase    = new DateTime(1900, 1, 6, 2, 5, 0);
        var strReturn = "";

        var y = dt.Year;
        for (var i = 1; i <= 24; i++)
        {
            var num   = 525948.76 * (y - 1900) + JqData[i - 1];
            var dtNew = dtBase.AddMinutes(num);
            if (dtNew.DayOfYear == dt.DayOfYear)
            {
                strReturn = Jq[i - 1];
            }
        }

        return strReturn;
    }

    /// <summary>
    /// 获取公历节日
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string? GetHoliday(DateTime dt)
    {
        string? strReturn = null;
        var     g         = GHoliday[dt.Month.ToString("00") + dt.Day.ToString("00")];
        if (g != null)
        {
            strReturn = g.ToString();
        }

        return strReturn;
    }

    /// <summary>
    /// 获取农历节日
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string? GetChinaHoliday(DateTime dt)
    {
        string? strReturn = null;
        var     year      = China.GetYear(dt);
        var     iMonth    = China.GetMonth(dt);
        var     leapMonth = China.GetLeapMonth(year);
        var     iDay      = China.GetDayOfMonth(dt);
        if (China.GetDayOfYear(dt) == China.GetDaysInYear(year))
        {
            strReturn = "除夕";
        }
        else if (leapMonth != iMonth)
        {
            if (leapMonth != 0 && iMonth >= leapMonth)
            {
                iMonth--;
            }

            var n = NHoliday[iMonth.ToString("00") + iDay.ToString("00")];

            if (n == null) return strReturn;

            if (strReturn == "")
            {
                strReturn = n.ToString();
            }
            else
            {
                strReturn += " " + n;
            }
        }

        return strReturn;
    }
}