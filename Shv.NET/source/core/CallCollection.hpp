#pragma once
#include "NativeHashes.hpp"
#include "Native.hpp"

using namespace System::Collections::Generic;

namespace GTA
{
	namespace Native
	{
		ref struct NativeTask;

		public ref class CallCollection
		{
		public:
			CallCollection()
			{
				_tasks = gcnew List<NativeTask^>();
			}

			void Call(Hash hash, ... array<InputArgument ^> ^arguments);
			int Execute();
		private:
			List<NativeTask^> ^_tasks;
		};
	}
}