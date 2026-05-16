using AiImeShell.Interop;
using System.Runtime.InteropServices;
using static AiImeShell.Interop.NativeMethods;

namespace AiImeShell;

/// <summary>
/// IME state machine + TSF key event sink.
///
/// States:
///   Idle      – no active composition
///   Composing – user typing romaji/hiragana (composition shown)
///   Converting – space pressed; candidate window visible
/// </summary>
[ClassInterface(ClassInterfaceType.None)]
internal sealed class KeyEventSink : ITfKeyEventSink, ITfCompositionSink
{
    private enum ImeState { Idle, Composing, Converting }

    private ImeState         _state     = ImeState.Idle;
    private string           _hiragana  = "";          // current hiragana buffer
    private string[]         _candidates = [];
    private int              _candidateIdx;
    private string           _context   = "";          // preceding sentences

    private readonly CompositionManager _comp;
    private readonly CandidateWindow    _candidateWin;
    private readonly RomajiConverter    _romaji = new();
    private ITfContext?                 _lastContext;

    public KeyEventSink(int clientId, CandidateWindow candidateWin)
    {
        _comp         = new CompositionManager(clientId);
        _candidateWin = candidateWin;
        _candidateWin.Committed += OnCandidateCommitted;
    }

    // ── ITfKeyEventSink ───────────────────────────────────────────────────────

    public int OnSetFocus(bool fForeground) => 0;

    public int OnTestKeyDown(ITfContext pic, IntPtr wParam, IntPtr lParam, out bool pfEaten)
    {
        pfEaten = ShouldEat((int)wParam);
        return 0;
    }

    public int OnKeyDown(ITfContext pic, IntPtr wParam, IntPtr lParam, out bool pfEaten)
    {
        pfEaten = false;
        int vk = (int)wParam;
        _lastContext = pic;

        switch (_state)
        {
            case ImeState.Idle:
                if (IsRomajiKey(vk))
                {
                    pfEaten = true;
                    HandleRomaji(pic, (char)vk);
                }
                break;

            case ImeState.Composing:
                if (IsRomajiKey(vk))
                {
                    pfEaten = true;
                    HandleRomaji(pic, (char)vk);
                }
                else if (vk == NativeMethods.VK_SPACE)
                {
                    pfEaten = true;
                    StartConversion(pic);
                }
                else if (vk == NativeMethods.VK_RETURN)
                {
                    pfEaten = true;
                    CommitHiragana(pic);
                }
                else if (vk == NativeMethods.VK_ESCAPE)
                {
                    pfEaten = true;
                    CancelComposition(pic);
                }
                else if (vk == NativeMethods.VK_BACK)
                {
                    pfEaten = true;
                    HandleBackspace(pic);
                }
                break;

            case ImeState.Converting:
                if (vk == NativeMethods.VK_SPACE || vk == NativeMethods.VK_DOWN)
                {
                    pfEaten = true;
                    _candidateWin.MoveDown();
                }
                else if (vk == NativeMethods.VK_UP)
                {
                    pfEaten = true;
                    _candidateWin.MoveUp();
                }
                else if (vk == NativeMethods.VK_RETURN)
                {
                    pfEaten = true;
                    _candidateWin.CommitSelected();
                }
                else if (vk == NativeMethods.VK_ESCAPE)
                {
                    pfEaten = true;
                    RevertToComposing(pic);
                }
                else if (vk >= '1' && vk <= '9')
                {
                    int idx = vk - '1';
                    if (idx < _candidates.Length)
                    {
                        pfEaten = true;
                        _candidateWin.SelectedIndex = idx;
                        _candidateWin.CommitSelected();
                    }
                }
                break;
        }
        return 0;
    }

    public int OnTestKeyUp(ITfContext pic, IntPtr wParam, IntPtr lParam, out bool pfEaten)
    {
        pfEaten = _state != ImeState.Idle && ShouldEat((int)wParam);
        return 0;
    }

    public int OnKeyUp(ITfContext pic, IntPtr wParam, IntPtr lParam, out bool pfEaten)
    {
        pfEaten = false;
        return 0;
    }

    public int OnPreservedKey(ITfContext pic, in Guid rguid, out bool pfEaten)
    {
        pfEaten = false;
        return 0;
    }

    // ── ITfCompositionSink ────────────────────────────────────────────────────

    public int OnCompositionTerminated(int ecWrite, ITfComposition pComposition)
    {
        _comp.OnTerminated();
        _hiragana = "";
        _romaji.Clear();
        _state = ImeState.Idle;
        return 0;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private bool ShouldEat(int vk)
    {
        if (_state == ImeState.Idle) return IsRomajiKey(vk);
        if (_state == ImeState.Composing)
            return IsRomajiKey(vk)
                || vk == NativeMethods.VK_SPACE
                || vk == NativeMethods.VK_RETURN
                || vk == NativeMethods.VK_ESCAPE
                || vk == NativeMethods.VK_BACK;
        if (_state == ImeState.Converting)
            return vk == NativeMethods.VK_SPACE
                || vk == NativeMethods.VK_RETURN
                || vk == NativeMethods.VK_ESCAPE
                || vk == NativeMethods.VK_UP
                || vk == NativeMethods.VK_DOWN
                || (vk >= '1' && vk <= '9');
        return false;
    }

    private static bool IsRomajiKey(int vk) =>
        vk >= 'A' && vk <= 'Z';

    private void HandleRomaji(ITfContext pic, char c)
    {
        string? hiragana = _romaji.Feed(c);
        if (hiragana == null) return; // non-romaji, ignore

        if (hiragana.Length > 0)
        {
            _hiragana += hiragana;
            UpdateComposition(pic);
        }
        else if (_romaji.Pending.Length > 0)
        {
            // Partial mora — show pending romaji as tentative
            UpdateComposition(pic, showPending: true);
        }
    }

    private void UpdateComposition(ITfContext pic, bool showPending = false)
    {
        string text = _hiragana + (showPending ? _romaji.Pending : "");
        if (text.Length == 0) return;

        _comp.RequestEdit(pic, ec =>
        {
            if (!_comp.IsComposing)
                _comp.StartComposition(ec, pic, this);
            _comp.SetText(ec, text);
        });
        _state = ImeState.Composing;
    }

    private void StartConversion(ITfContext pic)
    {
        // Flush any pending romaji
        string flush = _romaji.Flush();
        if (flush.Length > 0)
        {
            _hiragana += flush;
            UpdateComposition(pic);
        }

        if (_hiragana.Length == 0) return;

        string[] dict = AiImeCoreBridge.LookupDictionary(_hiragana);
        if (dict.Length == 0)
        {
            // No candidates — show hiragana itself
            dict = [_hiragana];
        }

        // Rerank with BERT only once the model has finished loading (background init)
        string[] ranked = AiImeCoreBridge.IsInitialized
            ? AiImeCoreBridge.Rerank(_context, _hiragana, dict)
            : dict;
        _candidates    = ranked.Length > 0 ? ranked : dict;
        _candidateIdx  = 0;

        // Update composition to show first candidate
        string first = _candidates[0];
        _comp.RequestEdit(pic, ec => _comp.SetText(ec, first));

        // Show candidate window near caret (simplified: use screen center as fallback)
        var pt = GetCaretScreenPos();
        _candidateWin.ShowAt(pt, _candidates);

        _state = ImeState.Converting;
    }

    private void CommitHiragana(ITfContext pic)
    {
        string flush = _romaji.Flush();
        if (flush.Length > 0) _hiragana += flush;

        _comp.RequestEdit(pic, ec =>
        {
            _comp.SetText(ec, _hiragana);
            _comp.EndComposition(ec);
        });
        _context += _hiragana;
        TrimContext();
        Reset();
    }

    private void OnCandidateCommitted(string candidate)
    {
        if (_lastContext == null) return;
        _comp.RequestEdit(_lastContext, ec =>
        {
            _comp.SetText(ec, candidate);
            _comp.EndComposition(ec);
        });
        _context += candidate;
        TrimContext();
        Reset();
    }

    private void CancelComposition(ITfContext pic)
    {
        _candidateWin.Hide();
        _comp.RequestEdit(pic, ec => _comp.CancelComposition(ec));
        Reset();
    }

    private void RevertToComposing(ITfContext pic)
    {
        _candidateWin.Hide();
        // Show hiragana again
        _comp.RequestEdit(pic, ec => _comp.SetText(ec, _hiragana));
        _state = ImeState.Composing;
    }

    private void HandleBackspace(ITfContext pic)
    {
        if (_romaji.Pending.Length > 0)
        {
            _romaji.Clear();
            UpdateComposition(pic);
            return;
        }
        if (_hiragana.Length > 0)
        {
            _hiragana = _hiragana[..^1];
            if (_hiragana.Length == 0)
                CancelComposition(pic);
            else
                UpdateComposition(pic);
        }
    }

    private void Reset()
    {
        _hiragana     = "";
        _candidates   = [];
        _candidateIdx = 0;
        _romaji.Clear();
        _state = ImeState.Idle;
    }

    private void TrimContext()
    {
        // Keep last ~200 characters for context
        if (_context.Length > 200)
            _context = _context[^200..];
    }

    private static System.Drawing.Point GetCaretScreenPos()
    {
        // Simplified: get caret position via GetCaretPos + ClientToScreen
        // Falls back to mouse cursor position
        if (GetCaretPos(out POINT pt))
        {
            var hwnd = GetFocus();
            ClientToScreen(hwnd, ref pt);
            return new System.Drawing.Point(pt.X, pt.Y + 20);
        }
        var cur = System.Windows.Forms.Cursor.Position;
        return new System.Drawing.Point(cur.X, cur.Y + 20);
    }
}
