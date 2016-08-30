using System;
using System.Windows.Forms;

namespace GTANetwork.GUI
{
    public interface IChat
    {
        event EventHandler OnComplete;
        void Init();
        bool IsFocused { get; set; }
        string CurrentInput { get; }
        void Clear();
        void Tick();
        void AddMessage(string sender, string msg);
        void OnKeyDown(Keys key);
    }
}