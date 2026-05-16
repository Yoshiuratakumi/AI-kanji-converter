#include <windows.h>
#include <objbase.h>
#include <stdio.h>

int main(void) {
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    printf("CoInitializeEx: 0x%08X\n", hr);

    CLSID clsid;
    CLSIDFromString(L"{C7E9D1A0-B2F3-4E56-A789-0C1D2E3F4A5B}", &clsid);

    IUnknown* pUnk = NULL;
    hr = CoCreateInstance(&clsid, NULL, CLSCTX_INPROC_SERVER, &IID_IUnknown, (void**)&pUnk);
    printf("CoCreateInstance: 0x%08X\n", hr);

    if (SUCCEEDED(hr)) {
        printf("SUCCESS - COM object created\n");
        pUnk->lpVtbl->Release(pUnk);
    } else {
        LPWSTR msg = NULL;
        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER|FORMAT_MESSAGE_FROM_SYSTEM,
                       NULL, hr, 0, (LPWSTR)&msg, 0, NULL);
        if (msg) { wprintf(L"Error: %s\n", msg); LocalFree(msg); }
    }
    CoUninitialize();
    return 0;
}
