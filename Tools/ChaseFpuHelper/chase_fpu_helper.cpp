#if !defined(_M_IX86)
#error ChaseFpuHelper must be built for Win32.
#endif

#include <windows.h>
#include <float.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

namespace
{
	constexpr DWORD kFloatMultipleTraps = 0xC00002B5;
	struct ChaseFpuSite
	{
		uintptr_t rva;
		BYTE bytes[3];
		size_t length;
	};
	constexpr ChaseFpuSite kChaseFpuSites[] = {
		{ 0x000BAF68, { 0xDC, 0x0A, 0x00 }, 2 },
		{ 0x000BAF77, { 0xDC, 0x4A, 0x08 }, 3 },
		{ 0x000BAF7F, { 0xDC, 0x48, 0x08 }, 3 },
		{ 0x000BAF88, { 0xDC, 0x4A, 0x08 }, 3 }
	};

	PVOID g_exceptionHandler = nullptr;
	volatile LONG g_recoveryCount = 0;
	volatile LONG g_siteRecoveryCounts[
		sizeof(kChaseFpuSites) / sizeof(kChaseFpuSites[0])] = {};
	uintptr_t g_chaseFpuContinuation = 0;
	volatile LONG g_productPatchInstalled = 0;
	volatile LONG g_directPlayHostPatchInstalled = 0;
	volatile LONG g_directPlayGroupPatchInstalled = 0;
	volatile LONG g_directPlayRuntimeIdentityPatchInstalled = 0;
	volatile LONG g_directPlayNullGroupPatchInstalled = 0;
	volatile LONG g_directPlayHostCount = 0;
	volatile LONG g_directPlayGroupCount = 0;
	uintptr_t g_directPlayRemoveGroupContinuation = 0;
	void AppendDiagnostics(const char* message);

	struct WideProduct
	{
		uint32_t words[4];
	};

	void AddPartialProduct(WideProduct& product, size_t wordIndex,
		uint64_t value)
	{
		uint64_t sum = static_cast<uint64_t>(product.words[wordIndex]) +
			static_cast<uint32_t>(value);
		product.words[wordIndex] = static_cast<uint32_t>(sum);
		uint64_t carry = sum >> 32;

		sum = static_cast<uint64_t>(product.words[wordIndex + 1]) +
			static_cast<uint32_t>(value >> 32) + carry;
		product.words[wordIndex + 1] = static_cast<uint32_t>(sum);
		carry = sum >> 32;

		for (size_t index = wordIndex + 2;
			carry != 0 && index < 4; ++index)
		{
			sum = static_cast<uint64_t>(product.words[index]) + carry;
			product.words[index] = static_cast<uint32_t>(sum);
			carry = sum >> 32;
		}
	}

	WideProduct MultiplyMantissas(uint64_t first, uint64_t second)
	{
		const uint32_t firstLow = static_cast<uint32_t>(first);
		const uint32_t firstHigh = static_cast<uint32_t>(first >> 32);
		const uint32_t secondLow = static_cast<uint32_t>(second);
		const uint32_t secondHigh = static_cast<uint32_t>(second >> 32);

		WideProduct product = {};
		AddPartialProduct(product, 0,
			static_cast<uint64_t>(firstLow) * secondLow);
		AddPartialProduct(product, 1,
			static_cast<uint64_t>(firstLow) * secondHigh);
		AddPartialProduct(product, 1,
			static_cast<uint64_t>(firstHigh) * secondLow);
		AddPartialProduct(product, 2,
			static_cast<uint64_t>(firstHigh) * secondHigh);
		return product;
	}

	bool TestWideBit(const WideProduct& product, unsigned int bit)
	{
		return (product.words[bit / 32] &
			(static_cast<uint32_t>(1) << (bit % 32))) != 0;
	}

	bool HasWideBitsBelow(const WideProduct& product,
		unsigned int bitExclusive)
	{
		const unsigned int completeWords = bitExclusive / 32;
		for (unsigned int index = 0; index < completeWords; ++index)
			if (product.words[index] != 0)
				return true;

		const unsigned int remainingBits = bitExclusive % 32;
		if (remainingBits == 0)
			return false;
		const uint32_t mask =
			(static_cast<uint32_t>(1) << remainingBits) - 1;
		return (product.words[completeWords] & mask) != 0;
	}

	uint64_t ShiftWideRightTo64(const WideProduct& product,
		unsigned int shift)
	{
		const unsigned int wordIndex = shift / 32;
		const unsigned int wordShift = shift % 32;
		uint64_t pair = product.words[wordIndex];
		if (wordIndex + 1 < 4)
			pair |= static_cast<uint64_t>(
				product.words[wordIndex + 1]) << 32;

		uint64_t result = pair >> wordShift;
		if (wordShift != 0 && wordIndex + 2 < 4)
			result |= static_cast<uint64_t>(
				product.words[wordIndex + 2]) << (64 - wordShift);
		return result;
	}

	uint64_t MultiplyDoubleBits(uint64_t first, uint64_t second)
	{
		constexpr uint64_t kSignMask = 0x8000000000000000ull;
		constexpr uint64_t kFractionMask = 0x000FFFFFFFFFFFFFull;
		constexpr uint64_t kHiddenBit = 0x0010000000000000ull;
		constexpr uint64_t kInfinity = 0x7FF0000000000000ull;
		constexpr uint64_t kQuietNaN = 0x7FF8000000000000ull;

		const uint64_t sign = (first ^ second) & kSignMask;
		const unsigned int firstExponent =
			static_cast<unsigned int>((first >> 52) & 0x7FFu);
		const unsigned int secondExponent =
			static_cast<unsigned int>((second >> 52) & 0x7FFu);
		const uint64_t firstFraction = first & kFractionMask;
		const uint64_t secondFraction = second & kFractionMask;

		if ((firstExponent == 0x7FFu && firstFraction != 0) ||
			(secondExponent == 0x7FFu && secondFraction != 0))
			return kQuietNaN;

		const bool firstZero =
			firstExponent == 0 && firstFraction == 0;
		const bool secondZero =
			secondExponent == 0 && secondFraction == 0;
		if (firstExponent == 0x7FFu || secondExponent == 0x7FFu)
		{
			if (firstZero || secondZero)
				return kQuietNaN;
			return sign | kInfinity;
		}

		// These DirectPlay transform values are normal doubles or zero.
		// Treat a subnormal input as signed zero rather than executing an ARM
		// floating-point instruction that Box64 can incorrectly trap.
		if (firstExponent == 0 || secondExponent == 0)
			return sign;

		const uint64_t firstMantissa = kHiddenBit | firstFraction;
		const uint64_t secondMantissa = kHiddenBit | secondFraction;
		const WideProduct product =
			MultiplyMantissas(firstMantissa, secondMantissa);
		const bool highProductBit = TestWideBit(product, 105);
		const unsigned int shift = highProductBit ? 53 : 52;
		int resultExponent =
			static_cast<int>(firstExponent + secondExponent) -
			(highProductBit ? 1022 : 1023);
		if (resultExponent <= 0)
			return sign;
		if (resultExponent >= 0x7FF)
			return sign | kInfinity;

		uint64_t resultMantissa =
			ShiftWideRightTo64(product, shift);
		const bool roundBit = TestWideBit(product, shift - 1);
		const bool stickyBits = HasWideBitsBelow(product, shift - 1);
		if (roundBit && (stickyBits || (resultMantissa & 1) != 0))
		{
			++resultMantissa;
			if ((resultMantissa & (kHiddenBit << 1)) != 0)
			{
				resultMantissa >>= 1;
				if (++resultExponent >= 0x7FF)
					return sign | kInfinity;
			}
		}

		return sign |
			(static_cast<uint64_t>(resultExponent) << 52) |
			(resultMantissa & kFractionMask);
	}

	__declspec(noinline) void __cdecl ChaseSafeProduct(
		void* output, const void* first, const void* second)
	{
		uint64_t firstBits = 0;
		uint64_t secondBits = 0;
		memcpy(&firstBits, first, sizeof(firstBits));
		memcpy(&secondBits, second, sizeof(secondBits));
		const uint64_t result =
			MultiplyDoubleBits(firstBits, secondBits);
		memcpy(output, &result, sizeof(result));
	}

	// Chase's DirectPlay-era transform helper repeatedly faults in Box64's
	// x87 path at game.exe+0xBAF77. Perform the same four IEEE-754 products
	// using integer operations, write the exact stack temporaries consumed by
	// the rest of the original function, and then continue at +0xBAF8E.
	// This avoids changing the game executable on disk or weakening floating
	// point behavior globally for unrelated titles.
	__declspec(naked) void ChaseProductsPortable()
	{
		__asm
		{
			push ebx
			push esi
			mov esi, dword ptr [ebp + 8]
			push edi
			push ecx
			mov edi, eax
			mov ebx, edx

			push ebx
			push edi
			lea eax, [ebp - 20h]
			push eax
			call ChaseSafeProduct
			add esp, 12

			lea eax, [ebx + 8]
			push eax
			push edi
			lea eax, [ebp - 18h]
			push eax
			call ChaseSafeProduct
			add esp, 12

			lea eax, [edi + 8]
			push eax
			push ebx
			lea eax, [ebp - 10h]
			push eax
			call ChaseSafeProduct
			add esp, 12

			lea eax, [ebx + 8]
			push eax
			lea eax, [edi + 8]
			push eax
			lea eax, [ebp - 8]
			push eax
			call ChaseSafeProduct
			add esp, 12

			pop ecx
			xor ebx, ebx
			jmp dword ptr [g_chaseFpuContinuation]
		}
	}

	bool InstallChaseProductPatch()
	{
		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		if (imageBase == 0)
			return false;

		BYTE* patchSite = reinterpret_cast<BYTE*>(
			imageBase + 0x000BAF66);
		const BYTE expectedBytes[] = {
			0xDD, 0x00, 0xDC, 0x0A, 0x53
		};
		if (memcmp(patchSite, expectedBytes, sizeof(expectedBytes)) != 0)
			return false;

		g_chaseFpuContinuation = imageBase + 0x000BAF8E;
		const uintptr_t hookAddress =
			reinterpret_cast<uintptr_t>(&ChaseProductsPortable);
		const LONG relativeJump = static_cast<LONG>(
			hookAddress - reinterpret_cast<uintptr_t>(patchSite) - 5);

		DWORD oldProtection = 0;
		if (!VirtualProtect(patchSite, 5, PAGE_EXECUTE_READWRITE,
			&oldProtection))
			return false;

		patchSite[0] = 0xE9;
		memcpy(patchSite + 1, &relativeJump, sizeof(relativeJump));
		FlushInstructionCache(GetCurrentProcess(), patchSite, 5);

		DWORD ignoredProtection = 0;
		VirtualProtect(patchSite, 5, oldProtection, &ignoredProtection);
		InterlockedExchange(&g_productPatchInstalled, 1);
		return true;
	}

	HRESULT WINAPI ChaseDirectPlayHostAccept(
		void* peer,
		const void* applicationDescription,
		void* const* deviceAddresses,
		DWORD deviceAddressCount,
		const void* securityDescription,
		const void* credentials,
		void* playerContext,
		DWORD flags)
	{
		(void)peer;
		(void)applicationDescription;
		(void)deviceAddresses;
		(void)deviceAddressCount;
		(void)securityDescription;
		(void)credentials;
		(void)playerContext;
		(void)flags;
		const HRESULT result = S_OK;

		const LONG hostCount =
			InterlockedIncrement(&g_directPlayHostCount);
		if (hostCount <= 8 || (hostCount & (hostCount - 1)) == 0)
		{
			char message[128] = {};
			sprintf_s(
				message,
				"directplay host=%ld result=0x%08X\n",
				hostCount,
				static_cast<unsigned int>(result));
			AppendDiagnostics(message);
		}

		// Wine's dpnet Host stub schedules a callback through an invalid WOW64
		// exception frame about one second later. Chase does not consume any
		// state produced by that stub in single-cabinet mode: its surrounding
		// wrapper and the local-group compatibility path provide the game-side
		// state. Do not enter Wine's Host implementation at all.
		return S_OK;
	}

	__declspec(naked) void ChaseDirectPlayHostAcceptThunk()
	{
		__asm
		{
			push ebp
			mov ebp, esp
			push dword ptr [ebp + 24h]
			push dword ptr [ebp + 20h]
			push dword ptr [ebp + 1Ch]
			push dword ptr [ebp + 18h]
			push dword ptr [ebp + 14h]
			push dword ptr [ebp + 10h]
			push dword ptr [ebp + 0Ch]
			push dword ptr [ebp + 08h]
			call ChaseDirectPlayHostAccept
			test eax, eax
			mov esp, ebp
			pop ebp
			ret 20h
		}
	}

	bool InstallChaseDirectPlayHostPatch()
	{
		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		if (imageBase == 0)
			return false;

		// The compatibility DLL is loaded while the game primary thread is
		// suspended, before OpenParrot's delayed Chase initializer runs. Cover
		// that early window by replacing the original indirect Host call and its
		// TEST with a five-byte call to the accepting thunk. The thunk deliberately
		// avoids Wine's dpnet Host stub: it schedules a WOW64 callback through an
		// invalid exception frame roughly one second later. Chase's wrapper and
		// the local-group patch below provide the single-cabinet state instead.
		BYTE* patchSite = reinterpret_cast<BYTE*>(
			imageBase + 0x000624F0);
		const BYTE expectedBytes[] = {
			0xFF, 0x52, 0x24, 0x85, 0xC0
		};
		if (memcmp(patchSite, expectedBytes, sizeof(expectedBytes)) != 0)
			return false;

		const uintptr_t hookAddress =
			reinterpret_cast<uintptr_t>(
				&ChaseDirectPlayHostAcceptThunk);
		const LONG relativeCall = static_cast<LONG>(
			hookAddress - reinterpret_cast<uintptr_t>(patchSite) - 5);

		DWORD oldProtection = 0;
		if (!VirtualProtect(patchSite, sizeof(expectedBytes),
			PAGE_EXECUTE_READWRITE,
			&oldProtection))
			return false;

		patchSite[0] = 0xE8;
		memcpy(patchSite + 1, &relativeCall, sizeof(relativeCall));
		FlushInstructionCache(
			GetCurrentProcess(), patchSite, sizeof(expectedBytes));

		DWORD ignoredProtection = 0;
		VirtualProtect(patchSite, sizeof(expectedBytes),
			oldProtection, &ignoredProtection);
		InterlockedExchange(&g_directPlayHostPatchInstalled, 1);
		return true;
	}

	int __fastcall ChaseInitializeLocalGroups(void* wrapper, void*)
	{
		const LONG groupCount =
			InterlockedIncrement(&g_directPlayGroupCount);
		char message[96] = {};
		sprintf_s(message, "directplay local_groups=%ld\n", groupCount);
		AppendDiagnostics(message);

		if (wrapper == nullptr)
			return 0;
		BYTE* network = *reinterpret_cast<BYTE**>(wrapper);
		if (network == nullptr)
			return 0;

		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		if (imageBase == 0)
			return 0;
		const char* const* groupNames =
			reinterpret_cast<const char* const*>(
				imageBase + 0x001CADD4);

		for (DWORD groupIndex = 0; groupIndex < 8; ++groupIndex)
		{
			BYTE* groupRecord =
				network + 0x500 + groupIndex * 0x2C;
			*reinterpret_cast<DWORD*>(groupRecord) = groupIndex + 1;
			char* groupName =
				reinterpret_cast<char*>(groupRecord + sizeof(DWORD));
			const char* sourceName = groupNames[groupIndex];
			if (sourceName == nullptr)
				groupName[0] = '\0';
			else
				strncpy_s(groupName, 0x28, sourceName, _TRUNCATE);
		}
		return 1;
	}

	bool InstallChaseDirectPlayGroupPatch()
	{
		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		if (imageBase == 0)
			return false;

		BYTE* patchSite = reinterpret_cast<BYTE*>(
			imageBase + 0x00064F20);
		const BYTE expectedBytes[] = {
			0x83, 0xEC, 0x44, 0xA1, 0x60
		};
		if (memcmp(patchSite, expectedBytes, sizeof(expectedBytes)) != 0)
			return false;

		const uintptr_t hookAddress =
			reinterpret_cast<uintptr_t>(&ChaseInitializeLocalGroups);
		const LONG relativeJump = static_cast<LONG>(
			hookAddress - reinterpret_cast<uintptr_t>(patchSite) - 5);

		DWORD oldProtection = 0;
		if (!VirtualProtect(patchSite, 5, PAGE_EXECUTE_READWRITE,
			&oldProtection))
			return false;

		patchSite[0] = 0xE9;
		memcpy(patchSite + 1, &relativeJump, sizeof(relativeJump));
		FlushInstructionCache(GetCurrentProcess(), patchSite, 5);

		DWORD ignoredProtection = 0;
		VirtualProtect(patchSite, 5, oldProtection, &ignoredProtection);
		InterlockedExchange(&g_directPlayGroupPatchInstalled, 1);
		return true;
	}

	bool InstallChaseDirectPlayRuntimeIdentityPatch()
	{
		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		if (imageBase == 0)
			return false;

		// The earlier SetPeerInfo call supplies the cabinet identity used while
		// hosting. This later call only publishes a one-byte runtime state value.
		// Wine's dpnet implementation reports it as a semi-stub and schedules an
		// invalid callback frame under Box64. The game ignores the HRESULT, so
		// omit only this second call and preserve the rest of the initializer.
		BYTE* patchSite = reinterpret_cast<BYTE*>(
			imageBase + 0x00065D95);
		const BYTE expectedBytes[] = {
			0xE8, 0x96, 0xCA, 0xFF, 0xFF
		};
		if (memcmp(patchSite, expectedBytes, sizeof(expectedBytes)) != 0)
			return false;

		DWORD oldProtection = 0;
		if (!VirtualProtect(patchSite, sizeof(expectedBytes),
			PAGE_EXECUTE_READWRITE, &oldProtection))
			return false;

		// The skipped thiscall normally returns with `ret 4`, removing the
		// single state-pointer argument pushed immediately before this call.
		// Preserve that stack cleanup explicitly; five NOPs leak four bytes and
		// eventually trip the caller's stack cookie as a buffer overrun.
		const BYTE skipCallAndPopArgument[] = {
			0x83, 0xC4, 0x04,       // add esp,4
			0x90, 0x90
		};
		memcpy(
			patchSite,
			skipCallAndPopArgument,
			sizeof(skipCallAndPopArgument));
		FlushInstructionCache(
			GetCurrentProcess(), patchSite, sizeof(expectedBytes));

		DWORD ignoredProtection = 0;
		VirtualProtect(patchSite, sizeof(expectedBytes),
			oldProtection, &ignoredProtection);
		InterlockedExchange(
			&g_directPlayRuntimeIdentityPatchInstalled, 1);
		return true;
	}

	// DirectPlay group-removal callbacks can race the local single-cabinet
	// shutdown path. The original helper assumes its network object is always
	// present and immediately reads [ecx+0x6E8]. Wine can deliver the callback
	// after Chase has cleared that object, so make a null cleanup idempotent and
	// otherwise execute the untouched original method.
	__declspec(naked) void ChaseDirectPlayRemoveGroupGuard()
	{
		__asm
		{
			test ecx, ecx
			jz noNetworkObject
			mov eax, dword ptr [esp + 8]
			cmp eax, 8
			jmp dword ptr [g_directPlayRemoveGroupContinuation]

		noNetworkObject:
			xor eax, eax
			ret 8
		}
	}

	bool InstallChaseDirectPlayNullGroupPatch()
	{
		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		if (imageBase == 0)
			return false;

		BYTE* patchSite = reinterpret_cast<BYTE*>(
			imageBase + 0x00062B20);
		const BYTE expectedBytes[] = {
			0x8B, 0x44, 0x24, 0x08, 0x83, 0xF8, 0x08
		};
		if (memcmp(patchSite, expectedBytes, sizeof(expectedBytes)) != 0)
			return false;

		g_directPlayRemoveGroupContinuation =
			imageBase + 0x00062B27;
		const uintptr_t hookAddress =
			reinterpret_cast<uintptr_t>(
				&ChaseDirectPlayRemoveGroupGuard);
		const LONG relativeJump = static_cast<LONG>(
			hookAddress - reinterpret_cast<uintptr_t>(patchSite) - 5);

		DWORD oldProtection = 0;
		if (!VirtualProtect(patchSite, sizeof(expectedBytes),
			PAGE_EXECUTE_READWRITE, &oldProtection))
			return false;

		patchSite[0] = 0xE9;
		memcpy(patchSite + 1, &relativeJump, sizeof(relativeJump));
		memset(patchSite + 5, 0x90, sizeof(expectedBytes) - 5);
		FlushInstructionCache(
			GetCurrentProcess(), patchSite, sizeof(expectedBytes));

		DWORD ignoredProtection = 0;
		VirtualProtect(patchSite, sizeof(expectedBytes),
			oldProtection, &ignoredProtection);
		InterlockedExchange(
			&g_directPlayNullGroupPatchInstalled, 1);
		return true;
	}

	void AppendDiagnostics(const char* message)
	{
		char loggingEnabled[2] = {};
		if (GetEnvironmentVariableA(
				"TP_ANDROID_DEBUG_LOGGING",
				loggingEnabled,
				static_cast<DWORD>(sizeof(loggingEnabled))) == 0 ||
			loggingEnabled[0] != '1')
			return;

		HANDLE logFile = CreateFileA(
			"E:\\TeknoParrotRuntime\\OpenParrotWin32\\ChaseFpuHelper.log",
			FILE_APPEND_DATA,
			FILE_SHARE_READ | FILE_SHARE_WRITE,
			nullptr,
			OPEN_ALWAYS,
			FILE_ATTRIBUTE_NORMAL,
			nullptr);
		if (logFile == INVALID_HANDLE_VALUE)
			return;

		DWORD bytesWritten = 0;
		WriteFile(logFile, message,
			static_cast<DWORD>(strlen(message)), &bytesWritten, nullptr);
		CloseHandle(logFile);
	}

	LONG CALLBACK RecoverChaseFpuTrap(PEXCEPTION_POINTERS exceptionPointers)
	{
		if (exceptionPointers == nullptr ||
			exceptionPointers->ExceptionRecord == nullptr ||
			exceptionPointers->ContextRecord == nullptr ||
			exceptionPointers->ExceptionRecord->ExceptionCode !=
				kFloatMultipleTraps)
			return EXCEPTION_CONTINUE_SEARCH;

		const uintptr_t imageBase = reinterpret_cast<uintptr_t>(
			GetModuleHandleW(nullptr));
		CONTEXT* context = exceptionPointers->ContextRecord;
		const uintptr_t exceptionIp = static_cast<uintptr_t>(context->Eip);
		size_t matchedSiteIndex =
			sizeof(kChaseFpuSites) / sizeof(kChaseFpuSites[0]);
		for (size_t siteIndex = 0;
			siteIndex < sizeof(kChaseFpuSites) / sizeof(kChaseFpuSites[0]);
			++siteIndex)
		{
			const ChaseFpuSite& site = kChaseFpuSites[siteIndex];
			if (imageBase != 0 &&
				exceptionIp == imageBase + site.rva &&
				memcmp(reinterpret_cast<const void*>(exceptionIp),
					site.bytes, site.length) == 0)
			{
				matchedSiteIndex = siteIndex;
				break;
			}
		}
		if (matchedSiteIndex ==
			sizeof(kChaseFpuSites) / sizeof(kChaseFpuSites[0]))
			return EXCEPTION_CONTINUE_SEARCH;

		// Box64 can preserve pending x87 status flags while Chase temporarily
		// changes its control word. The next waiting x87 instruction then raises
		// STATUS_FLOAT_MULTIPLE_TRAPS even though the FMUL operands are valid.
		// Clear the pending state and mask x87 exceptions in both x86 CONTEXT
		// representations. Leave EIP and the x87 stack unchanged so Wine
		// re-executes the original FMUL and produces the real game result.
		_clearfp();
		unsigned int currentControlWord = 0;
		_controlfp_s(&currentControlWord, _MCW_EM, _MCW_EM);

		context->FloatSave.ControlWord |= 0x3F;
		context->FloatSave.StatusWord &= ~0xFFu;

		BYTE* extendedRegisters = context->ExtendedRegisters;
		*reinterpret_cast<WORD*>(extendedRegisters) |= 0x3F;
		WORD* savedStatus =
			reinterpret_cast<WORD*>(extendedRegisters + 2);
		*savedStatus &= static_cast<WORD>(~0xFFu);
		DWORD* savedMxCsr =
			reinterpret_cast<DWORD*>(extendedRegisters + 24);
		*savedMxCsr = (*savedMxCsr | 0x1F80u) & ~0x3Fu;

		const LONG recoveryNumber =
			InterlockedIncrement(&g_recoveryCount);
		const LONG siteRecoveryNumber =
			InterlockedIncrement(&g_siteRecoveryCounts[matchedSiteIndex]);
		if (recoveryNumber <= 8 ||
			(recoveryNumber & (recoveryNumber - 1)) == 0)
		{
			char message[256] = {};
			sprintf_s(message,
				"tick=%llu total=%ld site=0x%08Ix site_count=%ld\n",
				static_cast<unsigned long long>(GetTickCount64()),
				recoveryNumber,
				kChaseFpuSites[matchedSiteIndex].rva,
				siteRecoveryNumber);
			AppendDiagnostics(message);
		}
		return EXCEPTION_CONTINUE_EXECUTION;
	}
}

extern "C" __declspec(dllexport) LONG WINAPI ChaseFpuRecoveryCount()
{
	return InterlockedCompareExchange(&g_recoveryCount, 0, 0);
}

extern "C" __declspec(dllexport) LONG WINAPI ChaseFpuSse2PatchInstalled()
{
	return InterlockedCompareExchange(&g_productPatchInstalled, 0, 0);
}

extern "C" __declspec(dllexport) LONG WINAPI ChaseDirectPlayHostPatchInstalled()
{
	return InterlockedCompareExchange(
		&g_directPlayHostPatchInstalled, 0, 0);
}

extern "C" __declspec(dllexport) LONG WINAPI ChaseDirectPlayGroupPatchInstalled()
{
	return InterlockedCompareExchange(
		&g_directPlayGroupPatchInstalled, 0, 0);
}

extern "C" __declspec(dllexport) LONG WINAPI
ChaseDirectPlayRuntimeIdentityPatchInstalled()
{
	return InterlockedCompareExchange(
		&g_directPlayRuntimeIdentityPatchInstalled, 0, 0);
}

extern "C" __declspec(dllexport) LONG WINAPI
ChaseDirectPlayNullGroupPatchInstalled()
{
	return InterlockedCompareExchange(
		&g_directPlayNullGroupPatchInstalled, 0, 0);
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
	if (reason == DLL_PROCESS_ATTACH)
	{
		DisableThreadLibraryCalls(instance);
		const bool productInstalled = InstallChaseProductPatch();
		const bool hostInstalled = InstallChaseDirectPlayHostPatch();
		const bool groupsInstalled = InstallChaseDirectPlayGroupPatch();
		const bool runtimeIdentityInstalled =
			InstallChaseDirectPlayRuntimeIdentityPatch();
		const bool nullGroupInstalled =
			InstallChaseDirectPlayNullGroupPatch();
		g_exceptionHandler =
			AddVectoredExceptionHandler(1, RecoverChaseFpuTrap);

		char message[256] = {};
		sprintf_s(message,
			"install product=%d host=%d groups=%d identity=%d "
			"null_group=%d veh=%d\n",
			productInstalled ? 1 : 0,
			hostInstalled ? 1 : 0,
			groupsInstalled ? 1 : 0,
			runtimeIdentityInstalled ? 1 : 0,
			nullGroupInstalled ? 1 : 0,
			g_exceptionHandler != nullptr ? 1 : 0);
		AppendDiagnostics(message);
	}
	else if (reason == DLL_PROCESS_DETACH &&
		g_exceptionHandler != nullptr)
	{
		RemoveVectoredExceptionHandler(g_exceptionHandler);
		g_exceptionHandler = nullptr;
	}
	return TRUE;
}
