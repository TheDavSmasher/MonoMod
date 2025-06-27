// clang -O3 -dynamiclib helper_macos_arm64.cc -o helper_macos_arm64.dylib -std=c++11 -lc++

#include <cstdint>
#include <thread>
#include <pthread.h>

extern "C" void mmch_jit_memcpy(void* dst, const void* src, size_t n)
{
    pthread_jit_write_protect_np(0);
    memcpy(dst, src, n);
    pthread_jit_write_protect_np(1);
}

struct AllocMemArgs60
{
    // Input arguments
    uint32_t hotCodeSize;
    uint32_t coldCodeSize;
    uint32_t roDataSize;
    uint32_t xcptnsCount;
    uint32_t flag;

    // Output arguments
    void* hotCodeBlock;
    void* hotCodeBlockRW;
    void* coldCodeBlock;
    void* coldCodeBlockRW;
    void* roDataBlock;
    void* roDataBlockRW;
};

typedef int (*ICoreJitCompiler_compileMethod60)(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode);
typedef int (*ICoreJitCompiler_compileMethod60_hook_post)(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode, int res, AllocMemArgs60 *pArgs);
typedef void (*ICorJitInfo_allocMem60)(void* pThis, struct AllocMemArgs60* pArgs);

struct JitHookConfig60
{
    ICoreJitCompiler_compileMethod60 compileMethod;
    ICoreJitCompiler_compileMethod60 compileMethodHook;
    ICoreJitCompiler_compileMethod60_hook_post compileMethodHookPost;
    ICorJitInfo_allocMem60 allocMem;
    ICorJitInfo_allocMem60 allocMemHook;
};

static int ICoreJitCompiler_compileMethod60_hook(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode);
static void ICorJitInfo_allocMem60_hook(void* pThis, struct AllocMemArgs60* pArgs);

static thread_local int compileMethod60_Entrancy = 0;
static thread_local struct AllocMemArgs60 allocMem60_Args = { 0 };
static struct JitHookConfig60 jitHookConfig60 =
{ 
    .compileMethodHook = &ICoreJitCompiler_compileMethod60_hook,
    .allocMemHook = &ICorJitInfo_allocMem60_hook
};

struct CompileMethod60HookTracker
{
    int lastErrNo;

    CompileMethod60HookTracker()
    {
        lastErrNo = errno;
        ++compileMethod60_Entrancy;
    }

    ~CompileMethod60HookTracker()
    {
        --compileMethod60_Entrancy;
        errno = lastErrNo;
    }

    int entrancy()
    {
        return compileMethod60_Entrancy;
    }
};

static int ICoreJitCompiler_compileMethod60_hook(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode)
{
    CompileMethod60HookTracker tracker;
    int entrancy = tracker.entrancy();
    if (entrancy == 1)
        memset(&allocMem60_Args, 0, sizeof(allocMem60_Args));

    int res = jitHookConfig60.compileMethod(pThis, comp, info, flags, nativeEntry, nativeSizeOfCode);
    if (entrancy == 1)
    {
        // TODO: Consider instead running this on the same thread but checking the PAL_JitWriteProtect TLS enabledCount var
        // if (enabledCount > 0) { pthread_jit_write_protect_np(1); .HookPost(...); pthread_jit_write_protect_np(0); }

        struct AllocMemArgs60 args = allocMem60_Args;
        std::thread hook_post_thread([&]()
        {
            CompileMethod60HookTracker tracker;
            res = jitHookConfig60.compileMethodHookPost(pThis, comp, info, flags, nativeEntry, nativeSizeOfCode, res, &args);
        });

        hook_post_thread.join();
    }

    return res;
}

static void ICorJitInfo_allocMem60_hook(void* pThis, struct AllocMemArgs60* pArgs)
{
    jitHookConfig60.allocMem(pThis, pArgs);

    if (compileMethod60_Entrancy == 1)
        allocMem60_Args = *pArgs;
}

extern "C" void* mmch_jit_hook_config(int runtimeMajMin)
{
    switch (runtimeMajMin)
    {
    case 60:
        return &jitHookConfig60;
    default:
        return nullptr;
    }
}
