using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivPrism;

public enum PrismLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, chat commands, or input hooks are used.</summary>
[Service]
public sealed class PrismClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public PrismClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivPrism.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivPrism.Ready");
        settings = pi.GetIpcSubscriber<object>("XivPrism.OpenSettings");
        Refresh();
    }

    public PrismLinkState State { get; private set; } = PrismLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        PrismLinkState.Ready => "Open your Prism.",
        PrismLinkState.Waiting => "Prism is getting ready. Try again in a moment.",
        _ => "Prism is unavailable. Enable XivPrism in the Dalamud plugin installer.",
    };

    [OnTick]
    private void Tick()
    {
        if (disposed || Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 1000;
        Refresh();
    }

    private void Refresh()
    {
        if (disposed) return;
        try
        {
            HasSettings = settings.HasAction;
            State = !toggle.HasAction || !ready.HasFunction ? PrismLinkState.Missing
                : ready.InvokeFunc() ? PrismLinkState.Ready : PrismLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = PrismLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != PrismLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = PrismLinkState.Missing;
            LogOnce(error); return false;
        }
    }

    public bool OpenSettings()
    {
        if (disposed) return false;
        Refresh();
        if (!HasSettings) return false;
        try { settings.InvokeAction(); return true; }
        catch (Exception error)
        {
            HasSettings = false;
            LogOnce(error); return false;
        }
    }

    private void LogOnce(Exception error)
    {
        var message = error.GetType().Name + ": " + error.Message;
        if (message == lastProblem) return;
        lastProblem = message;
        Logger.Warning("[Umbra.XivPrism] Prism connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = PrismLinkState.Missing; HasSettings = false;
    }
}
