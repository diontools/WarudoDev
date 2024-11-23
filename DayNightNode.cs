
using System;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

[NodeType(
    Id = "1f6351fb-54c1-435d-87ab-4196eb68fc89",
    Title = "昼夜 DayNight",
    Category = "CATEGORY_EVENTS",
    Width = 1f
)]
public class DayNightNode : Node
{
    private DateTime _date;
    private bool _isInDay = false;
    private TimeSpan _startTime;
    private TimeSpan _endTime;

    [FlowInput]
    public Continuation Enter() => Exit;

    [FlowOutput]
    public Continuation Exit;

    [DataInput]
    [Label("緯度 Latitude")]
    public float Latitude = 34.661739f;

    [DataInput]
    [Label("経度 Longitude")]
    public float Longitude = 133.935032f;

    [DataOutput]
    [Label("日の出時刻")]
    public string StartTime() => _startTime.ToString(@"hh\:mm");

    [DataOutput]
    [Label("日の入り時刻")]
    public string EndTime() => _endTime.ToString(@"hh\:mm");

    [DataOutput]
    [Label("昼？")]
    public bool IsDay() => _isInDay;

    [DataOutput]
    [Label("夜？")]
    public bool IsNight() => !_isInDay;

    [Trigger]
    [Label("更新")]
    public void Trigger()
    {
        _date = default;
        Refresh();

        if (this.Exit != null)
        {
            InvokeFlow(nameof(Exit));
        }
    }

    protected override void OnCreate() => Refresh();

    public override void OnUpdate() => Refresh();

    private void Refresh()
    {
        var now = DateTime.Now;
        var date = now.Date;
        var time = now.TimeOfDay;

        var isChanged = false;
        if (_date != date)
        {
            var (start, end, mid) = CalcSunStartEndTime(date, this.Latitude, this.Longitude);
            _startTime = new TimeSpan((long)((double)start * TimeSpan.TicksPerHour));
            _endTime = new TimeSpan((long)((double)end * TimeSpan.TicksPerHour));
            _date = date;
            isChanged = true;
        }

        var isInDay = _startTime <= time && time < _endTime;
        if (_isInDay != isInDay)
        {
            _isInDay = isInDay;
            isChanged = true;
        }

        if (isChanged)
        {
            if (Exit != null)
            {
                InvokeFlow(nameof(Exit));
            }
        }
    }

    /// <summary>
    /// 日の出、日の入りを計算します。
    /// </summary>
    /// <param name="date">日付</param>
    /// <param name="lat">緯度</param>
    /// <param name="lon">経度</param>
    /// <remarks>参考文献: https://web.archive.org/web/20221007114356/http://www.iot-kyoto.com/satoh/2016/01/22/post-99/</remarks>
    static (float start, float end, float mid) CalcSunStartEndTime(DateTime date, float lat, float lon)
    {
        static float RAD(float v) => v * MathF.PI / 180f;
        static float DEG(float v) => v * 180f / MathF.PI;

        var firstDate = new DateTime(date.Year, 1, 1); // 年始
        var day = (date.Date - firstDate).Days; // 通算日 [0-365]
        var totalDays = DateTime.IsLeapYear(date.Year) ? 366 : 365; // うるう年は366日

        // ラジアンに変換する
        lat = RAD(lat);
        lon = RAD(lon);

        // 係数
        var khi = day * 2.0f * MathF.PI / totalDays;

        // 太陽赤緯[°]
        var delta = 0.006918f - 0.399912f * MathF.Cos(khi) + 0.070257f * MathF.Sin(khi)
                           - 0.006758f * MathF.Cos(2 * khi) + 0.000907f * MathF.Sin(2 * khi)
                           - 0.002697f * MathF.Cos(3 * khi) + 0.001480f * MathF.Sin(3 * khi);

        // 均時差[h]
        var Et = (0.0172f + 0.4281f * MathF.Cos(khi) - 7.3515f * MathF.Sin(khi)
                          - 3.3495f * MathF.Cos(2 * khi) - 9.3619f * MathF.Sin(2 * khi)) / 60.0f;

        // 南中時刻
        var mid = ((9.0f - Et + 12.0f) * 15.0f - DEG(lon)) / 15.0f;

        // 日出・日没時刻 : －大気差－眼高差－視半径＋地心視差 ≒ -0.899
        var omegaS = MathF.Acos((MathF.Sin(RAD(-0.899f)) - MathF.Sin(lat) * MathF.Sin(delta)) / (MathF.Cos(lat) * MathF.Cos(delta)));

        var start = (DEG(-omegaS) + (9.0f - Et + 12.0f) * 15.0f - DEG(lon)) / 15.0f;
        var end = (DEG(omegaS) + (9.0f - Et + 12.0f) * 15.0f - DEG(lon)) / 15.0f;

        return (start, end, mid);
    }
}
