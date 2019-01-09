#include <Windows.h>
#include <vcclr.h>

using namespace System;
using namespace System::IO;
using namespace System::Collections::Generic;

namespace PSRT::Astra::Native {

	public ref class ComparePhaseInternals {
	public:
		ref struct Patch {
		public:
			Int64 LastWriteTime;
			bool ShouldUpdate;
		};

	public:
		static void PreProcessPatches(Dictionary<String ^, Patch ^> ^patches, String ^pso2BinDirectory) {
			pin_ptr<const wchar_t> searchPath = PtrToStringChars(Path::Combine(pso2BinDirectory, "data\\win32\\*"));
			WIN32_FIND_DATA findData;
			auto handle = FindFirstFile(searchPath, &findData);
			if (handle == INVALID_HANDLE_VALUE) {
				return;
			}

			try
			{
				do
				{
					if (findData.dwFileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_HIDDEN))
						continue;

					auto fileName = "data/win32/" + gcnew String(findData.cFileName) + ".pat";
					if (!patches->ContainsKey(fileName)) {
						continue;
					}

					auto lastWriteTimeLong = static_cast<System::Int64>(findData.ftLastWriteTime.dwHighDateTime) << 32 | findData.ftLastWriteTime.dwLowDateTime;

					auto %patch = patches[fileName];
					if (patch->LastWriteTime == lastWriteTimeLong)
						patch->ShouldUpdate = false;
				} while (FindNextFile(handle, &findData));
			}
			finally
			{
				FindClose(handle);
			}
		}

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