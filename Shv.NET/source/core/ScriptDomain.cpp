/**
 * Copyright (C) 2015 crosire
 *
 * This software is  provided 'as-is', without any express  or implied  warranty. In no event will the
 * authors be held liable for any damages arising from the use of this software.
 * Permission  is granted  to anyone  to use  this software  for  any  purpose,  including  commercial
 * applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *
 *   1. The origin of this software must not be misrepresented; you must not claim that you  wrote the
 *      original  software. If you use this  software  in a product, an  acknowledgment in the product
 *      documentation would be appreciated but is not required.
 *   2. Altered source versions must  be plainly  marked as such, and  must not be  misrepresented  as
 *      being the original software.
 *   3. This notice may not be removed or altered from any source distribution.
 */

#include "ScriptDomain.hpp"

using namespace System;
using namespace System::Threading;
using namespace System::Reflection;
using namespace System::Collections::Generic;
namespace WinForms = System::Windows::Forms;

namespace
{
	inline void SignalAndWait(AutoResetEvent ^toSignal, AutoResetEvent ^toWaitOn)
	{
		toSignal->Set();
		toWaitOn->WaitOne();
	}
	inline bool SignalAndWait(AutoResetEvent ^toSignal, AutoResetEvent ^toWaitOn, int timeout)
	{
		toSignal->Set();
		return toWaitOn->WaitOne(timeout);
	}
}

namespace GTA
{
	String ^GetScriptSupportURL(Type ^scriptType)
	{
		for each (ScriptAttributes ^attribute in scriptType->GetCustomAttributes(ScriptAttributes::typeid, true))
		{
			if (!String::IsNullOrEmpty(attribute->SupportURL))
			{
				return attribute->SupportURL;
			}
		}

		return nullptr;
	}

	void Log(String ^logLevel, ... array<String ^> ^message)
	{
		DateTime now = DateTime::Now;

		String ^logpath = IO::Path::Combine(IO::Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location), "..\\logs\\ScriptHookVDotNet.log");
		logpath = logpath->Insert(logpath->IndexOf(".log"), "-" + now.ToString("yyyy-MM-dd"));

		try
		{
			auto fs = gcnew IO::FileStream(logpath, IO::FileMode::Append, IO::FileAccess::Write, IO::FileShare::Read);
			auto sw = gcnew IO::StreamWriter(fs);

			try
			{
				sw->Write(String::Concat("[", now.ToString("HH:mm:ss"), "] ", logLevel, " "));

				for each (String ^string in message)
				{
					sw->Write(string);
				}

				sw->WriteLine();
			}
			finally
			{
				sw->Close();
				fs->Close();
			}
		}
		catch (...)
		{
			return;
		}
	}
	Assembly ^HandleResolve(Object ^sender, ResolveEventArgs ^args)
	{
		auto assembly = Script::typeid->Assembly;
		auto assemblyName = gcnew AssemblyName(args->Name);

		if (assemblyName->Name->StartsWith("ScriptHookVDotNet", StringComparison::CurrentCultureIgnoreCase))
		{
			if (assemblyName->Version->Major != assembly->GetName()->Version->Major)
			{
				Log("[WARNING]", "A script references v", assemblyName->Version->ToString(3), " which may not be compatible with the current v" + assembly->GetName()->Version->ToString(3), " and was therefore ignored.");
			}
			return assembly;
		}

		return nullptr;
	}
	void HandleUnhandledException(Object ^sender, UnhandledExceptionEventArgs ^args)
	{
		if (!args->IsTerminating)
		{
			Log("[ERROR]", "Caught unhandled exception:", Environment::NewLine, args->ExceptionObject->ToString());
		}
		else
		{
			Log("[ERROR]", "Caught fatal unhandled exception:", Environment::NewLine, args->ExceptionObject->ToString());
		}

		//if (sender == nullptr || !Script::typeid->IsInstanceOfType(sender))
		//{
		//	return;
		//}

		//auto scriptType = sender->GetType();

		//Log("[INFO]", "The exception was thrown while executing the script '", scriptType->FullName, "'.");

		//String ^supportURL = GetScriptSupportURL(scriptType);

		//if (supportURL != nullptr)
		//{
		//	Log("[INFO]", "Please check the following site for support on the issue: ", supportURL);
		//}
	}

	ScriptDomain::ScriptDomain() : _appdomain(System::AppDomain::CurrentDomain), _executingThreadId(Thread::CurrentThread->ManagedThreadId)
	{
		sCurrentDomain = this;

		_appdomain->AssemblyResolve += gcnew ResolveEventHandler(&HandleResolve);
		_appdomain->UnhandledException += gcnew UnhandledExceptionEventHandler(&HandleUnhandledException);

		Log("[INFO]", "Created new script domain with v", ScriptDomain::typeid->Assembly->GetName()->Version->ToString(3), ".");

		//_console = gcnew ConsoleScript();
	}
	ScriptDomain::~ScriptDomain()
	{
		CleanupStrings();
	}

	ScriptDomain ^ScriptDomain::Load(String ^path)
	{
		//if (!IO::Path::IsPathRooted(path))
		//{
		//	path = IO::Path::Combine(IO::Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location), path);
		//}

		path = IO::Path::GetFullPath(path);

		//// Clear log
		//String ^logPath = IO::Path::ChangeExtension(Assembly::GetExecutingAssembly()->Location, ".log");

		//try
		//{
		//	IO::File::WriteAllText(logPath, String::Empty);
		//}
		//catch (...) { }

		// Create AppDomain
		auto setup = gcnew AppDomainSetup();
		setup->ApplicationBase = path;
		setup->ShadowCopyFiles = "false";
		setup->ShadowCopyDirectories = path;

		auto appdomain = System::AppDomain::CreateDomain("ScriptDomain_" + (path->GetHashCode() * Environment::TickCount).ToString("X"), nullptr, setup, gcnew Security::PermissionSet(Security::Permissions::PermissionState::Unrestricted));
		appdomain->InitializeLifetimeService();

		ScriptDomain ^scriptdomain = nullptr;

		try
		{
			scriptdomain = static_cast<ScriptDomain ^>(appdomain->CreateInstanceFromAndUnwrap(ScriptDomain::typeid->Assembly->Location, ScriptDomain::typeid->FullName));
		}
		catch (Exception ^ex)
		{
			Log("[ERROR]", "Failed to create script domain':", Environment::NewLine, ex->ToString());

			System::AppDomain::Unload(appdomain);

			return nullptr;
		}

		Log("[INFO]", "Loading scripts from '", path, "' ...");

		if (IO::Directory::Exists(path))
		{
			//auto filenameScripts = gcnew List<String ^>();
			auto filenameAssemblies = gcnew List<String ^>();

			try
			{
				//filenameScripts->AddRange(IO::Directory::GetFiles(path, "*.vb", IO::SearchOption::AllDirectories));
				//filenameScripts->AddRange(IO::Directory::GetFiles(path, "*.cs", IO::SearchOption::AllDirectories));
				filenameAssemblies->AddRange(IO::Directory::GetFiles(path, "*.dll", IO::SearchOption::AllDirectories));
			}
			catch (Exception ^ex)
			{
				Log("[ERROR]", "Failed to reload scripts:", Environment::NewLine, ex->ToString());

				System::AppDomain::Unload(appdomain);

				return nullptr;
			}

			for (int i = 0; i < filenameAssemblies->Count; i++)
			{
				auto filename = filenameAssemblies[i];
				try {
					auto assemblyName = AssemblyName::GetAssemblyName(filename);

					if (assemblyName->Name->StartsWith("ScriptHookVDotNet", StringComparison::OrdinalIgnoreCase))
					{
						Log("[WARNING]", "Removing assembly file '", IO::Path::GetFileName(filename), "'.");

						filenameAssemblies->RemoveAt(i--);

						try
						{
							IO::File::Delete(filename);
						}
						catch (Exception^ ex)
						{
							Log("[ERROR]", "Failed to delete assembly file:", Environment::NewLine, ex->ToString());
						}
					}
				}
				catch (Exception^ ex) {
					Log("[ERROR]", "Failed to load " + filename + ", possibly a non .Net assembly, skipping.", Environment::NewLine, ex->ToString());
				}
			}

			//for each (String ^filename in filenameScripts)
			//{
			//	scriptdomain->LoadScript(filename);
			//}
			for each (String ^filename in filenameAssemblies)
			{
				if (filename->Contains("GTANetwork.dll") || filename->Contains("NativeUI.dll"))
				{
					scriptdomain->LoadAssembly(filename);
				}
			}
		}
		else
		{
			Log("[ERROR]", "Failed to reload scripts because the directory is missing.");
		}

		return scriptdomain;
	}
	bool ScriptDomain::LoadScript(String ^filename)
	{
		auto compilerOptions = gcnew CodeDom::Compiler::CompilerParameters();
		compilerOptions->CompilerOptions = "/optimize";
		compilerOptions->GenerateInMemory = true;
		compilerOptions->IncludeDebugInformation = true;
		compilerOptions->ReferencedAssemblies->Add("System.dll");
		compilerOptions->ReferencedAssemblies->Add("System.Core.dll");
		compilerOptions->ReferencedAssemblies->Add("System.Drawing.dll");
		compilerOptions->ReferencedAssemblies->Add("System.Windows.Forms.dll");
		compilerOptions->ReferencedAssemblies->Add("System.XML.dll");
		compilerOptions->ReferencedAssemblies->Add("System.XML.Linq.dll");
		compilerOptions->ReferencedAssemblies->Add(GTA::Script::typeid->Assembly->Location);

		String ^extension = IO::Path::GetExtension(filename);
		CodeDom::Compiler::CodeDomProvider ^compiler = nullptr;

		if (extension->Equals(".cs", StringComparison::InvariantCultureIgnoreCase))
		{
			compiler = gcnew Microsoft::CSharp::CSharpCodeProvider();
			compilerOptions->CompilerOptions += " /unsafe";
		}
		else if (extension->Equals(".vb", StringComparison::InvariantCultureIgnoreCase))
		{
			compiler = gcnew Microsoft::VisualBasic::VBCodeProvider();
		}
		else
		{
			return false;
		}

		CodeDom::Compiler::CompilerResults ^compilerResult = compiler->CompileAssemblyFromFile(compilerOptions, filename);

		if (!compilerResult->Errors->HasErrors)
		{
			Log("[INFO]", "Successfully compiled '", IO::Path::GetFileName(filename), "'.");

			return LoadAssembly(filename, compilerResult->CompiledAssembly);
		}
		else
		{
			auto errors = gcnew Text::StringBuilder();

			for each (CodeDom::Compiler::CompilerError ^error in compilerResult->Errors)
			{
				errors->Append("   at line ");
				errors->Append(error->Line);
				errors->Append(": ");
				errors->Append(error->ErrorText);
				errors->AppendLine();
			}

			Log("[ERROR]", "Failed to compile '", IO::Path::GetFileName(filename), "' with ", compilerResult->Errors->Count.ToString(), " error(s):", Environment::NewLine, errors->ToString());

			return false;
		}
	}
	bool ScriptDomain::LoadAssembly(String ^filename)
	{
		Log("[INFO]", "Reading assembly '", IO::Path::GetFileName(filename), "' ...");

		Assembly ^assembly = nullptr;

		try
		{
			//assembly = Assembly::Load(IO::File::ReadAllBytes(filename));
			assembly = Assembly::LoadFrom(filename);
		}
		//catch (BadImageFormatException ^) { return false; }
		catch (Exception ^ex)
		{
			Log("[ERROR]", "Failed to load assembly '", IO::Path::GetFileName(filename), "':", Environment::NewLine, ex->ToString());
			return false;
		}

		return LoadAssembly(filename, assembly);
	}

	bool ScriptDomain::LoadAssembly(String ^filename, Assembly ^assembly)
	{
		//String ^version = (IO::Path::GetExtension(filename) == ".dll" ? (" v" + assembly->GetName()->Version->ToString(3)) : String::Empty);
		unsigned int count = 0;

		try
		{
			for each (auto type in assembly->GetTypes())
			{
				if (!type->IsSubclassOf(Script::typeid))
				{
					continue;
				}

				count++;
				_scriptTypes->Add(gcnew Tuple<String ^, Type ^>(filename, type));
			}
		}
		catch (ReflectionTypeLoadException ^ex)
		{
			for each (auto exs in ex->LoaderExceptions)
			{
				Log("[ERROR]", Environment::NewLine, exs->ToString());
			}
			Log("[ERROR]", "Failed to load assembly '", IO::Path::GetFileName(filename), "':", Environment::NewLine, ex->ToString());
			return false;
		}
		catch (Exception ^ex)
		{
			Log("[ERROR]", "Failed to load assembly '", IO::Path::GetFileName(filename), "':", Environment::NewLine, ex->ToString());

			return false;
		}

		Log("[INFO]", "Found ", count.ToString(), " script(s) in '", IO::Path::GetFileName(filename), "'.");

		return count != 0;
	}

	void ScriptDomain::Unload(ScriptDomain ^%domain)
	{
		Log("[INFO]", "Unloading script domain ...");

		domain->Abort();

		System::AppDomain ^appdomain = domain->AppDomain;

		delete domain;

		try
		{
			System::AppDomain::Unload(appdomain);
		}
		catch (Exception ^ex)
		{
			Log("[ERROR]", "Failed to unload deleted script domain:", Environment::NewLine, ex->ToString());
		}

		domain = nullptr;

		GC::Collect();
	}
	GTA::Script ^ScriptDomain::InstantiateScript(Type ^scriptType)
	{
		if (!scriptType->IsSubclassOf(Script::typeid))
		{
			return nullptr;
		}

		Log("[INFO]", "Instantiating script '", scriptType->FullName, "' in script domain '", Name, "' ...");

		try
		{
			return static_cast<GTA::Script ^>(Activator::CreateInstance(scriptType));
		}
		catch (MissingMethodException ^)
		{
			Log("[ERROR]", "Failed to instantiate script '", scriptType->FullName, "' because no public default constructor was found.");
		}
		catch (TargetInvocationException ^ex)
		{
			Log("[ERROR]", "Failed to instantiate script '", scriptType->FullName, "' because constructor threw an exception:", Environment::NewLine, ex->InnerException->ToString());
		}
		catch (Exception ^ex)
		{
			Log("[ERROR]", "Failed to instantiate script '", scriptType->FullName, "':", Environment::NewLine, ex->ToString());
		}

		return nullptr;
	}

	bool SortScripts(List<Tuple<String ^, Type ^> ^> ^%scriptTypes)
	{
		auto graph = gcnew Dictionary<Tuple<String ^, Type ^> ^, List<Type ^> ^>();

		for each (auto scriptType in scriptTypes)
		{
			auto dependencies = gcnew List<Type ^>();

			for each (RequireScript ^attribute in static_cast<MemberInfo ^>(scriptType->Item2)->GetCustomAttributes(RequireScript::typeid, true))
			{
				dependencies->Add(attribute->_dependency);
			}

			graph->Add(scriptType, dependencies);
		}

		auto result = gcnew List<Tuple<String ^, Type ^> ^>(graph->Count);

		while (graph->Count > 0)
		{
			Tuple<String ^, Type ^> ^scriptype = nullptr;

			for each (auto item in graph)
			{
				if (item.Value->Count == 0)
				{
					scriptype = item.Key;
					break;
				}
			}

			if (scriptype == nullptr)
			{
				Log("[ERROR]", "Detected a circular script dependency. Aborting ...");
				return false;
			}

			result->Add(scriptype);
			graph->Remove(scriptype);

			for each (auto item in graph)
			{
				item.Value->Remove(scriptype->Item2);
			}
		}

		scriptTypes = result;

		return true;
	}
	void ScriptDomain::Start()
	{
		if (_runningScripts->Count != 0 || _scriptTypes->Count == 0)
		{
			return;
		}

		String ^assemblyPath = IO::Path::Combine(IO::Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location), "..\\bin\\scripts");
		String ^assemblyFilename = IO::Path::GetFileNameWithoutExtension(assemblyPath);

		for each (String ^path in IO::Directory::GetFiles(IO::Path::GetDirectoryName(assemblyPath), "*.log"))
		{
			if (!path->StartsWith(assemblyFilename))
			{
				continue;
			}

			try
			{
				TimeSpan logAge = DateTime::Now - DateTime::Parse(IO::Path::GetFileNameWithoutExtension(path)->Substring(path->IndexOf('-') + 1));

				// Delete logs older than 5 days
				if (logAge.Days >= 5)
				{
					IO::File::Delete(path);
				}
			}
			catch (...)
			{
				continue;
			}
		}

		Log("[INFO]", "Starting ", _scriptTypes->Count.ToString(), " script(s) ...");

		if (!SortScripts(_scriptTypes))
		{
			return;
		}

		for each (auto scriptType in _scriptTypes)
		{
			Script ^script = InstantiateScript(scriptType->Item2);

			if (Object::ReferenceEquals(script, nullptr))
			{
				continue;
			}

			script->_running = true;
			script->_filename = scriptType->Item1;
			script->_scriptdomain = this;
			script->_thread = gcnew Thread(gcnew ThreadStart(script, &Script::MainLoop));

			script->_thread->Start();

			Log("[INFO]", "Started script '", script->Name, "'.");

			_runningScripts->Add(script);
		}
	}

	void ScriptDomain::Abort()
	{
		Log("[INFO]", "Stopping ", _runningScripts->Count.ToString(), " script(s) ...");

		for each (Script ^script in _runningScripts)
		{
			script->Abort();

			delete script;
		}

		_scriptTypes->Clear();
		_runningScripts->Clear();

		GC::Collect();
	}
	void ScriptDomain::AbortScript(Script ^script)
	{
		if (Object::ReferenceEquals(script->_thread, nullptr))
		{
			return;
		}

		script->_running = false;

		script->_thread->Abort();
		script->_thread = nullptr;

		Log("[INFO]", "Aborted script '", script->Name, "'.");
	}

	void ScriptDomain::DoTick()
	{
		// Execute scripts
		for (int i = 0; i < _runningScripts->Count; i++)
		{
			GTA::Script ^script = _runningScripts[i];

			if (!script->_running)
			{
				continue;
			}

			_executingScript = script;

			while ((script->_running = SignalAndWait(script->_continueEvent, script->_waitEvent, 5000)) && _taskQueue->Count > 0)
			//while ((script->_running = SignalAndWait(script->_continueEvent, script->_waitEvent, 30000)) && _taskQueue->Count > 0)
			{
				_taskQueue->Dequeue()->Run();
			}

			_executingScript = nullptr;

			if (!script->_running)
			{
				Log("[ERROR]", "Script '", script->Name, "' is not responding! Aborting ...");

				AbortScript(script);
				continue;
			}
		}

		// Clean up pinned strings
		CleanupStrings();
	}

	void ScriptDomain::DoKeyboardMessage(WinForms::Keys key, bool status, bool statusCtrl, bool statusShift, bool statusAlt)
	{
		const int keycode = static_cast<int>(key);

		if (keycode < 0 || keycode >= _keyboardState->Length)
		{
			return;
		}

		_keyboardState[keycode] = status;

		if (_recordKeyboardEvents)
		{
			if (statusCtrl)
			{
				key = key | WinForms::Keys::Control;
			}
			if (statusShift)
			{
				key = key | WinForms::Keys::Shift;
			}
			if (statusAlt)
			{
				key = key | WinForms::Keys::Alt;
			}

			auto args = gcnew WinForms::KeyEventArgs(key);
			auto eventinfo = gcnew Tuple<bool, WinForms::KeyEventArgs ^>(status, args);

			for each (GTA::Script ^script in _runningScripts)
			{
				script->_keyboardEvents->Enqueue(eventinfo);
			}
		}
	}

	void ScriptDomain::DoD3DCall(void *swapchain)
	{
		int count = _hookedScripts->Count;
		for (int i = 0; i < count; i++)
		{
			_hookedScripts[i]->D3DHook(swapchain);
		}
	}

	void ScriptDomain::HookD3DScript(Script ^script)
	{
		if (!_hookedScripts->Contains(script))
		{
			_hookedScripts->Add(script);
		}
	}

	void ScriptDomain::PauseKeyboardEvents(bool pause)
	{
		_recordKeyboardEvents = !pause;
	}
	void ScriptDomain::ExecuteTask(IScriptTask ^task)
	{
		if (Thread::CurrentThread->ManagedThreadId == _executingThreadId)
		{
			task->Run();
		}
		else
		{
			_taskQueue->Enqueue(task);

			SignalAndWait(ExecutingScript->_waitEvent, ExecutingScript->_continueEvent);
		}
	}

	IntPtr ScriptDomain::PinString(String ^string)
	{
		const int size = Text::Encoding::UTF8->GetByteCount(string);
		IntPtr handle(new unsigned char[size + 1]());

		Runtime::InteropServices::Marshal::Copy(Text::Encoding::UTF8->GetBytes(string), 0, handle, size);

		_pinnedStrings->Add(handle);

		return handle;
	}
	void ScriptDomain::CleanupStrings()
	{
		for each (IntPtr handle in _pinnedStrings)
		{
			delete[] handle.ToPointer();
		}

		_pinnedStrings->Clear();
	}
	String ^ScriptDomain::LookupScriptFilename(Type ^type)
	{
		for each (auto scriptType in _scriptTypes)
		{
			if (scriptType->Item2 == type)
			{
				return scriptType->Item1;
			}
		}

		return String::Empty;
	}
	Object ^ScriptDomain::InitializeLifetimeService()
	{
		return nullptr;
	}
}
