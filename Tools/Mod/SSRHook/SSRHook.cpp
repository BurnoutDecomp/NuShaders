// SSRHook feeds scene depth (t8) + scene colour (t7) to the NuShaders SSR
// pixel shader (Specular_1Bit_Doublesided_Parallax.fx + D_SSR)

#include <windows.h>
#include <d3d11.h>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <cstdarg>

#pragma comment(lib, "d3d11.lib")

static const UINT SSR_DEPTH_SLOT = 8, SSR_COLOUR_SLOT = 7;

static void Log(const char *fmt, ...)
{
    char buf[512];
    va_list a;
    va_start(a, fmt);
    vsnprintf(buf, sizeof(buf), fmt, a);
    va_end(a);
    OutputDebugStringA(buf);
    HANDLE h = GetStdHandle(STD_OUTPUT_HANDLE);
    if (h && h != INVALID_HANDLE_VALUE)
    {
        DWORD w = 0;
        WriteFile(h, buf, (DWORD)strlen(buf), &w, nullptr);
    }
}

// ---------------------------------------------------------------------------
// Our SSR PS DXBC checksum from SSRHook.ini
static uint8_t kSum[16] = {0};
static bool g_haveSum = false;

static void LoadChecksum()
{
    char path[MAX_PATH] = {0};
    HMODULE hm = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&LoadChecksum), &hm);
    if (!GetModuleFileNameA(hm, path, MAX_PATH))
        return;
    char *slash = strrchr(path, '\\');
    if (!slash)
        return;
    strcpy(slash + 1, "SSRHook.ini");
    FILE *f = fopen(path, "r");
    if (!f)
        return;
    auto hx = [](char c) -> int
    { if(c>='0'&&c<='9')return c-'0'; if(c>='a'&&c<='f')return c-'a'+10; if(c>='A'&&c<='F')return c-'A'+10; return -1; };
    char line[256];
    while (fgets(line, sizeof(line), f))
    {
        char *p = strstr(line, "pixel_shader_checksum");
        if (!p)
            continue;
        p = strchr(p, '=');
        if (!p)
            continue;
        ++p;
        while (*p == ' ' || *p == '\t')
            ++p;
        int n = 0;
        while (n < 16 && p[0] && p[1])
        {
            int hi = hx(p[0]), lo = hx(p[1]);
            if (hi < 0 || lo < 0)
                break;
            kSum[n++] = (uint8_t)((hi << 4) | lo);
            p += 2;
        }
        g_haveSum = (n == 16);
        break;
    }
    fclose(f);
}

// ---------------------------------------------------------------------------
// Runtime state
static ID3D11PixelShader *g_ourPs = nullptr;
static bool g_ourPsBound = false;
static ID3D11Resource *g_depthRes = nullptr;
static ID3D11ShaderResourceView *g_depthSrv = nullptr;
static ID3D11ShaderResourceView *g_colourSrv = nullptr;

static DXGI_FORMAT DepthSrvFormat(DXGI_FORMAT res)
{
    switch (res)
    {
    case DXGI_FORMAT_R32_TYPELESS:
        return DXGI_FORMAT_R32_FLOAT;
    case DXGI_FORMAT_R24G8_TYPELESS:
        return DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
    case DXGI_FORMAT_R16_TYPELESS:
        return DXGI_FORMAT_R16_UNORM;
    case DXGI_FORMAT_D32_FLOAT:
        return DXGI_FORMAT_R32_FLOAT;
    case DXGI_FORMAT_D24_UNORM_S8_UINT:
        return DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
    default:
        return res;
    }
}

// ---------------------------------------------------------------------------
typedef HRESULT(STDMETHODCALLTYPE *PFN_CreatePixelShader)(ID3D11Device *, const void *, SIZE_T, ID3D11ClassLinkage *, ID3D11PixelShader **);
typedef void(STDMETHODCALLTYPE *PFN_PSSetShader)(ID3D11DeviceContext *, ID3D11PixelShader *, ID3D11ClassInstance *const *, UINT);
typedef void(STDMETHODCALLTYPE *PFN_OMSetRenderTargets)(ID3D11DeviceContext *, UINT, ID3D11RenderTargetView *const *, ID3D11DepthStencilView *);
typedef void(STDMETHODCALLTYPE *PFN_DrawIndexed)(ID3D11DeviceContext *, UINT, UINT, INT);
typedef void(STDMETHODCALLTYPE *PFN_DrawIndexedInstanced)(ID3D11DeviceContext *, UINT, UINT, UINT, INT, UINT);

static PFN_CreatePixelShader orig_CreatePixelShader = nullptr;
static PFN_PSSetShader orig_PSSetShader = nullptr;
static PFN_OMSetRenderTargets orig_OMSetRenderTargets = nullptr;
static PFN_DrawIndexed orig_DrawIndexed = nullptr;
static PFN_DrawIndexedInstanced orig_DrawIndexedInstanced = nullptr;

static HRESULT STDMETHODCALLTYPE Hooked_CreatePixelShader(
    ID3D11Device *dev, const void *bc, SIZE_T len, ID3D11ClassLinkage *link, ID3D11PixelShader **out)
{
    HRESULT hr = orig_CreatePixelShader(dev, bc, len, link, out);
    if (SUCCEEDED(hr) && out && *out && g_haveSum && bc && len > 20 &&
        memcmp(reinterpret_cast<const uint8_t *>(bc) + 4, kSum, 16) == 0)
    {
        g_ourPs = *out;
        Log("[SSRHook] recognized SSR pixel shader %p\n", (void *)g_ourPs);
    }
    return hr;
}

static void STDMETHODCALLTYPE Hooked_PSSetShader(
    ID3D11DeviceContext *ctx, ID3D11PixelShader *ps, ID3D11ClassInstance *const *ci, UINT n)
{
    g_ourPsBound = (ps != nullptr && ps == g_ourPs);
    orig_PSSetShader(ctx, ps, ci, n);
}

static void STDMETHODCALLTYPE Hooked_OMSetRenderTargets(
    ID3D11DeviceContext *ctx, UINT n, ID3D11RenderTargetView *const *rtvs, ID3D11DepthStencilView *dsv)
{
    if (dsv)
    {
        ID3D11Resource *res = nullptr;
        dsv->GetResource(&res);
        if (res && res != g_depthRes)
        {
            if (g_depthSrv)
            {
                g_depthSrv->Release();
                g_depthSrv = nullptr;
            }
            if (g_depthRes)
            {
                g_depthRes->Release();
                g_depthRes = nullptr;
            }
            g_depthRes = res;
            g_depthRes->AddRef();
            ID3D11Texture2D *tex = nullptr;
            if (SUCCEEDED(res->QueryInterface(__uuidof(ID3D11Texture2D), (void **)&tex)) && tex)
            {
                D3D11_TEXTURE2D_DESC td;
                tex->GetDesc(&td);
                ID3D11Device *dev = nullptr;
                ctx->GetDevice(&dev);
                if (dev)
                {
                    D3D11_SHADER_RESOURCE_VIEW_DESC sd;
                    ZeroMemory(&sd, sizeof(sd));
                    sd.Format = DepthSrvFormat(td.Format);
                    sd.ViewDimension = (td.SampleDesc.Count > 1) ? D3D11_SRV_DIMENSION_TEXTURE2DMS : D3D11_SRV_DIMENSION_TEXTURE2D;
                    sd.Texture2D.MipLevels = 1;
                    HRESULT shr = dev->CreateShaderResourceView(res, &sd, &g_depthSrv);
                    Log("[SSRHook] depth %ux%u fmt=%d ms=%u SRV=%s\n", td.Width, td.Height,
                        (int)td.Format, td.SampleDesc.Count, SUCCEEDED(shr) ? "ok" : "FAILED");
                    dev->Release();
                }
                tex->Release();
            }
        }
        if (res)
            res->Release();
    }
    // TODO(runtime): capture a scene-colour copy for t7 (half-res samplersource, or CopyResource the colour RT).
    orig_OMSetRenderTargets(ctx, n, rtvs, dsv);
}

static inline void BindSsrInputs(ID3D11DeviceContext *ctx)
{
    if (!g_ourPsBound)
        return;
    if (g_depthSrv)
        ctx->PSSetShaderResources(SSR_DEPTH_SLOT, 1, &g_depthSrv);
    if (g_colourSrv)
        ctx->PSSetShaderResources(SSR_COLOUR_SLOT, 1, &g_colourSrv);
    static bool logged = false;
    if (!logged)
    {
        logged = true;
        Log("[SSRHook] bound SSR inputs for our draw (depth=%p colour=%p)\n", (void *)g_depthSrv, (void *)g_colourSrv);
    }
}
static void STDMETHODCALLTYPE Hooked_DrawIndexed(ID3D11DeviceContext *ctx, UINT c, UINT s, INT b)
{
    BindSsrInputs(ctx);
    orig_DrawIndexed(ctx, c, s, b);
}
static void STDMETHODCALLTYPE Hooked_DrawIndexedInstanced(ID3D11DeviceContext *ctx, UINT cpi, UINT ic, UINT sl, INT bv, UINT si)
{
    BindSsrInputs(ctx);
    orig_DrawIndexedInstanced(ctx, cpi, ic, sl, bv, si);
}

// ---------------------------------------------------------------------------
// VMT hooking - overwrite a vtable slot, save the original.

// D3D11 vtable indices: 
// Device::CreatePixelShader=15
// Context::PSSetShader=9
// DrawIndexed=12
// DrawIndexedInstanced=20
// OMSetRenderTargets=33

template <typename T>
static void VtSwap(void **vtbl, int idx, T detour, T *orig)
{
    DWORD oldProt = 0;
    VirtualProtect(&vtbl[idx], sizeof(void *), PAGE_EXECUTE_READWRITE, &oldProt);
    *orig = reinterpret_cast<T>(vtbl[idx]);
    vtbl[idx] = reinterpret_cast<void *>(detour);
    VirtualProtect(&vtbl[idx], sizeof(void *), oldProt, &oldProt);
}

static bool g_hooked = false;

// ---------------------------------------------------------------------------
// Init
static void InitHooks()
{
    LoadChecksum();
    ID3D11Device *dev = nullptr;
    ID3D11DeviceContext *ctx = nullptr;
    D3D_FEATURE_LEVEL fl{};
    HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0,
                                   D3D11_SDK_VERSION, &dev, &fl, &ctx);
    if (FAILED(hr) || !dev || !ctx)
    {
        Log("[SSRHook] dummy device failed 0x%08X\n", hr);
        return;
    }
    void **dv = *reinterpret_cast<void ***>(dev);
    void **cv = *reinterpret_cast<void ***>(ctx);
    VtSwap(dv, 15, Hooked_CreatePixelShader, &orig_CreatePixelShader);
    VtSwap(cv, 9, Hooked_PSSetShader, &orig_PSSetShader);
    VtSwap(cv, 12, Hooked_DrawIndexed, &orig_DrawIndexed);
    VtSwap(cv, 20, Hooked_DrawIndexedInstanced, &orig_DrawIndexedInstanced);
    VtSwap(cv, 33, Hooked_OMSetRenderTargets, &orig_OMSetRenderTargets);
    g_hooked = true;
    Log("[SSRHook] injected; vtable hooks installed (checksum %s)\n", g_haveSum ? "set" : "MISSING SSRHook.ini");
    ctx->Release();
    dev->Release();
}

static DWORD WINAPI InitThread(LPVOID)
{
    InitHooks();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(h);
        CreateThread(nullptr, 0, InitThread, nullptr, 0, nullptr); // explicit WINAPI proc (x86-safe)
    }
    return TRUE;
}
