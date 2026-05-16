#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Registers AiImeShell as a TSF Text Input Processor (TIP) for Japanese input.

.DESCRIPTION
    1. Registers the COM server (comhost.dll) under HKLM\SOFTWARE\Classes\CLSID
    2. Registers the language profile with TSF via ITfInputProcessorProfiles

.PARAMETER DllPath
    Full path to AiImeShell.comhost.dll (default: sibling of this script's parent)
#>
param(
    [string]$DllPath = ""
)

$ErrorActionPreference = "Stop"

# ── Locate the DLL ────────────────────────────────────────────────────────────
if (-not $DllPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectDir = Split-Path -Parent $scriptDir
    $DllPath = Join-Path $projectDir "bin\Release\net8.0-windows\publish\AiImeShell.comhost.dll"
}

if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found: $DllPath`nRun: dotnet publish -c Release first."
    exit 1
}

$DllPath = (Resolve-Path $DllPath).Path
Write-Host "Registering: $DllPath"

# ── GUIDs ─────────────────────────────────────────────────────────────────────
$CLSID       = "{C7E9D1A0-B2F3-4E56-A789-0C1D2E3F4A5B}"
$LangProfile = "{D8F0E2B1-C3A4-5F67-B890-1D2E3F4A5B6C}"
$LangJa      = 0x0411
$ImeName     = "AI日本語IME"

# ── 1. COM server registration ────────────────────────────────────────────────
$clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$CLSID"
New-Item -Path $clsidKey -Force | Out-Null
Set-ItemProperty -Path $clsidKey -Name "(Default)" -Value $ImeName

$inprocKey = "$clsidKey\InProcServer32"
New-Item -Path $inprocKey -Force | Out-Null
Set-ItemProperty -Path $inprocKey -Name "(Default)"      -Value $DllPath
Set-ItemProperty -Path $inprocKey -Name "ThreadingModel" -Value "Apartment"

Write-Host "  COM server registered under HKLM\SOFTWARE\Classes\CLSID\$CLSID"

# ── 2. TSF TIP registration via ITfInputProcessorProfiles ────────────────────
$regScript = @"
using System;
using System.Runtime.InteropServices;

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
 Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
interface ITfInputProcessorProfiles {
    int Register(ref Guid rclsid);
    int Unregister(ref Guid rclsid);
    int AddLanguageProfile(ref Guid rclsid, ushort langid, ref Guid guidProfile,
        [MarshalAs(UnmanagedType.LPWStr)] string pchDesc, uint cchDesc,
        [MarshalAs(UnmanagedType.LPWStr)] string pchIconFile, uint cchFile, uint uIconIndex);
    // remaining methods omitted
}

class Reg {
    static readonly Guid CLSID_Profiles = new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
    static readonly Guid IID_Profiles   = new Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA");

    public static void Run(string clsidStr, string profileStr, ushort langid, string name) {
        Type t = Type.GetTypeFromCLSID(CLSID_Profiles)!;
        var profiles = (ITfInputProcessorProfiles)Activator.CreateInstance(t)!;
        Guid clsid   = new Guid(clsidStr);
        Guid profile = new Guid(profileStr);
        profiles.Register(ref clsid);
        profiles.AddLanguageProfile(ref clsid, langid, ref profile,
            name, (uint)name.Length, "", 0, 0);
        Marshal.ReleaseComObject(profiles);
        Console.WriteLine("TSF language profile registered.");
    }
}
"@

# Use the registry fallback (more reliable across Windows versions)
# HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\LanguageProfile\0x0411\{LangProfile}

$tipRoot = "HKLM:\SOFTWARE\Microsoft\CTF\TIP\$CLSID\LanguageProfile\0x$('{0:x4}' -f $LangJa)\$LangProfile"
New-Item -Path $tipRoot -Force | Out-Null
Set-ItemProperty -Path $tipRoot -Name "Description" -Value $ImeName
Set-ItemProperty -Path $tipRoot -Name "Display"     -Value 0
Set-ItemProperty -Path $tipRoot -Name "Enable"      -Value 1
Set-ItemProperty -Path $tipRoot -Name "IconFile"    -Value ""
Set-ItemProperty -Path $tipRoot -Name "IconIndex"   -Value 0

Write-Host "  TSF language profile registered under CTF\TIP"
Write-Host ""
Write-Host "Done. Log off and log on again (or restart explorer) to load the IME."
Write-Host "Use 'Win+Space' or the language bar to switch to '$ImeName'."
