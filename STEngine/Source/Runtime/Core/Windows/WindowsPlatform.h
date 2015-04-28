#pragma once

struct FWindowsPlatformTypes : public FGenericPlatformTypes
{
#ifdef _WIN64
    typedef unsigned __int64	SIZE_T;
#else
    typedef unsigned long		SIZE_T;
#endif
};

typedef FWindowsPlatformTypes FPlatformTypes;

// Base defines, must define these for the platform, there are no defaults
#define PLATFORM_DESKTOP					1
#if defined( _WIN64 )
#define PLATFORM_64BITS					1
#else
#define PLATFORM_64BITS					0
#endif

#define VARARGS     __cdecl					/* Functions with variable arguments */
#define CDECL	    __cdecl					/* Standard C function */
#define STDCALL		__stdcall				/* Standard calling convention */
#define FORCEINLINE __forceinline			/* Force code to be inline */
#define FORCENOINLINE __declspec(noinline)	/* Force code to NOT be inline */

// Hints compiler that expression is true; generally restricted to comparisons against constants
#if !defined(__clang__)		// Clang doesn't support __assume (Microsoft specific)
#define ASSUME(expr) __assume(expr)
#endif

#define DECLARE_UINT64(x)	x

// Backwater of the spec. All compilers support this except microsoft, and they will soon
#if !defined(__clang__)		// Clang expects typename outside template
#define TYPENAME_OUTSIDE_TEMPLATE
#endif

#pragma warning(disable : 4481) // nonstandard extension used: override specifier 'override'

#if defined(__clang__)
#define CONSTEXPR constexpr
#else
#define CONSTEXPR
#endif
#define ABSTRACT abstract

// Strings.
#define LINE_TERMINATOR TEXT("\r\n")
#define LINE_TERMINATOR_ANSI "\r\n"

// Alignment.
#if defined(__clang__)
#define GCC_PACK(n) __attribute__((packed,aligned(n)))
#define GCC_ALIGN(n) __attribute__((aligned(n)))
#else
#define MS_ALIGN(n) __declspec(align(n))
#endif

// Pragmas
#define MSVC_PRAGMA(Pragma) __pragma(Pragma)

// Prefetch
#define CACHE_LINE_SIZE	128

// DLL export and import definitions
#define DLLEXPORT __declspec(dllexport)
#define DLLIMPORT __declspec(dllimport)

// disable this now as it is annoying for generic platform implementations
#pragma warning(disable : 4100) // unreferenced formal parameter