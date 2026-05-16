#include <windows.h>
#include <msctf.h>
#include <stdio.h>

int main(void) {
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    printf("CoInitialize: 0x%08X\n", hr);

    // Create ITfInputProcessorProfiles
    CLSID clsidProfiles = {0x33C53A50, 0xF456, 0x4884, {0xB0, 0x49, 0x85, 0xFD, 0x64, 0x3E, 0xCF, 0xED}};
    IID iidProfiles = {0x1F02B6C5, 0x7842, 0x4EE6, {0x8A, 0x0B, 0x9A, 0x24, 0x18, 0x3A, 0x95, 0xCA}};
    
    ITfInputProcessorProfiles* pProfiles = NULL;
    hr = CoCreateInstance(&clsidProfiles, NULL, CLSCTX_INPROC_SERVER, &iidProfiles, (void**)&pProfiles);
    printf("Create ITfInputProcessorProfiles: 0x%08X\n", hr);
    
    if (FAILED(hr)) { CoUninitialize(); return 1; }
    
    // Try to activate our profile
    CLSID ourClsid = {0xC7E9D1A0, 0xB2F3, 0x4E56, {0xA7, 0x89, 0x0C, 0x1D, 0x2E, 0x3F, 0x4A, 0x5B}};
    GUID ourProfile = {0xD8F0E2B1, 0xC3A4, 0x5F67, {0xB8, 0x90, 0x1D, 0x2E, 0x3F, 0x4A, 0x5B, 0x6C}};
    LANGID langJa = 0x0411;
    
    hr = pProfiles->lpVtbl->ActivateLanguageProfile(pProfiles, &ourClsid, langJa, &ourProfile);
    printf("ActivateLanguageProfile: 0x%08X\n", hr);
    
    // Also check if it's enabled
    BOOL bEnabled = FALSE;
    hr = pProfiles->lpVtbl->IsEnabledLanguageProfile(pProfiles, &ourClsid, langJa, &ourProfile, &bEnabled);
    printf("IsEnabled: 0x%08X  enabled=%d\n", hr, bEnabled);
    
    // Get description
    BSTR bstrDesc = NULL;
    hr = pProfiles->lpVtbl->GetLanguageProfileDescription(pProfiles, &ourClsid, langJa, &ourProfile, &bstrDesc);
    if (SUCCEEDED(hr) && bstrDesc) {
        wprintf(L"Description: %s\n", bstrDesc);
        SysFreeString(bstrDesc);
    } else {
        printf("GetLanguageProfileDescription: 0x%08X\n", hr);
    }
    
    pProfiles->lpVtbl->Release(pProfiles);
    CoUninitialize();
    return 0;
}
