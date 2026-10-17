using System.Diagnostics;

sealed class Engine
{
    const int Block = 64;
    readonly int _tracks;
    int _stages;
    float[] _state = [];
    readonly float[] _phase, _inc;
    readonly float[] _l = new float[Block], _r = new float[Block];

    public Engine(int tracks, int stages = 1)
    {
        _tracks = tracks;
        _phase = new float[tracks];
        _inc = Enumerable.Range(0, tracks).Select(i => 2 * MathF.PI * (110 + 7 * i) / 48000f).ToArray();
        SetStages(stages);
    }

    public int Stages => _stages;

    void SetStages(int stages) { _stages = stages; _state = new float[_tracks * stages * 2]; }

    public int Calibrate(double targetUs)
    {
        for (var s = 1; s < 4096; s++)
        {
            SetStages(s);
            for (var i = 0; i < 2000; i++) Process();
            var t0 = Stopwatch.GetTimestamp();
            for (var i = 0; i < 2000; i++) Process();
            var us = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency / 2000;
            if (us >= targetUs) return s;
        }
        return _stages;
    }

    public void Process()
    {
        var l = _l.AsSpan(); var r = _r.AsSpan();
        l.Clear(); r.Clear();
        const float b0 = 0.2f, b1 = 0.4f, b2 = 0.2f, a1 = -0.3f, a2 = 0.1f;
        for (var t = 0; t < _tracks; t++)
        {
            var ph = _phase[t]; var inc = _inc[t];
            var pan = (t & 1) == 0 ? 0.6f : 0.4f;
            for (var i = 0; i < Block; i++)
            {
                var x = Sin(ph); ph += inc; if (ph > MathF.PI) ph -= 2 * MathF.PI;
                for (var s = 0; s < _stages; s++)
                {
                    var k = (t * _stages + s) * 2;
                    var y = b0 * x + _state[k];
                    _state[k] = b1 * x - a1 * y + _state[k + 1];
                    _state[k + 1] = b2 * x - a2 * y;
                    x = y;
                }
                l[i] += x * pan; r[i] += x * (1 - pan);
            }
            _phase[t] = ph;
        }
    }

    static float Sin(float x) { var x2 = x * x; return x * (1f - x2 * (0.16605f - x2 * 0.00761f)); }
}
