using OpenTK.Graphics.OpenGL4;

namespace SupernovaLab.Rendering;

/// <summary>
/// GPU time of the scene's drawing from GL_TIME_ELAPSED queries (core since OpenGL 3.3), read late so the CPU never waits: results are
/// collected on later frames, oldest first, when the GPU has them. <see cref="Available"/> is false when the queries cannot be created or
/// fail; the governor then stays off (ruling R-P2-30). Create and use with the GL context current.
/// </summary>
public sealed class GpuTimer : IDisposable
{
    private const int Slots = 4;
    private readonly int[] _queries = new int[Slots];
    private readonly Queue<int> _pending = new();
    private readonly bool[] _busy = new bool[Slots];
    private int _next;
    private bool _active;

    public GpuTimer()
    {
        try
        {
            GL.GetError();
            GL.GenQueries(Slots, _queries);
            // Probe: one empty query must be accepted without a GL error.
            GL.BeginQuery(QueryTarget.TimeElapsed, _queries[0]);
            GL.EndQuery(QueryTarget.TimeElapsed);
            Available = GL.GetError() == OpenTK.Graphics.OpenGL4.ErrorCode.NoError;
            if (Available) { _busy[0] = true; _pending.Enqueue(0); _next = 1; }
        }
        catch
        {
            Available = false;
            try { GL.GetError(); } catch { }
        }
    }

    public bool Available { get; private set; }

    /// <summary>Starts timing (skipped when every query is still waiting for its result).</summary>
    public void Begin()
    {
        if (!Available || _active || _busy[_next]) return;
        GL.BeginQuery(QueryTarget.TimeElapsed, _queries[_next]);
        _active = true;
    }

    /// <summary>Ends the timing started by <see cref="Begin"/>.</summary>
    public void End()
    {
        if (!_active) return;
        GL.EndQuery(QueryTarget.TimeElapsed);
        _busy[_next] = true;
        _pending.Enqueue(_next);
        _next = (_next + 1) % Slots;
        _active = false;
    }

    /// <summary>Hands each finished measurement (seconds), oldest first, to <paramref name="onResult"/>; never waits for an unfinished one.</summary>
    public void Poll(Action<double> onResult)
    {
        if (!Available) return;
        try
        {
            while (_pending.Count > 0)
            {
                int slot = _pending.Peek();
                GL.GetQueryObject(_queries[slot], GetQueryObjectParam.QueryResultAvailable, out int ready);
                if (ready == 0) break;
                GL.GetQueryObject(_queries[slot], GetQueryObjectParam.QueryResult, out long ns);
                _pending.Dequeue();
                _busy[slot] = false;
                onResult(ns * 1e-9);
            }
        }
        catch
        {
            Available = false;
        }
    }

    public void Dispose()
    {
        try { GL.DeleteQueries(Slots, _queries); } catch { }
    }
}
