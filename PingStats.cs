using System;
using System.Collections.Generic;

namespace ATS.DarkSearch;

public class PingStats
{
    public enum PingState
    {
        Ok,
        Blacklisted,
        Throttled,
        Paused
    }

    private Dictionary<long, Dictionary<PingState, int>> _hourStats = new Dictionary<long, Dictionary<PingState, int>>();

    public void Update(string url, PingState state)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        var utcNow = DateTimeOffset.UtcNow;
        var now = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0);

        if (!_hourStats.ContainsKey(now.Ticks))
        {
            _hourStats[now.Ticks] = new Dictionary<PingState, int>
            {
                [PingState.Ok] = 0,
                [PingState.Blacklisted] = 0,
                [PingState.Throttled] = 0,
                [PingState.Paused] = 0
            };
        }
        
        _hourStats[now.Ticks][state]++;
    }

    public int Get(DateTimeOffset date, PingState state)
    {
        var hourDate = new DateTime(date.Year, date.Month, date.Day, date.Hour, 0, 0);
        if (!_hourStats.ContainsKey(hourDate.Ticks))
            return 0;

        return _hourStats[hourDate.Ticks][state];
    }
}