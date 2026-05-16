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
        Path.Combine(
            Path.GetDirectoryName(typeof(AiImeTextService).Assembly.Location)
                ?? AppContext.BaseDirectory,
            "models");

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

            // Load the SKK dictionary on a background thread (6 MB, ~120K entries;
            // takes ~300 ms). No native DLLs are loaded — pure C# parsing.
            Task.Run(() =>
            {
                try
                {
                    string dictPath = Path.Combine(ModelDir, "dict.skk");
                    SkkDictionary.Load(dictPath);
                }
                catch (Exception ex)
                {
                    Log($"SkkDictionary load exception: {ex}");
                }
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

    private CandidateWindow? CreateCandidateWindowOnUiThread()
    {
        CandidateWindow? win = null;
        var ready = new ManualResetEventSlim(false);

        _uiThread = new Thread(() =>
        {
            bool created = false;
            try
            {
                // Do NOT call Application.EnableVisualStyles() or
                // Application.SetCompatibleTextRenderingDefault() here —
                // they throw InvalidOperationException in host processes
                // (Word, Excel, Notepad) that already own window handles.
                win = new CandidateWindow();
                win.CreateControl();
                created = true;
            }
            catch (Exception ex)
            {
                Log($"CandidateWindow creation error: {ex.Message}");
            }
            finally
            {
                ready.Set();
            }
            if (!created) return;
            try
            {
                Application.Run();
            }
            catch (Exception ex)
            {
                Log($"UI thread error: {ex.Message}");
            }
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Name = "AiIme UI";
        _uiThread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(5)))
            Log("CandidateWindow creation timed out — IME will run without candidate window");
        if (win == null)
            Log("CandidateWindow is null — IME will run without candidate window");
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
