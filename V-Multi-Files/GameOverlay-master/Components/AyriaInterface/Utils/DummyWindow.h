class DummyWindow
{
public:
	DummyWindow();
	~DummyWindow();

	HWND Get();

private:
	HWND Window;
	std::string Class;
};
