#pragma once

#if !defined(PLATFORM_WINDOWS)
#define PLATFORM_WINDOWS 0
#endif
#if !defined(PLATFORM_XBOXONE)
#define PLATFORM_XBOXONE 0
#endif
#if !defined(PLATFORM_MAC)
#define PLATFORM_MAC 0
#endif
#if !defined(PLATFORM_PS4)
#define PLATFORM_PS4 0
#endif
#if !defined(PLATFORM_IOS)
#define PLATFORM_IOS 0
#endif
#if !defined(PLATFORM_ANDROID)
#define PLATFORM_ANDROID 0
#endif
#if !defined(PLATFORM_ANDROIDGL4)
#define PLATFORM_ANDROIDGL4 0
#endif
#if !defined(PLATFORM_ANDROIDES31)
#define PLATFORM_ANDROIDES31 0
#endif
#if !defined(PLATFORM_WINRT)
#define PLATFORM_WINRT 0
#endif
#if !defined(PLATFORM_WINRT_ARM)
#define PLATFORM_WINRT_ARM	0
#endif
#if !defined(PLATFORM_APPLE)
#define PLATFORM_APPLE 0
#endif
#if !defined(PLATFORM_HTML5)
#define PLATFORM_HTML5 0
#endif
#if !defined(PLATFORM_LINUX)
#define PLATFORM_LINUX 0
#endif

#if PLATFORM_APPLE
#include <stddef.h> // needed for size_t
#endif

#if PLATFORM_WINDOWS
#include "Windows/WIndowsPlatform.h"
#elif PLATFORM_PS4
#include "PS4/PS4Platform.h"
#elif PLATFORM_XBOXONE
#include "XboxOne/XboxOnePlatform.h"
#elif PLATFORM_MAC
#include "Mac/MacPlatform.h"
#elif PLATFORM_IOS
#include "IOS/IOSPlatform.h"
#elif PLATFORM_ANDROID
#include "Android/AndroidPlatform.h"
#elif PLATFORM_WINRT_ARM
#include "WinRT/WinRTARMPlatform.h"
#elif PLATFORM_WINRT
#include "WinRT/WinRTPlatform.h"
#elif PLATFORM_HTML5
#include "HTML5/HTML5Platform.h"
#elif PLATFORM_LINUX
#include "Linux/LinuxPlatform.h"
#else
#error Unknown Compiler
#endif

#ifndef VARARGS
#define VARARGS					/* Functions with variable arguments */
#endif
#ifndef CDECL
#define CDECL	    			/* Standard C function */
#endif
#ifndef STDCALL
#define STDCALL					/* Standard calling convention */
#endif
#ifndef FORCEINLINE
#define FORCEINLINE 			/* Force code to be inline */
#endif
#ifndef FORCENOINLINE
#define FORCENOINLINE 			/* Force code to NOT be inline */
#endif
#ifndef RESTRICT
#define RESTRICT __restrict		/* no alias hint */
#endif

#ifndef ASSUME						/* Hints compiler that expression is true; generally restricted to comparisons against constants */
#define ASSUME(...) 
#endif

// String constants
#ifndef LINE_TERMINATOR						
#define LINE_TERMINATOR TEXT("\n")
#endif
#ifndef LINE_TERMINATOR_ANSI
#define LINE_TERMINATOR_ANSI "\n"
#endif

#ifndef DLLEXPORT
    #define DLLEXPORT
    #define DLLIMPORT
#endif

// Unsigned base types.
typedef FPlatformTypes::uint8		uint8;		///< An 8-bit unsigned integer.
typedef FPlatformTypes::uint16		uint16;		///< A 16-bit unsigned integer.
typedef FPlatformTypes::uint32		uint32;		///< A 32-bit unsigned integer.
typedef FPlatformTypes::uint64		uint64;		///< A 64-bit unsigned integer.

// Signed base types.
typedef	FPlatformTypes::int8		int8;		///< An 8-bit signed integer.
typedef FPlatformTypes::int16		int16;		///< A 16-bit signed integer.
typedef FPlatformTypes::int32		int32;		///< A 32-bit signed integer.
typedef FPlatformTypes::int64		int64;		///< A 64-bit signed integer.

// Character types.
typedef FPlatformTypes::ANSICHAR	ANSICHAR;	///< An ANSI character. Normally a signed type.
typedef FPlatformTypes::WIDECHAR	WIDECHAR;	///< A wide character. Normally a signed type.
typedef FPlatformTypes::TCHAR		TCHAR;		///< Either ANSICHAR or WIDECHAR, depending on whether the platform supports wide characters or the requirements of the licensee.
typedef FPlatformTypes::CHAR8		UTF8CHAR;	///< An 8-bit character containing a UTF8 (Unicode, 8-bit, variable-width) code unit.
typedef FPlatformTypes::CHAR16		UCS2CHAR;	///< A 16-bit character containing a UCS2 (Unicode, 16-bit, fixed-width) code unit, used for compatibility with 'Windows TCHAR' across multiple platforms.
typedef FPlatformTypes::CHAR16		UTF16CHAR;	///< A 16-bit character containing a UTF16 (Unicode, 16-bit, variable-width) code unit.
typedef FPlatformTypes::CHAR32		UTF32CHAR;	///< A 32-bit character containing a UTF32 (Unicode, 32-bit, fixed-width) code unit.

typedef FPlatformTypes::UPTRINT UPTRINT;		///< An unsigned integer the same size as a pointer
typedef FPlatformTypes::PTRINT PTRINT;			///< A signed integer the same size as a pointer
typedef FPlatformTypes::SIZE_T SIZE_T;			///< An unsigned integer the same size as a pointer, the same as UPTRINT

typedef FPlatformTypes::TYPE_OF_NULL	TYPE_OF_NULL;		///< The type of the NULL constant.
typedef FPlatformTypes::TYPE_OF_NULLPTR	TYPE_OF_NULLPTR;	///< The type of the C++ nullptr keyword.