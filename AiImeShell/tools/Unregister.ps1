#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Removes AiImeShell from the system.
#>
$ErrorActionPreference = "Stop"

$CLSID  = "{C7E9D1A0-B2F3-4E56-A789-0C1D2E3F4A5B}"
$LangJa = 0x0411

# Remove COM registration
Remove-Item -Path "HKLM:\SOFTWARE\Classes\CLSID\$CLSID" -Recurse -Force -ErrorAction SilentlyContinue

# Remove TSF registration
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\CTF\TIP\$CLSID" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "AiImeShell unregistered."
