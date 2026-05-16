using AiImeShell.Interop;
using System.Runtime.InteropServices;

namespace AiImeShell;

/// <summary>
/// Manages a single TSF composition (underlined input string in the document).
/// All mutations happen inside an edit session on the calling thread.
/// </summary>
internal sealed class CompositionManager
{
    private ITfComposition? _composition;
    private readonly int    _clientId;

    public bool IsComposing => _composition != null;

    public CompositionManager(int clientId) => _clientId = clientId;

    // ── Public operations (called from edit sessions) ─────────────────────────

    /// <summary>Start a new composition at the current selection.</summary>
    public void StartComposition(int ec, ITfContext context, ITfCompositionSink sink)
    {
        if (_composition != null) return;

        var insert = (ITfInsertAtSelection)context;
        insert.InsertTextAtSelection(ec, TsfConst.TF_IAS_QUERYONLY, "", 0,
                                     out ITfRange range);

        var ctxComp = (ITfContextComposition)context;
        ctxComp.StartComposition(ec, range, sink, out _composition);
        Marshal.ReleaseComObject(range);
    }

    /// <summary>Replace composition text with new string.</summary>
    public void SetText(int ec, string text)
    {
        if (_composition == null) return;
        _composition.GetRange(out ITfRange range);
        range.SetText(ec, 0, text, text.Length);
        Marshal.ReleaseComObject(range);
    }

    /// <summary>Commit composition (end without cancelling).</summary>
    public void EndComposition(int ec)
    {
        if (_composition == null) return;
        _composition.EndComposition(ec);
        Marshal.ReleaseComObject(_composition);
        _composition = null;
    }

    /// <summary>
    /// Cancel: clear text then end. Used on Escape.
    /// The text content is cleared before EndComposition so TSF removes it.
    /// </summary>
    public void CancelComposition(int ec)
    {
        if (_composition == null) return;
        _composition.GetRange(out ITfRange range);
        range.SetText(ec, 0, "", 0);
        Marshal.ReleaseComObject(range);
        _composition.EndComposition(ec);
        Marshal.ReleaseComObject(_composition);
        _composition = null;
    }

    /// <summary>Called by TSF when composition is terminated externally.</summary>
    public void OnTerminated()
    {
        if (_composition != null)
        {
            Marshal.ReleaseComObject(_composition);
            _composition = null;
        }
    }

    // ── Edit session helpers ──────────────────────────────────────────────────

    /// <summary>Run an action in a synchronous read-write edit session.</summary>
    public void RequestEdit(ITfContext context, Action<int> action)
    {
        var session = new LambdaEditSession(action);
        context.RequestEditSession(_clientId, session,
            TsfConst.TF_ES_SYNC | TsfConst.TF_ES_READWRITE, out _);
    }
}

/// <summary>Adapts a lambda to ITfEditSession.</summary>
[ClassInterface(ClassInterfaceType.None)]
internal sealed class LambdaEditSession : ITfEditSession
{
    private readonly Action<int> _action;
    public LambdaEditSession(Action<int> action) => _action = action;
    public int DoEditSession(int ec) { _action(ec); return 0; }
}
