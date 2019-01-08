#include <Windows.h>
#include <vcclr.h>

using namespace System;

namespace PSRT::Astra::Native {

	public ref class ComparePhaseInternals {
	public:
		static bool CompareFileTime(String ^filePath, System::Int64 cacheLastWriteTime) {
			pin_ptr<const wchar_t> filePathChars = PtrToStringChars(filePath);

			WIN32_FILE_ATTRIBUTE_DATA attributeData;
			auto result = GetFileAttributesEx(filePathChars, GetFileExInfoStandard, &attributeData);
			if (result == 0)
				return false;

			if ((attributeData.dwFileAttributes == -1) || (attributeData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) {
				return false;
			}

			auto lastWriteTimeLong = static_cast<System::Int64>(attributeData.ftLastWriteTime.dwHighDateTime) << 32 | attributeData.ftLastWriteTime.dwLowDateTime;
			return cacheLastWriteTime == lastWriteTimeLong;
		}
	};

}