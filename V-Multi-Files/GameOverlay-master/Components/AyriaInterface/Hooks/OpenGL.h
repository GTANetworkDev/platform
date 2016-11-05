
namespace Hook
{
	class OpenGL
	{
	public:
		static bool Initialize();
		static bool Uninitialize();

	private:
		static bool Initialized;
		static Hook::Jump SwapBufferHook;

		static BOOL WINAPI SwapBuffers(HDC hdc);
	};
}