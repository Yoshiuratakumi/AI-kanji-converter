using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
#pragma warning disable CA1401, CA2101

namespace AiImeShell.Interop;

// 笏笏 Structs 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

[StructLayout(LayoutKind.Sequential)]
public struct TF_PRESERVEDKEY
{
    public uint uVKey;
    public uint uModifiers;
}

[StructLayout(LayoutKind.Sequential)]
public struct TF_SELECTIONSTYLE
{
    public TfActiveSelEnd ase;
    [MarshalAs(UnmanagedType.Bool)] public bool fInterimChar;
}

[StructLayout(LayoutKind.Sequential)]
public struct TF_SELECTION
{
    [MarshalAs(UnmanagedType.Interface)] public ITfRange range;
    public TF_SELECTIONSTYLE style;
}

[StructLayout(LayoutKind.Sequential)]
public struct TF_STATUS
{
    public uint dwDynamicFlags;
    public uint dwStaticFlags;
}

public enum TfActiveSelEnd { TF_AE_NONE = 0, TF_AE_START = 1, TF_AE_END = 2 }

// 笏笏 Constants 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

public static class TsfConst
{
    public const uint TF_ES_ASYNCDONTCARE = 0x0;
    public const uint TF_ES_SYNC         = 0x1;
    public const uint TF_ES_READ         = 0x2;
    public const uint TF_ES_READWRITE    = 0x6;

    public const uint TF_IAS_NOQUERY    = 0x1;
    public const uint TF_IAS_QUERYONLY  = 0x2;

    public const uint TF_DEFAULT_SELECTION = 0xFFFFFFFF;
    public const int  TF_ANCHOR_START = 0;
    public const int  TF_ANCHOR_END   = 1;

    // RequestEditSession success flag
    public const int S_OK    = 0;
    public const int S_FALSE = 1;
}

// 笏笏 Thread Manager 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E801-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E801-2021-11D2-93E0-0060B067B86E")]
public interface ITfThreadMgr
{
    [PreserveSig] int Activate(out int ptid);
    [PreserveSig] int Deactivate();
    [PreserveSig] int CreateDocumentMgr(out ITfDocumentMgr ppdim);
    [PreserveSig] int EnumDocumentMgrs([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int GetFocus(out ITfDocumentMgr? ppdimFocus);
    [PreserveSig] int SetFocus([In] ITfDocumentMgr pdimFocus);
    [PreserveSig] int AssociateFocus([In] IntPtr hwnd,
                                     [In] ITfDocumentMgr? pdimNew,
                                     out ITfDocumentMgr? ppdimPrev);
    [PreserveSig] int IsThreadFocus([MarshalAs(UnmanagedType.Bool)] out bool pfThreadFocus);
    [PreserveSig] int GetFunctionProvider(in Guid clsid,
                                          [MarshalAs(UnmanagedType.IUnknown)] out object ppFuncProv);
    [PreserveSig] int EnumFunctionProviders([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int GetGlobalCompartment([MarshalAs(UnmanagedType.IUnknown)] out object ppCompMgr);
}

// 笏笏 Keystroke Manager 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E7F0-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E7F0-2021-11D2-93E0-0060B067B86E")]
public interface ITfKeystrokeMgr
{
    [PreserveSig] int AdviseKeyEventSink(
        [In] int tid,
        [In, MarshalAs(UnmanagedType.Interface)] ITfKeyEventSink pSink,
        [MarshalAs(UnmanagedType.Bool)] bool fForeground);
    [PreserveSig] int UnadviseKeyEventSink([In] int tid);
    [PreserveSig] int GetForegroundKeyEventSink(out int ptid,
        [MarshalAs(UnmanagedType.Interface)] out ITfKeyEventSink? ppSink);
    [PreserveSig] int GetFocusedKeystrokeManager([MarshalAs(UnmanagedType.IUnknown)] out object ppks);
    [PreserveSig] int IsPreservedKey(in Guid rguid,
        [MarshalAs(UnmanagedType.Bool)] out bool pfRegistered);
    [PreserveSig] int PreserveKey([In] int tid, in Guid rguid, in TF_PRESERVEDKEY prekey,
        [MarshalAs(UnmanagedType.LPWStr)] string pchDesc, [In] uint cchDesc);
    [PreserveSig] int UnpreserveKey(in Guid rguid, in TF_PRESERVEDKEY prekey);
    [PreserveSig] int SetPreservedKeyDescription(in Guid rguid,
        [MarshalAs(UnmanagedType.LPWStr)] string pchDesc, [In] uint cchDesc);
    [PreserveSig] int GetPreservedKeyDescription(in Guid rguid,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrDesc);
    [PreserveSig] int SimulatePreservedKey([In] ITfContext pic, in Guid rguid,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEaten);
}

// 笏笏 Text Input Processor 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E7F7-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E7F7-2021-11D2-93E0-0060B067B86E")]
public interface ITfTextInputProcessor
{
    [PreserveSig] int Activate([In] ITfThreadMgr ptim, [In] int tid);
    [PreserveSig] int Deactivate();
}

// 笏笏 Key Event Sink (we implement this) 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E7F5-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E7F5-2021-11D2-93E0-0060B067B86E")]
public interface ITfKeyEventSink
{
    [PreserveSig] int OnSetFocus([MarshalAs(UnmanagedType.Bool)] bool fForeground);
    [PreserveSig] int OnTestKeyDown([In] ITfContext pic, [In] IntPtr wParam, [In] IntPtr lParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEaten);
    [PreserveSig] int OnKeyDown([In] ITfContext pic, [In] IntPtr wParam, [In] IntPtr lParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEaten);
    [PreserveSig] int OnTestKeyUp([In] ITfContext pic, [In] IntPtr wParam, [In] IntPtr lParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEaten);
    [PreserveSig] int OnKeyUp([In] ITfContext pic, [In] IntPtr wParam, [In] IntPtr lParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEaten);
    [PreserveSig] int OnPreservedKey([In] ITfContext pic, in Guid rguid,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEaten);
}

// 笏笏 Document Manager 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E7F4-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E7F4-2021-11D2-93E0-0060B067B86E")]
public interface ITfDocumentMgr
{
    [PreserveSig] int CreateContext([In] int tid, [In] uint dwFlags,
        [MarshalAs(UnmanagedType.IUnknown)] object? punk,
        out ITfContext ppic, out int pecTextStore);
    [PreserveSig] int Push([In] ITfContext pic);
    [PreserveSig] int Pop([In] uint dwFlags);
    [PreserveSig] int GetTop(out ITfContext? ppic);
    [PreserveSig] int GetBase(out ITfContext? ppic);
    [PreserveSig] int EnumContexts([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
}

// 笏笏 Context 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E7FD-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E7FD-2021-11D2-93E0-0060B067B86E")]
public interface ITfContext
{
    [PreserveSig] int RequestEditSession([In] int tid,
        [In, MarshalAs(UnmanagedType.Interface)] ITfEditSession pes,
        [In] uint dwFlags, out int phrSession);
    [PreserveSig] int InWriteSession([In] int tid,
        [MarshalAs(UnmanagedType.Bool)] out bool pfWriteSession);
    [PreserveSig] int GetSelection([In] int ec, [In] uint ulIndex, [In] uint ulCount,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] TF_SELECTION[] pSelection,
        out uint pcFetched);
    [PreserveSig] int SetSelection([In] int ec, [In] uint ulCount,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] TF_SELECTION[] pSelection);
    [PreserveSig] int GetStart([In] int ec, out ITfRange ppStart);
    [PreserveSig] int GetEnd([In] int ec, out ITfRange ppEnd);
    [PreserveSig] int GetActiveView([MarshalAs(UnmanagedType.IUnknown)] out object ppView);
    [PreserveSig] int EnumViews([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int GetStatus(out TF_STATUS pdcs);
    [PreserveSig] int GetProperty(in Guid guidProp,
        [MarshalAs(UnmanagedType.Interface)] out ITfProperty ppProp);
    [PreserveSig] int GetAppProperty(in Guid guidProp,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppProp);
    [PreserveSig] int TrackProperties(
        [In, MarshalAs(UnmanagedType.LPArray)] Guid[] prgProp, [In] uint cProp,
        [In, MarshalAs(UnmanagedType.LPArray)] Guid[]? prgAppProp, [In] uint cAppProp,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppProperty);
    [PreserveSig] int EnumProperties([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int GetDocumentMgr(out ITfDocumentMgr ppdm);
    [PreserveSig] int CreateRangeBackup([In] int ec, [In] ITfRange pRange,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppBackup);
}

// 笏笏 Edit Session (we implement this) 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E803-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E803-2021-11D2-93E0-0060B067B86E")]
public interface ITfEditSession
{
    [PreserveSig] int DoEditSession([In] int ec);
}

// 笏笏 Insert at Selection 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {55CE16BA-3014-41C1-9CEB-FADE1446AC6C}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("55CE16BA-3014-41C1-9CEB-FADE1446AC6C")]
public interface ITfInsertAtSelection
{
    [PreserveSig] int InsertTextAtSelection([In] int ec, [In] uint dwFlags,
        [MarshalAs(UnmanagedType.LPWStr)] string pchText, [In] int cch,
        out ITfRange ppRange);
    [PreserveSig] int InsertEmbeddedAtSelection([In] int ec, [In] uint dwFlags,
        [MarshalAs(UnmanagedType.IUnknown)] object pDataObject,
        out ITfRange ppRange);
}

// 笏笏 Context Composition 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B")]
public interface ITfContextComposition
{
    [PreserveSig] int StartComposition([In] int ec, [In] ITfRange pCompositionRange,
        [In, MarshalAs(UnmanagedType.Interface)] ITfCompositionSink pSink,
        out ITfComposition ppComposition);
    [PreserveSig] int EnumCompositions([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int FindComposition([In] int ec, [In] ITfRange pTestRange,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int TakeOwnership([In] int ec,
        [MarshalAs(UnmanagedType.Interface)] object pComposition,
        [In, MarshalAs(UnmanagedType.Interface)] ITfCompositionSink pSink,
        out ITfComposition ppComposition);
}

// 笏笏 Composition 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {20168D64-5A8F-4A5A-B7BD-CFA29F4D0FD9}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("20168D64-5A8F-4A5A-B7BD-CFA29F4D0FD9")]
public interface ITfComposition
{
    [PreserveSig] int GetRange(out ITfRange ppRange);
    [PreserveSig] int ShiftStart([In] int ec, [In] ITfRange pNewStart);
    [PreserveSig] int ShiftEnd([In] int ec, [In] ITfRange pNewEnd);
    [PreserveSig] int EndComposition([In] int ec);
}

// 笏笏 Composition Sink (we implement this) 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {A781718C-579A-4B15-A280-32B8577ACC5E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("A781718C-579A-4B15-A280-32B8577ACC5E")]
public interface ITfCompositionSink
{
    [PreserveSig] int OnCompositionTerminated([In] int ecWrite, [In] ITfComposition pComposition);
}

// 笏笏 Range 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {AA80E7FF-2021-11D2-93E0-0060B067B86E}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E7FF-2021-11D2-93E0-0060B067B86E")]
public interface ITfRange
{
    [PreserveSig] int GetText([In] int ec, [In] uint dwFlags,
        [Out, MarshalAs(UnmanagedType.LPWStr)] out string pchText,
        [In] uint cchMax, out uint pcch);
    [PreserveSig] int SetText([In] int ec, [In] uint dwFlags,
        [MarshalAs(UnmanagedType.LPWStr)] string pchText, [In] int cch);
    [PreserveSig] int GetFormattedText([In] int ec,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppDataObject);
    [PreserveSig] int GetEmbedded([In] int ec, in Guid rguidService, in Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
    [PreserveSig] int InsertEmbedded([In] int ec, [In] uint dwFlags,
        [MarshalAs(UnmanagedType.IUnknown)] object pDataObject);
    [PreserveSig] int ShiftStart([In] int ec, [In] int cchReq, out int pcch,
        [In] IntPtr pHalt); // TF_HALTCOND*
    [PreserveSig] int ShiftEnd([In] int ec, [In] int cchReq, out int pcch,
        [In] IntPtr pHalt);
    [PreserveSig] int ShiftStartToRange([In] int ec, [In] ITfRange pRange, [In] int aPos);
    [PreserveSig] int ShiftEndToRange([In] int ec, [In] ITfRange pRange, [In] int aPos);
    [PreserveSig] int ShiftStartRegion([In] int ec, [In] int dir,
        [MarshalAs(UnmanagedType.Bool)] out bool pfNoRegion);
    [PreserveSig] int ShiftEndRegion([In] int ec, [In] int dir,
        [MarshalAs(UnmanagedType.Bool)] out bool pfNoRegion);
    [PreserveSig] int IsEmpty([In] int ec,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEmpty);
    [PreserveSig] int Collapse([In] int ec, [In] int aPos);
    [PreserveSig] int IsEqualStart([In] int ec, [In] ITfRange pWith, [In] int aPos,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEqual);
    [PreserveSig] int IsEqualEnd([In] int ec, [In] ITfRange pWith, [In] int aPos,
        [MarshalAs(UnmanagedType.Bool)] out bool pfEqual);
    [PreserveSig] int CompareStart([In] int ec, [In] ITfRange pWith, [In] int aPos,
        out int plResult);
    [PreserveSig] int CompareEnd([In] int ec, [In] ITfRange pWith, [In] int aPos,
        out int plResult);
    [PreserveSig] int AdjustForInsert([In] int ec, [In] uint cchInsert,
        [MarshalAs(UnmanagedType.Bool)] out bool pfInsertOk);
    [PreserveSig] int GetGravity(out uint pgStart, out uint pgEnd);
    [PreserveSig] int SetGravity([In] int ec, [In] uint gStart, [In] uint gEnd);
    [PreserveSig] int Clone(out ITfRange ppClone);
    [PreserveSig] int GetContext(out ITfContext ppContext);
}

// 笏笏 Property (for display attributes) 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {E2449660-9542-11D2-BF46-00105A2799B5}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("E2449660-9542-11D2-BF46-00105A2799B5")]
public interface ITfProperty
{
    // ITfReadOnlyProperty methods (vtable slots 0-5 after IUnknown)
    [PreserveSig] int GetType(out Guid pguid);
    [PreserveSig] int EnumRanges([In] int ec,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppEnum,
        [In] ITfRange? pTargetRange);
    [PreserveSig] int GetValue([In] int ec, [In] ITfRange pRange,
        [MarshalAs(UnmanagedType.Struct)] out object pvarValue);
    [PreserveSig] int GetContext(out ITfContext ppContext);
    // ITfProperty methods
    [PreserveSig] int FindRange([In] int ec, [In] ITfRange pRange,
        out ITfRange ppRange, [In] int aPos);
    [PreserveSig] int SetValueStore([In] int ec, [In] ITfRange pRange,
        [MarshalAs(UnmanagedType.IUnknown)] object pPropStore);
    [PreserveSig] int SetValue([In] int ec, [In] ITfRange pRange,
        [MarshalAs(UnmanagedType.Struct)] in object pvarValue);
    [PreserveSig] int Clear([In] int ec, [In] ITfRange pRange);
}

// 笏笏 Input Processor Profiles (for registration) 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// IID: {1F02B6C5-7842-4EE6-8A0B-9A24183A95CA}  (ITfInputProcessorProfiles)
// CLSID: {33C53A50-F456-4884-B049-85FD643ECFED} (TF_InputProcessorProfiles)

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
public interface ITfInputProcessorProfiles
{
    [PreserveSig] int Register(in Guid rclsid);
    [PreserveSig] int Unregister(in Guid rclsid);
    [PreserveSig] int AddLanguageProfile(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile,
        [MarshalAs(UnmanagedType.LPWStr)] string pchDesc, [In] uint cchDesc,
        [MarshalAs(UnmanagedType.LPWStr)] string pchIconFile, [In] uint cchFile,
        [In] uint uIconIndex);
    [PreserveSig] int RemoveLanguageProfile(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile);
    [PreserveSig] int EnumInputProcessorInfo(
        [MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int GetDefaultLanguageProfile([In] ushort langid, in Guid catid,
        out Guid pclsid, out Guid pguidProfile);
    [PreserveSig] int SetDefaultLanguageProfile([In] ushort langid, in Guid rclsid,
        in Guid guidProfiles);
    [PreserveSig] int ActivateLanguageProfile(in Guid rclsid, [In] ushort langid,
        in Guid guidProfiles);
    [PreserveSig] int GetActiveLanguageProfile(in Guid rclsid, [In] ushort langid,
        out Guid pguidProfile);
    [PreserveSig] int GetLanguageProfileDescription(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrProfile);
    [PreserveSig] int GetCurrentLanguage(out ushort plangid);
    [PreserveSig] int ChangeCurrentLanguage([In] ushort langid);
    [PreserveSig] int GetLanguageList(out IntPtr ppLangId, out uint pulCount);
    [PreserveSig] int EnumLanguageProfiles([In] ushort langid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
    [PreserveSig] int EnableLanguageProfile(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile, [MarshalAs(UnmanagedType.Bool)] bool fEnable);
    [PreserveSig] int IsEnabledLanguageProfile(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile, [MarshalAs(UnmanagedType.Bool)] out bool pfEnable);
    [PreserveSig] int EnableLanguageProfileByDefault(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile, [MarshalAs(UnmanagedType.Bool)] bool fEnable);
    [PreserveSig] int SubstituteKeyboardLayout(in Guid rclsid, [In] ushort langid,
        in Guid guidProfile, [In] IntPtr hKL);
}
