#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>
#include <d3d9.h>
#include <tlhelp32.h>
#include <intrin.h>
#include <cstdarg>
#include <cstdio>
#include <cstring>

namespace
{
    HANDLE g_log = INVALID_HANDLE_VALUE;
    volatile LONG g_logging = 0;

    using CreateFileAFn = HANDLE(WINAPI*)(
        LPCSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
    using CreateFileWFn = HANDLE(WINAPI*)(
        LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
    using WaitNamedPipeAFn = BOOL(WINAPI*)(LPCSTR, DWORD);
    using WaitNamedPipeWFn = BOOL(WINAPI*)(LPCWSTR, DWORD);
    using WaitForSingleObjectFn = DWORD(WINAPI*)(HANDLE, DWORD);
    using WaitForMultipleObjectsFn = DWORD(WINAPI*)(
        DWORD, const HANDLE*, BOOL, DWORD);
    using SleepFn = void(WINAPI*)(DWORD);
    using CreateEventAFn = HANDLE(WINAPI*)(
        LPSECURITY_ATTRIBUTES, BOOL, BOOL, LPCSTR);
    using CreateEventWFn = HANDLE(WINAPI*)(
        LPSECURITY_ATTRIBUTES, BOOL, BOOL, LPCWSTR);
    using CreateMutexAFn = HANDLE(WINAPI*)(
        LPSECURITY_ATTRIBUTES, BOOL, LPCSTR);
    using CreateMutexWFn = HANDLE(WINAPI*)(
        LPSECURITY_ATTRIBUTES, BOOL, LPCWSTR);
    using CreateNamedPipeAFn = HANDLE(WINAPI*)(
        LPCSTR, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD,
        LPSECURITY_ATTRIBUTES);
    using CreateNamedPipeWFn = HANDLE(WINAPI*)(
        LPCWSTR, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD,
        LPSECURITY_ATTRIBUTES);
    using ConnectNamedPipeFn = BOOL(WINAPI*)(HANDLE, LPOVERLAPPED);
    using CreateFileMappingAFn = HANDLE(WINAPI*)(
        HANDLE, LPSECURITY_ATTRIBUTES, DWORD, DWORD, DWORD, LPCSTR);
    using CreateFileMappingWFn = HANDLE(WINAPI*)(
        HANDLE, LPSECURITY_ATTRIBUTES, DWORD, DWORD, DWORD, LPCWSTR);
    using LoadLibraryAFn = HMODULE(WINAPI*)(LPCSTR);
    using LoadLibraryWFn = HMODULE(WINAPI*)(LPCWSTR);
    using CreateThreadFn = HANDLE(WINAPI*)(
        LPSECURITY_ATTRIBUTES, SIZE_T, LPTHREAD_START_ROUTINE, LPVOID, DWORD,
        LPDWORD);
    using CoCreateInstanceFn = HRESULT(WINAPI*)(
        REFCLSID, LPUNKNOWN, DWORD, REFIID, LPVOID*);
    using Direct3DCreate9Fn = IDirect3D9*(WINAPI*)(UINT);
    using D3DCreateDeviceFn = HRESULT(STDMETHODCALLTYPE*)(
        IDirect3D9*, UINT, D3DDEVTYPE, HWND, DWORD,
        D3DPRESENT_PARAMETERS*, IDirect3DDevice9**);
    using D3DResetFn = HRESULT(STDMETHODCALLTYPE*)(
        IDirect3DDevice9*, D3DPRESENT_PARAMETERS*);
    using D3DPresentFn = HRESULT(STDMETHODCALLTYPE*)(
        IDirect3DDevice9*, const RECT*, const RECT*, HWND, const RGNDATA*);
    using D3DSceneFn = HRESULT(STDMETHODCALLTYPE*)(IDirect3DDevice9*);

    CreateFileAFn g_createFileA = ::CreateFileA;
    CreateFileWFn g_createFileW = ::CreateFileW;
    WaitNamedPipeAFn g_waitNamedPipeA = ::WaitNamedPipeA;
    WaitNamedPipeWFn g_waitNamedPipeW = ::WaitNamedPipeW;
    WaitForSingleObjectFn g_waitForSingleObject = ::WaitForSingleObject;
    WaitForMultipleObjectsFn g_waitForMultipleObjects =
        ::WaitForMultipleObjects;
    SleepFn g_sleep = ::Sleep;
    CreateEventAFn g_createEventA = ::CreateEventA;
    CreateEventWFn g_createEventW = ::CreateEventW;
    CreateMutexAFn g_createMutexA = ::CreateMutexA;
    CreateMutexWFn g_createMutexW = ::CreateMutexW;
    CreateNamedPipeAFn g_createNamedPipeA = ::CreateNamedPipeA;
    CreateNamedPipeWFn g_createNamedPipeW = ::CreateNamedPipeW;
    ConnectNamedPipeFn g_connectNamedPipe = ::ConnectNamedPipe;
    CreateFileMappingAFn g_createFileMappingA = ::CreateFileMappingA;
    CreateFileMappingWFn g_createFileMappingW = ::CreateFileMappingW;
    LoadLibraryAFn g_loadLibraryA = ::LoadLibraryA;
    LoadLibraryWFn g_loadLibraryW = ::LoadLibraryW;
    CreateThreadFn g_createThread = ::CreateThread;
    CoCreateInstanceFn g_coCreateInstance = ::CoCreateInstance;
    Direct3DCreate9Fn g_direct3DCreate9 = ::Direct3DCreate9;
    D3DCreateDeviceFn g_d3dCreateDevice = nullptr;
    D3DResetFn g_d3dReset = nullptr;
    D3DPresentFn g_d3dPresent = nullptr;
    D3DSceneFn g_d3dBeginScene = nullptr;
    D3DSceneFn g_d3dEndScene = nullptr;
    volatile LONG g_presentCount = 0;
    volatile LONG g_beginSceneCount = 0;
    volatile LONG g_endSceneCount = 0;
    volatile LONG g_messageWakeStarted = 0;
    DWORD g_messageThreadId = 0;
    HWND g_messageWindow = nullptr;

    const char* Safe(const char* value)
    {
        return value == nullptr ? "<null>" : value;
    }

    template<size_t Size>
    const char* Narrow(const wchar_t* value, char (&buffer)[Size])
    {
        if (value == nullptr)
            return "<null>";
        const int converted = WideCharToMultiByte(
            CP_UTF8, 0, value, -1, buffer, static_cast<int>(Size),
            nullptr, nullptr);
        if (converted <= 0)
            return "<wide-conversion-failed>";
        return buffer;
    }

    void Log(const char* format, ...)
    {
        if (g_log == INVALID_HANDLE_VALUE ||
            InterlockedCompareExchange(&g_logging, 1, 0) != 0)
            return;

        char message[2048] = {};
        const int prefix = _snprintf_s(
            message, sizeof(message), _TRUNCATE, "%10lu T%05lu ",
            GetTickCount(), GetCurrentThreadId());
        va_list arguments;
        va_start(arguments, format);
        _vsnprintf_s(
            message + (prefix > 0 ? prefix : 0),
            sizeof(message) - (prefix > 0 ? prefix : 0),
            _TRUNCATE, format, arguments);
        va_end(arguments);
        strncat_s(message, sizeof(message), "\r\n", _TRUNCATE);

        DWORD written = 0;
        ::WriteFile(
            g_log, message, static_cast<DWORD>(strlen(message)), &written,
            nullptr);
        ::FlushFileBuffers(g_log);
        InterlockedExchange(&g_logging, 0);
    }

    __forceinline void* Caller()
    {
        return _ReturnAddress();
    }

    HANDLE WINAPI TraceCreateFileA(
        LPCSTR name, DWORD access, DWORD share,
        LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags,
        HANDLE templateFile)
    {
        const HANDLE result = g_createFileA(
            name, access, share, security, disposition, flags, templateFile);
        const DWORD error = GetLastError();
        Log("CreateFileA caller=%p name=\"%s\" access=%08lX disp=%lu -> %p err=%lu",
            Caller(), Safe(name), access, disposition, result, error);
        SetLastError(error);
        return result;
    }

    HANDLE WINAPI TraceCreateFileW(
        LPCWSTR name, DWORD access, DWORD share,
        LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags,
        HANDLE templateFile)
    {
        const HANDLE result = g_createFileW(
            name, access, share, security, disposition, flags, templateFile);
        const DWORD error = GetLastError();
        char narrow[1024] = {};
        Log("CreateFileW caller=%p name=\"%s\" access=%08lX disp=%lu -> %p err=%lu",
            Caller(), Narrow(name, narrow), access, disposition, result, error);
        SetLastError(error);
        return result;
    }

    BOOL WINAPI TraceWaitNamedPipeA(LPCSTR name, DWORD timeout)
    {
        Log("WaitNamedPipeA ENTER caller=%p name=\"%s\" timeout=%lu",
            Caller(), Safe(name), timeout);
        const BOOL result = g_waitNamedPipeA(name, timeout);
        const DWORD error = GetLastError();
        Log("WaitNamedPipeA LEAVE name=\"%s\" -> %d err=%lu",
            Safe(name), result, error);
        SetLastError(error);
        return result;
    }

    BOOL WINAPI TraceWaitNamedPipeW(LPCWSTR name, DWORD timeout)
    {
        char narrow[1024] = {};
        Log("WaitNamedPipeW ENTER caller=%p name=\"%s\" timeout=%lu",
            Caller(), Narrow(name, narrow), timeout);
        const BOOL result = g_waitNamedPipeW(name, timeout);
        const DWORD error = GetLastError();
        Log("WaitNamedPipeW LEAVE -> %d err=%lu", result, error);
        SetLastError(error);
        return result;
    }

    DWORD WINAPI TraceWaitForSingleObject(HANDLE handle, DWORD timeout)
    {
        if (timeout >= 1000)
            Log("WaitForSingleObject ENTER caller=%p handle=%p timeout=%lu",
                Caller(), handle, timeout);
        const DWORD result = g_waitForSingleObject(handle, timeout);
        if (timeout >= 1000)
            Log("WaitForSingleObject LEAVE handle=%p -> %lu",
                handle, result);
        return result;
    }

    DWORD WINAPI TraceWaitForMultipleObjects(
        DWORD count, const HANDLE* handles, BOOL waitAll, DWORD timeout)
    {
        if (timeout >= 1000)
            Log("WaitForMultipleObjects ENTER caller=%p count=%lu all=%d timeout=%lu first=%p",
                Caller(), count, waitAll, timeout,
                count > 0 && handles != nullptr ? handles[0] : nullptr);
        const DWORD result =
            g_waitForMultipleObjects(count, handles, waitAll, timeout);
        if (timeout >= 1000)
            Log("WaitForMultipleObjects LEAVE -> %lu", result);
        return result;
    }

    void WINAPI TraceSleep(DWORD milliseconds)
    {
        if (milliseconds >= 500)
            Log("Sleep caller=%p milliseconds=%lu", Caller(), milliseconds);
        g_sleep(milliseconds);
    }

    HANDLE WINAPI TraceCreateEventA(
        LPSECURITY_ATTRIBUTES security, BOOL manualReset, BOOL initialState,
        LPCSTR name)
    {
        const HANDLE result =
            g_createEventA(security, manualReset, initialState, name);
        Log("CreateEventA caller=%p name=\"%s\" manual=%d initial=%d -> %p",
            Caller(), Safe(name), manualReset, initialState, result);
        return result;
    }

    HANDLE WINAPI TraceCreateEventW(
        LPSECURITY_ATTRIBUTES security, BOOL manualReset, BOOL initialState,
        LPCWSTR name)
    {
        const HANDLE result =
            g_createEventW(security, manualReset, initialState, name);
        char narrow[1024] = {};
        Log("CreateEventW caller=%p name=\"%s\" manual=%d initial=%d -> %p",
            Caller(), Narrow(name, narrow), manualReset, initialState, result);
        return result;
    }

    HANDLE WINAPI TraceCreateMutexA(
        LPSECURITY_ATTRIBUTES security, BOOL initialOwner, LPCSTR name)
    {
        const HANDLE result = g_createMutexA(security, initialOwner, name);
        Log("CreateMutexA caller=%p name=\"%s\" initial=%d -> %p",
            Caller(), Safe(name), initialOwner, result);
        return result;
    }

    HANDLE WINAPI TraceCreateMutexW(
        LPSECURITY_ATTRIBUTES security, BOOL initialOwner, LPCWSTR name)
    {
        const HANDLE result = g_createMutexW(security, initialOwner, name);
        char narrow[1024] = {};
        Log("CreateMutexW caller=%p name=\"%s\" initial=%d -> %p",
            Caller(), Narrow(name, narrow), initialOwner, result);
        return result;
    }

    HANDLE WINAPI TraceCreateNamedPipeA(
        LPCSTR name, DWORD openMode, DWORD pipeMode, DWORD maxInstances,
        DWORD outBufferSize, DWORD inBufferSize, DWORD timeout,
        LPSECURITY_ATTRIBUTES security)
    {
        const HANDLE result = g_createNamedPipeA(
            name, openMode, pipeMode, maxInstances, outBufferSize,
            inBufferSize, timeout, security);
        const DWORD error = GetLastError();
        Log("CreateNamedPipeA caller=%p name=\"%s\" -> %p err=%lu",
            Caller(), Safe(name), result, error);
        SetLastError(error);
        return result;
    }

    HANDLE WINAPI TraceCreateNamedPipeW(
        LPCWSTR name, DWORD openMode, DWORD pipeMode, DWORD maxInstances,
        DWORD outBufferSize, DWORD inBufferSize, DWORD timeout,
        LPSECURITY_ATTRIBUTES security)
    {
        const HANDLE result = g_createNamedPipeW(
            name, openMode, pipeMode, maxInstances, outBufferSize,
            inBufferSize, timeout, security);
        const DWORD error = GetLastError();
        char narrow[1024] = {};
        Log("CreateNamedPipeW caller=%p name=\"%s\" -> %p err=%lu",
            Caller(), Narrow(name, narrow), result, error);
        SetLastError(error);
        return result;
    }

    BOOL WINAPI TraceConnectNamedPipe(HANDLE pipe, LPOVERLAPPED overlapped)
    {
        Log("ConnectNamedPipe ENTER caller=%p handle=%p overlapped=%p",
            Caller(), pipe, overlapped);
        const BOOL result = g_connectNamedPipe(pipe, overlapped);
        const DWORD error = GetLastError();
        Log("ConnectNamedPipe LEAVE handle=%p -> %d err=%lu",
            pipe, result, error);
        SetLastError(error);
        return result;
    }

    HANDLE WINAPI TraceCreateFileMappingA(
        HANDLE file, LPSECURITY_ATTRIBUTES security, DWORD protect,
        DWORD high, DWORD low, LPCSTR name)
    {
        const HANDLE result =
            g_createFileMappingA(file, security, protect, high, low, name);
        Log("CreateFileMappingA caller=%p name=\"%s\" size=%08lX%08lX -> %p",
            Caller(), Safe(name), high, low, result);
        return result;
    }

    HANDLE WINAPI TraceCreateFileMappingW(
        HANDLE file, LPSECURITY_ATTRIBUTES security, DWORD protect,
        DWORD high, DWORD low, LPCWSTR name)
    {
        const HANDLE result =
            g_createFileMappingW(file, security, protect, high, low, name);
        char narrow[1024] = {};
        Log("CreateFileMappingW caller=%p name=\"%s\" size=%08lX%08lX -> %p",
            Caller(), Narrow(name, narrow), high, low, result);
        return result;
    }

    HMODULE WINAPI TraceLoadLibraryA(LPCSTR name)
    {
        const HMODULE result = g_loadLibraryA(name);
        const DWORD error = GetLastError();
        Log("LoadLibraryA caller=%p name=\"%s\" -> %p err=%lu",
            Caller(), Safe(name), result, error);
        SetLastError(error);
        return result;
    }

    HMODULE WINAPI TraceLoadLibraryW(LPCWSTR name)
    {
        const HMODULE result = g_loadLibraryW(name);
        const DWORD error = GetLastError();
        char narrow[1024] = {};
        Log("LoadLibraryW caller=%p name=\"%s\" -> %p err=%lu",
            Caller(), Narrow(name, narrow), result, error);
        SetLastError(error);
        return result;
    }

    HANDLE WINAPI TraceCreateThread(
        LPSECURITY_ATTRIBUTES attributes, SIZE_T stackSize,
        LPTHREAD_START_ROUTINE start, LPVOID parameter, DWORD flags,
        LPDWORD threadId)
    {
        const HANDLE result = g_createThread(
            attributes, stackSize, start, parameter, flags, threadId);
        Log("CreateThread caller=%p start=%p parameter=%p flags=%08lX -> %p tid=%lu",
            Caller(), start, parameter, flags, result,
            threadId == nullptr ? 0 : *threadId);
        return result;
    }

    HRESULT WINAPI TraceCoCreateInstance(
        REFCLSID classId, LPUNKNOWN outer, DWORD context, REFIID interfaceId,
        LPVOID* object)
    {
        wchar_t classText[64] = {};
        wchar_t interfaceText[64] = {};
        StringFromGUID2(classId, classText, _countof(classText));
        StringFromGUID2(interfaceId, interfaceText, _countof(interfaceText));
        char narrowClass[128] = {};
        char narrowInterface[128] = {};
        Log("CoCreateInstance ENTER caller=%p clsid=%s iid=%s context=%08lX",
            Caller(), Narrow(classText, narrowClass),
            Narrow(interfaceText, narrowInterface), context);
        const HRESULT result =
            g_coCreateInstance(classId, outer, context, interfaceId, object);
        Log("CoCreateInstance LEAVE hr=%08lX object=%p",
            static_cast<DWORD>(result),
            object == nullptr ? nullptr : *object);
        return result;
    }

    bool PatchVtableSlot(
        void* object, size_t index, void* replacement, void** original)
    {
        if (object == nullptr)
            return false;
        auto vtable = *reinterpret_cast<void***>(object);
        if (vtable == nullptr)
            return false;

        DWORD oldProtect = 0;
        if (!VirtualProtect(
                &vtable[index], sizeof(vtable[index]), PAGE_READWRITE,
                &oldProtect))
            return false;
        if (original != nullptr)
            *original = vtable[index];
        vtable[index] = replacement;
        DWORD ignored = 0;
        VirtualProtect(
            &vtable[index], sizeof(vtable[index]), oldProtect, &ignored);
        FlushInstructionCache(
            GetCurrentProcess(), &vtable[index], sizeof(vtable[index]));
        return true;
    }

    HRESULT STDMETHODCALLTYPE TraceD3DReset(
        IDirect3DDevice9* device, D3DPRESENT_PARAMETERS* parameters)
    {
        Log("IDirect3DDevice9::Reset ENTER caller=%p size=%ux%u windowed=%d",
            Caller(),
            parameters == nullptr ? 0 : parameters->BackBufferWidth,
            parameters == nullptr ? 0 : parameters->BackBufferHeight,
            parameters == nullptr ? 0 : parameters->Windowed);
        const HRESULT result = g_d3dReset(device, parameters);
        Log("IDirect3DDevice9::Reset LEAVE hr=%08lX",
            static_cast<DWORD>(result));
        return result;
    }

    HRESULT STDMETHODCALLTYPE TraceD3DPresent(
        IDirect3DDevice9* device, const RECT* source, const RECT* destination,
        HWND window, const RGNDATA* dirtyRegion)
    {
        const LONG count = InterlockedIncrement(&g_presentCount);
        if (count <= 10 || count % 300 == 0)
            Log("IDirect3DDevice9::Present ENTER caller=%p count=%ld",
                Caller(), count);
        const HRESULT result = g_d3dPresent(
            device, source, destination, window, dirtyRegion);
        if (count <= 10 || count % 300 == 0)
            Log("IDirect3DDevice9::Present LEAVE count=%ld hr=%08lX",
                count, static_cast<DWORD>(result));
        return result;
    }

    HRESULT STDMETHODCALLTYPE TraceD3DBeginScene(IDirect3DDevice9* device)
    {
        const LONG count = InterlockedIncrement(&g_beginSceneCount);
        if (count <= 10 || count % 300 == 0)
            Log("IDirect3DDevice9::BeginScene caller=%p count=%ld",
                Caller(), count);
        return g_d3dBeginScene(device);
    }

    HRESULT STDMETHODCALLTYPE TraceD3DEndScene(IDirect3DDevice9* device)
    {
        const LONG count = InterlockedIncrement(&g_endSceneCount);
        if (count <= 10 || count % 300 == 0)
            Log("IDirect3DDevice9::EndScene caller=%p count=%ld",
                Caller(), count);
        return g_d3dEndScene(device);
    }

    DWORD WINAPI MessageWakeThread(LPVOID)
    {
        Log("Message wake helper started targetTid=%lu hwnd=%p",
            g_messageThreadId, g_messageWindow);
        // CreateDevice returns before the game's WinMain reaches GetMessage.
        // Give it a moment to enter the queue, then supply only the initial
        // paint that Wine's Android window path failed to generate. Repeating
        // WM_PAINT drove the main thread continuously and was useful only as a
        // diagnostic.
        g_sleep(50);
        SetLastError(ERROR_SUCCESS);
        const BOOL result = g_messageWindow == nullptr
            ? FALSE
            : PostMessageA(g_messageWindow, WM_PAINT, 0, 0);
        const DWORD error = GetLastError();
        Log("Message wake one-shot window=%d err=%lu", result, error);
        return 0;
    }

    HRESULT STDMETHODCALLTYPE TraceD3DCreateDevice(
        IDirect3D9* direct3D, UINT adapter, D3DDEVTYPE deviceType,
        HWND focusWindow, DWORD behaviorFlags,
        D3DPRESENT_PARAMETERS* parameters, IDirect3DDevice9** device)
    {
        Log("IDirect3D9::CreateDevice ENTER caller=%p adapter=%u type=%u flags=%08lX size=%ux%u windowed=%d",
            Caller(), adapter, static_cast<UINT>(deviceType), behaviorFlags,
            parameters == nullptr ? 0 : parameters->BackBufferWidth,
            parameters == nullptr ? 0 : parameters->BackBufferHeight,
            parameters == nullptr ? 0 : parameters->Windowed);
        const HRESULT result = g_d3dCreateDevice(
            direct3D, adapter, deviceType, focusWindow, behaviorFlags,
            parameters, device);
        Log("IDirect3D9::CreateDevice LEAVE hr=%08lX device=%p",
            static_cast<DWORD>(result),
            device == nullptr ? nullptr : *device);

        if (SUCCEEDED(result) && device != nullptr && *device != nullptr)
        {
            void* original = nullptr;
            if (PatchVtableSlot(
                    *device, 16, reinterpret_cast<void*>(&TraceD3DReset),
                    &original))
                g_d3dReset = reinterpret_cast<D3DResetFn>(original);
            if (PatchVtableSlot(
                    *device, 17, reinterpret_cast<void*>(&TraceD3DPresent),
                    &original))
                g_d3dPresent = reinterpret_cast<D3DPresentFn>(original);
            if (PatchVtableSlot(
                    *device, 41, reinterpret_cast<void*>(&TraceD3DBeginScene),
                    &original))
                g_d3dBeginScene = reinterpret_cast<D3DSceneFn>(original);
            if (PatchVtableSlot(
                    *device, 42, reinterpret_cast<void*>(&TraceD3DEndScene),
                    &original))
                g_d3dEndScene = reinterpret_cast<D3DSceneFn>(original);
            Log("IDirect3DDevice9 vtable trace installed");

            g_messageThreadId = GetCurrentThreadId();
            g_messageWindow = focusWindow;
            if (InterlockedCompareExchange(
                    &g_messageWakeStarted, 1, 0) == 0)
            {
                HANDLE helper = g_createThread(
                    nullptr, 0, &MessageWakeThread, nullptr, 0, nullptr);
                if (helper != nullptr)
                {
                    CloseHandle(helper);
                    Log("Message wake helper scheduled targetTid=%lu hwnd=%p",
                        g_messageThreadId, g_messageWindow);
                }
                else
                {
                    Log("Message wake helper failed err=%lu",
                        GetLastError());
                }
            }
        }
        return result;
    }

    IDirect3D9* WINAPI TraceDirect3DCreate9(UINT sdkVersion)
    {
        Log("Direct3DCreate9 ENTER caller=%p sdk=%u", Caller(), sdkVersion);
        IDirect3D9* result = g_direct3DCreate9(sdkVersion);
        Log("Direct3DCreate9 LEAVE object=%p", result);
        if (result != nullptr)
        {
            void* original = nullptr;
            if (PatchVtableSlot(
                    result, 16,
                    reinterpret_cast<void*>(&TraceD3DCreateDevice),
                    &original))
            {
                g_d3dCreateDevice =
                    reinterpret_cast<D3DCreateDeviceFn>(original);
                Log("IDirect3D9::CreateDevice trace installed original=%p",
                    original);
            }
        }
        return result;
    }

    void DescribeAddress(
        DWORD address, char* description, size_t descriptionSize)
    {
        _snprintf_s(
            description, descriptionSize, _TRUNCATE, "%08lX", address);
        HANDLE snapshot = CreateToolhelp32Snapshot(
            TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
            GetCurrentProcessId());
        if (snapshot == INVALID_HANDLE_VALUE)
            return;

        MODULEENTRY32 module = {};
        module.dwSize = sizeof(module);
        if (Module32First(snapshot, &module))
        {
            do
            {
                const DWORD begin =
                    reinterpret_cast<DWORD>(module.modBaseAddr);
                const DWORD end = begin + module.modBaseSize;
                if (address >= begin && address < end)
                {
                    _snprintf_s(
                        description, descriptionSize, _TRUNCATE,
                        "%s+%08lX", module.szModule, address - begin);
                    break;
                }
            }
            while (Module32Next(snapshot, &module));
        }
        CloseHandle(snapshot);
    }

    void SnapshotThreads(const char* label)
    {
        const DWORD processId = GetCurrentProcessId();
        const DWORD currentThreadId = GetCurrentThreadId();
        HANDLE snapshot =
            CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            Log("Thread snapshot %s failed err=%lu", label, GetLastError());
            return;
        }

        Log("Thread snapshot %s begin", label);
        THREADENTRY32 thread = {};
        thread.dwSize = sizeof(thread);
        if (Thread32First(snapshot, &thread))
        {
            do
            {
                if (thread.th32OwnerProcessID != processId ||
                    thread.th32ThreadID == currentThreadId)
                    continue;

                HANDLE handle = OpenThread(
                    THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT |
                        THREAD_QUERY_INFORMATION,
                    FALSE, thread.th32ThreadID);
                if (handle == nullptr)
                {
                    Log("Thread tid=%lu OpenThread failed err=%lu",
                        thread.th32ThreadID, GetLastError());
                    continue;
                }

                const DWORD suspendCount = SuspendThread(handle);
                if (suspendCount == static_cast<DWORD>(-1))
                {
                    Log("Thread tid=%lu SuspendThread failed err=%lu",
                        thread.th32ThreadID, GetLastError());
                    CloseHandle(handle);
                    continue;
                }

                CONTEXT context = {};
                context.ContextFlags = CONTEXT_CONTROL;
                if (GetThreadContext(handle, &context))
                {
                    char location[512] = {};
                    DescribeAddress(
                        context.Eip, location, _countof(location));
                    Log("Thread tid=%lu eip=%08lX location=%s esp=%08lX ebp=%08lX",
                        thread.th32ThreadID, context.Eip, location,
                        context.Esp, context.Ebp);

                    DWORD frame = context.Ebp;
                    for (int depth = 0; depth < 8 && frame != 0; ++depth)
                    {
                        DWORD words[2] = {};
                        SIZE_T bytesRead = 0;
                        if (!ReadProcessMemory(
                                GetCurrentProcess(),
                                reinterpret_cast<const void*>(frame),
                                words, sizeof(words), &bytesRead) ||
                            bytesRead != sizeof(words))
                            break;

                        char returnLocation[512] = {};
                        DescribeAddress(
                            words[1], returnLocation,
                            _countof(returnLocation));
                        Log("Thread tid=%lu frame=%d ebp=%08lX ret=%08lX location=%s next=%08lX",
                            thread.th32ThreadID, depth, frame, words[1],
                            returnLocation, words[0]);

                        if (words[0] <= frame ||
                            words[0] - frame > 0x00100000)
                            break;
                        frame = words[0];
                    }
                }
                else
                {
                    Log("Thread tid=%lu GetThreadContext failed err=%lu",
                        thread.th32ThreadID, GetLastError());
                }

                ResumeThread(handle);
                CloseHandle(handle);
            }
            while (Thread32Next(snapshot, &thread));
        }
        CloseHandle(snapshot);
        Log("Thread snapshot %s end", label);
    }

    DWORD WINAPI WatchdogThread(LPVOID)
    {
        g_sleep(8000);
        SnapshotThreads("8s");
        g_sleep(12000);
        SnapshotThreads("20s");
        return 0;
    }

    bool PatchImport(
        HMODULE module, const char* importedModule, const char* functionName,
        void* replacement, void** original)
    {
        auto base = reinterpret_cast<unsigned char*>(module);
        const auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (dos->e_magic != IMAGE_DOS_SIGNATURE)
            return false;
        const auto nt = reinterpret_cast<IMAGE_NT_HEADERS*>(
            base + dos->e_lfanew);
        if (nt->Signature != IMAGE_NT_SIGNATURE)
            return false;
        const DWORD importRva = nt->OptionalHeader
            .DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress;
        if (importRva == 0)
            return false;

        auto descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
            base + importRva);
        for (; descriptor->Name != 0; ++descriptor)
        {
            const char* name =
                reinterpret_cast<const char*>(base + descriptor->Name);
            if (_stricmp(name, importedModule) != 0)
                continue;

            auto names = reinterpret_cast<IMAGE_THUNK_DATA*>(
                base + (descriptor->OriginalFirstThunk != 0
                    ? descriptor->OriginalFirstThunk
                    : descriptor->FirstThunk));
            auto addresses = reinterpret_cast<IMAGE_THUNK_DATA*>(
                base + descriptor->FirstThunk);
            for (; names->u1.AddressOfData != 0; ++names, ++addresses)
            {
                if (IMAGE_SNAP_BY_ORDINAL(names->u1.Ordinal))
                    continue;
                const auto import = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                    base + names->u1.AddressOfData);
                if (strcmp(
                        reinterpret_cast<const char*>(import->Name),
                        functionName) != 0)
                    continue;

                DWORD oldProtect = 0;
                if (!VirtualProtect(
                        &addresses->u1.Function, sizeof(void*),
                        PAGE_READWRITE, &oldProtect))
                    return false;
                if (original != nullptr)
                    *original = reinterpret_cast<void*>(
                        addresses->u1.Function);
                addresses->u1.Function =
                    reinterpret_cast<ULONG_PTR>(replacement);
                DWORD ignored = 0;
                VirtualProtect(
                    &addresses->u1.Function, sizeof(void*), oldProtect,
                    &ignored);
                FlushInstructionCache(
                    GetCurrentProcess(), &addresses->u1.Function,
                    sizeof(void*));
                return true;
            }
        }
        return false;
    }

    template<typename T>
    void Hook(
        HMODULE executable, const char* module, const char* function,
        void* replacement, T& original)
    {
        void* address = reinterpret_cast<void*>(original);
        if (PatchImport(
                executable, module, function, replacement, &address))
        {
            original = reinterpret_cast<T>(address);
            Log("Hooked %s!%s original=%p replacement=%p",
                module, function, address, replacement);
        }
    }

    void InstallProbe()
    {
        wchar_t executable[MAX_PATH] = {};
        wchar_t directory[MAX_PATH] = {};
        GetModuleFileNameW(nullptr, executable, _countof(executable));
        GetCurrentDirectoryW(_countof(directory), directory);

        g_log = ::CreateFileW(
            L".\\AkaiWaitProbe.log", GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL, nullptr);
        if (g_log == INVALID_HANDLE_VALUE)
            return;

        char executableText[1024] = {};
        char directoryText[1024] = {};
        Log("AndroidGuestWaitProbe attached executable=\"%s\" cwd=\"%s\"",
            Narrow(executable, executableText),
            Narrow(directory, directoryText));

        HMODULE mainModule = GetModuleHandleW(nullptr);
        Hook(mainModule, "KERNEL32.dll", "CreateFileA",
            reinterpret_cast<void*>(&TraceCreateFileA), g_createFileA);
        Hook(mainModule, "KERNEL32.dll", "CreateFileW",
            reinterpret_cast<void*>(&TraceCreateFileW), g_createFileW);
        Hook(mainModule, "KERNEL32.dll", "WaitNamedPipeA",
            reinterpret_cast<void*>(&TraceWaitNamedPipeA), g_waitNamedPipeA);
        Hook(mainModule, "KERNEL32.dll", "WaitNamedPipeW",
            reinterpret_cast<void*>(&TraceWaitNamedPipeW), g_waitNamedPipeW);
        Hook(mainModule, "KERNEL32.dll", "WaitForSingleObject",
            reinterpret_cast<void*>(&TraceWaitForSingleObject),
            g_waitForSingleObject);
        Hook(mainModule, "KERNEL32.dll", "WaitForMultipleObjects",
            reinterpret_cast<void*>(&TraceWaitForMultipleObjects),
            g_waitForMultipleObjects);
        Hook(mainModule, "KERNEL32.dll", "Sleep",
            reinterpret_cast<void*>(&TraceSleep), g_sleep);
        Hook(mainModule, "KERNEL32.dll", "CreateEventA",
            reinterpret_cast<void*>(&TraceCreateEventA), g_createEventA);
        Hook(mainModule, "KERNEL32.dll", "CreateEventW",
            reinterpret_cast<void*>(&TraceCreateEventW), g_createEventW);
        Hook(mainModule, "KERNEL32.dll", "CreateMutexA",
            reinterpret_cast<void*>(&TraceCreateMutexA), g_createMutexA);
        Hook(mainModule, "KERNEL32.dll", "CreateMutexW",
            reinterpret_cast<void*>(&TraceCreateMutexW), g_createMutexW);
        Hook(mainModule, "KERNEL32.dll", "CreateNamedPipeA",
            reinterpret_cast<void*>(&TraceCreateNamedPipeA),
            g_createNamedPipeA);
        Hook(mainModule, "KERNEL32.dll", "CreateNamedPipeW",
            reinterpret_cast<void*>(&TraceCreateNamedPipeW),
            g_createNamedPipeW);
        Hook(mainModule, "KERNEL32.dll", "ConnectNamedPipe",
            reinterpret_cast<void*>(&TraceConnectNamedPipe),
            g_connectNamedPipe);
        Hook(mainModule, "KERNEL32.dll", "CreateFileMappingA",
            reinterpret_cast<void*>(&TraceCreateFileMappingA),
            g_createFileMappingA);
        Hook(mainModule, "KERNEL32.dll", "CreateFileMappingW",
            reinterpret_cast<void*>(&TraceCreateFileMappingW),
            g_createFileMappingW);
        Hook(mainModule, "KERNEL32.dll", "LoadLibraryA",
            reinterpret_cast<void*>(&TraceLoadLibraryA), g_loadLibraryA);
        Hook(mainModule, "KERNEL32.dll", "LoadLibraryW",
            reinterpret_cast<void*>(&TraceLoadLibraryW), g_loadLibraryW);
        Hook(mainModule, "KERNEL32.dll", "CreateThread",
            reinterpret_cast<void*>(&TraceCreateThread), g_createThread);
        Hook(mainModule, "ole32.dll", "CoCreateInstance",
            reinterpret_cast<void*>(&TraceCoCreateInstance),
            g_coCreateInstance);
        Hook(mainModule, "d3d9.dll", "Direct3DCreate9",
            reinterpret_cast<void*>(&TraceDirect3DCreate9),
            g_direct3DCreate9);
        Log("AndroidGuestWaitProbe install complete");

        HANDLE watchdog = g_createThread(
            nullptr, 0, &WatchdogThread, nullptr, 0, nullptr);
        if (watchdog != nullptr)
        {
            CloseHandle(watchdog);
            Log("AndroidGuestWaitProbe watchdog started");
        }
        else
        {
            Log("AndroidGuestWaitProbe watchdog failed err=%lu",
                GetLastError());
        }
    }
}

// OpenParrot overwrites the first five bytes of each export with a near JMP.
// Keep a generous executable landing pad after that region. Tiny optimized
// "xor eax,eax; ret" stubs leave INT3 alignment bytes immediately after the
// entry point, which can be hit while Wine/OpenParrot installs the Fast I/O
// redirects.
#define IDMAC_STUB_BODY() \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    __asm { _emit 0x90 } \
    return 0

extern "C" __declspec(noinline) DWORD __cdecl iDmacDrvOpen(
    int, LPVOID, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) DWORD __cdecl iDmacDrvClose(
    int, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvDmaRead(
    int, LPVOID, UINT_PTR, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvDmaWrite(
    int, LPVOID, UINT_PTR, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvRegisterRead(
    int, DWORD, LPVOID, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvRegisterWrite(
    int, DWORD, int, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvRegisterBufferRead(
    int, DWORD, LPVOID, UINT_PTR, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvRegisterBufferWrite(
    int, DWORD, LPVOID, UINT_PTR, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvMemoryRead(
    int, DWORD, LPVOID, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvMemoryWrite(
    int, DWORD, int, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvMemoryBufferRead(
    int, DWORD, LPVOID, UINT_PTR, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvMemoryBufferWrite(
    int, int, LPVOID, UINT_PTR, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvMemoryReadExt(
    int, DWORD, int, LPVOID, DWORD, LPVOID) { IDMAC_STUB_BODY(); }
extern "C" __declspec(noinline) int __cdecl iDmacDrvMemoryWriteExt(
    int, int, int, LPVOID, size_t, LPVOID) { IDMAC_STUB_BODY(); }

BOOL WINAPI DllMain(HINSTANCE, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
        InstallProbe();
    else if (reason == DLL_PROCESS_DETACH &&
             g_log != INVALID_HANDLE_VALUE)
    {
        Log("AndroidGuestWaitProbe detached");
        CloseHandle(g_log);
        g_log = INVALID_HANDLE_VALUE;
    }
    return TRUE;
}
