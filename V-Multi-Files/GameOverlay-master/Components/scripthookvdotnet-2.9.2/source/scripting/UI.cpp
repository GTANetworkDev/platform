#include "UI.hpp"
#include "Native.hpp"
#include "ScriptDomain.hpp"
#include <Main.h>
#include <windows.h>
#include <STDInclude.h>

#include <string>
#include <msclr\marshal_cppstd.h>

namespace GTA
{
	
	using namespace System;
	using namespace System::Collections::Generic;
	using namespace System::Drawing;
	using namespace System::Runtime::InteropServices;

	Notification::Notification(int handle) : _handle(handle)
	{
	}

	void Notification::Hide()
	{
		Native::Function::Call(Native::Hash::_REMOVE_NOTIFICATION, _handle);
	}

	/*
	if (c.Bool != NULL)
	{
	Native::OutputArgument^ tmp = gcnew Native::OutputArgument(c.Bool);
	}
	else if (c.Int != NULL)
	{
	Native::OutputArgument^ tmp = gcnew Native::OutputArgument(c.Int);
	}
	else if (c.String.size() >= 1)
	{
	Native::OutputArgument^ tmp = gcnew Native::OutputArgument(gcnew System::String(c.String.c_str()));
	}*/



	
	array<Native::OutputArgument^> ^CEF::GetEventTriggerParams()
	{
		List<Native::OutputArgument ^> ^resultHandles = gcnew List<Native::OutputArgument ^>();


		
		std::vector<eventTrigger> eventparams = CEFAPI::GetTriggerEvent();
		for each (eventTrigger c in eventparams)
		{

			try
			{
				Native::OutputArgument^ tmp = gcnew Native::OutputArgument(gcnew System::String(c.String.c_str()));
				resultHandles->Add(tmp);
			}
			catch(int e)
			{

			}
			
		}
		CEFAPI::ResetTriggerEvent();
		return resultHandles->ToArray();

		
	}
	

	void CEF::LoadURL(String ^url)
	{
		System::String^ managedString = url;

		msclr::interop::marshal_context context;
		std::string standardString = context.marshal_as<std::string>(managedString);

		CEFAPI::LoadURL(standardString);
	}


	void CEF::Focus(bool value)
	{

		CEFAPI::Focus(value);
	}

	/*
	void CEF::CreateInstance()
	{
		CEFAPI::StartCEF();
	}
	void CEF::CloseInstance()
	{
		CEFAPI::Crash();
	}
	
	void CEF::ExecuteJavaScript(String ^code)
	{
		
		System::String^ managedString = code;

		msclr::interop::marshal_context context;
		std::string standardString = context.marshal_as<std::string>(managedString);
		CEFAPI::ExecuteJavaScript(standardString);
	}
	
	
	void CEF::ShowCursor(bool value)
	{
		CEFAPI::ShowCursor(value);
	}
	
	void CEF::KeyHandler(bool value)
	{
		CEFAPI::KeyHandler(value);
	}

	*/
	Notification ^UI::Notify(String ^message)
	{
		return Notify(message, false);
	}
	Notification ^UI::Notify(String ^message, bool blinking)
	{
		Native::Function::Call(Native::Hash::_SET_NOTIFICATION_TEXT_ENTRY, "CELL_EMAIL_BCON");
		const int strLen = 99;
		for (int i = 0; i < message->Length; i += strLen)
		{
			System::String ^substr = message->Substring(i, System::Math::Min(strLen, message->Length - i));
			Native::Function::Call(Native::Hash::_ADD_TEXT_COMPONENT_STRING, substr);
		}

		return gcnew Notification(Native::Function::Call<int>(Native::Hash::_DRAW_NOTIFICATION, blinking, 1));
	}

	void UI::ShowSubtitle(String ^message)
	{
		ShowSubtitle(message, 2500);
	}
	void UI::ShowSubtitle(String ^message, int duration)
	{
		Native::Function::Call(Native::Hash::_SET_TEXT_ENTRY_2, "CELL_EMAIL_BCON");
		const int strLen = 99;
		for (int i = 0; i < message->Length; i += strLen)
		{
			System::String ^substr = message->Substring(i, System::Math::Min(strLen, message->Length - i));
			Native::Function::Call(Native::Hash::_ADD_TEXT_COMPONENT_STRING, substr);
		}
		Native::Function::Call(Native::Hash::_DRAW_SUBTITLE_TIMED, duration, 1);
	}

	bool UI::IsHudComponentActive(HudComponent component)
	{
		return Native::Function::Call<bool>(Native::Hash::IS_HUD_COMPONENT_ACTIVE, static_cast<int>(component));
	}
	void UI::ShowHudComponentThisFrame(HudComponent component)
	{
		Native::Function::Call(Native::Hash::SHOW_HUD_COMPONENT_THIS_FRAME, static_cast<int>(component));
	}
	void UI::HideHudComponentThisFrame(HudComponent component)
	{
		Native::Function::Call(Native::Hash::HIDE_HUD_COMPONENT_THIS_FRAME, static_cast<int>(component));
	}

	Point UI::WorldToScreen(Math::Vector3 position)
	{
		float pointX, pointY;

		if (!Native::Function::Call<bool>(Native::Hash::_WORLD3D_TO_SCREEN2D, position.X, position.Y, position.Z, &pointX, &pointY))
		{
			return Point();
		}

		return Point(static_cast<int>(pointX * UI::WIDTH), static_cast<int>(pointY * UI::HEIGHT));
	}

	void UI::DrawTexture(String ^filename, int index, int level, int time, Point pos, Size size)
	{
		DrawTexture(filename, index, level, time, pos, PointF(0.0f, 0.0f), size, 0.0f, Color::White, 1.0f);
	}
	void UI::DrawTexture(String ^filename, int index, int level, int time, Point pos, Size size, float rotation, Color color)
	{
		DrawTexture(filename, index, level, time, pos, PointF(0.0f, 0.0f), size, rotation, color, 1.0f);
	}
	void UI::DrawTexture(String ^filename, int index, int level, int time, Point pos, PointF center, Size size, float rotation, Color color)
	{
		DrawTexture(filename, index, level, time, pos, center, size, rotation, color, 1.0f);
	}
	void UI::DrawTexture(String ^filename, int index, int level, int time, Point pos, PointF center, Size size, float rotation, Color color, float aspectRatio)
	{
		if (!System::IO::File::Exists(filename))
		{
			throw gcnew System::IO::FileNotFoundException(filename);
		}

		int id;

		if (_textures->ContainsKey(filename))
		{
			id = _textures->default[filename];
		}
		else
		{
			id = createTexture(reinterpret_cast<const char *>(ScriptDomain::CurrentDomain->PinString(filename).ToPointer()));
			

			_textures->Add(filename, id);
		}

		const float x = static_cast<float>(pos.X) / UI::WIDTH;
		const float y = static_cast<float>(pos.Y) / UI::HEIGHT;
		const float w = static_cast<float>(size.Width) / UI::WIDTH;
		const float h = static_cast<float>(size.Height) / UI::HEIGHT;

		drawTexture(id, index, level, time, w, h, center.X, center.Y, x, y, rotation, aspectRatio, color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);

	}
}