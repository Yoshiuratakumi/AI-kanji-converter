using AiImeShell.Interop;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AiImeShell;

[ComVisible(true)]
[Guid(Guids.TextService)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class AiImeTextService : ITfTextInputProcessor
{
    private static readonly string ModelDir =
        Path.Combine(AppContext.BaseDirectory, "models");

    private ITfThreadMgr?    _threadMgr;
    private int              _clientId;
    private KeyEventSink?    _keySink;
    private CandidateWindow? _candidateWin;
    private Thread?          _uiThread;

    // ── ITfTextInputProcessor ─────────────────────────────────────────────────

    public int Activate(ITfThreadMgr ptim, int tid)
    {
        try
        {
            Log("Activate called");
            _threadMgr = ptim;
            _clientId  = tid;

            // Run model loading on a background thread to avoid blocking the TSF
            // Activate() call. The 546 MB BERT model takes several seconds to load;
            // blocking here hangs every process that has a text-input field.
            Task.Run(() =>
            {
                if (!AiImeCoreBridge.Initialize(ModelDir))
                    Log($"AiImeCore init warning: {AiImeCoreBridge.GetLastError()}");
                else
                    Log("AiImeCore initialized");
            });

            _candidateWin = CreateCandidateWindowOnUiThread();
            Log("CandidateWindow created");

            _keySink = new KeyEventSink(_clientId, _candidateWin);

            var keystrokeMgr = (ITfKeystrokeMgr)ptim;
            keystrokeMgr.AdviseKeyEventSink(_clientId, _keySink, true);
            Log("Activate complete");

            return 0;
        }
        catch (Exception ex)
        {
            Log($"Activate FAILED: {ex}");
            return unchecked((int)0x80004005);
        }
    }

    public int Deactivate()
    {
        try
        {
            Log("Deactivate called");
            if (_threadMgr != null && _keySink != null)
            {
                var keystrokeMgr = (ITfKeystrokeMgr)_threadMgr;
                keystrokeMgr.UnadviseKeyEventSink(_clientId);
            }

            if (_candidateWin != null)
            {
                _candidateWin.BeginInvoke(() =>
                {
                    _candidateWin.Dispose();
                    Application.ExitThread();
                });
            }
            _uiThread?.Join(2000);
            _candidateWin = null;
            _uiThread     = null;
            _keySink      = null;

            AiImeCoreBridge.Shutdown();

            if (_threadMgr != null)
            {
                Marshal.ReleaseComObject(_threadMgr);
                _threadMgr = null;
            }
            Log("Deactivate complete");
        }
        catch (Exception ex)
        {
            Log($"Deactivate error: {ex}");
        }
        return 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CandidateWindow CreateCandidateWindowOnUiThread()
    {
        CandidateWindow? win = null;
        var ready = new ManualResetEventSlim(false);

        _uiThread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                win = new CandidateWindow();
                win.CreateControl();
            }
            finally
            {
                ready.Set();
            }
            Application.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Name = "AiIme UI";
        _uiThread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("CandidateWindow creation timed out");
        if (win == null)
            throw new InvalidOperationException("CandidateWindow creation failed");
        return win;
    }

    internal static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(@"C:\Temp");
            File.AppendAllText(@"C:\Temp\aimime.log",
                $"{DateTime.Now:HH:mm:ss.fff} [pid={Environment.ProcessId}] {msg}\r\n");
        }
        catch { }
    }
}
