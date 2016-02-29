using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using PlayGTANetwork.DirectXHook;
using PlayGTANetwork.DirectXHook.Hook;
using PlayGTANetwork.DirectXHook.Interface;

namespace PlayGTANetwork
{
    public partial class Form1 : Form
    {
        private int _gtaProcessId;
        private Process _gtaProcess;
        private CaptureProcess _captureProcess;

        public Form1()
        {
            InitializeComponent();
            StartHook();
            
        }

        public void StartHook()
        {
            var exeName = "GTA5";
            Process[] processes = Process.GetProcessesByName(exeName);
            foreach (var process in processes)
            {
                if (process == null) continue;
                if (HookManager.IsHooked(process.Id))
                    continue;

                Direct3DVersion direct3dVersion = Direct3DVersion.Direct3D11;

                var gameSettings = GameSettings.LoadGameSettings();
                /*
                switch (gameSettings.Graphics.DX_Version.Value)
                {
                    case 0:
                        direct3dVersion = Direct3DVersion.Direct3D10;
                        break;
                    case 1:
                        direct3dVersion = Direct3DVersion.Direct3D10_1;
                        break;
                    case 2:
                        direct3dVersion = Direct3DVersion.Direct3D11;
                        break;
                }*/

                CaptureConfig cc = new CaptureConfig()
                {
                    Direct3DVersion = direct3dVersion,
                    ShowOverlay = true,
                };

                _gtaProcessId = process.Id;
                _gtaProcess = process;

                var captureInterface = new CaptureInterface();
                _captureProcess = new CaptureProcess(process, cc, captureInterface);
                //Thread.Sleep(5000);
                //_captureProcess.CaptureInterface.DisplayInGameText("HELLO FROM PLAYGTANETWORK!", TimeSpan.FromSeconds(60));
                //_captureProcess.CaptureInterface.up
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            HookManager.RemoveHookedProcess(_captureProcess.Process.Id);
            _captureProcess.CaptureInterface.Disconnect();
            _captureProcess = null;
            MessageBox.Show("Detached!");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (Bitmap bmpScreenCapture = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                            Screen.PrimaryScreen.Bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmpScreenCapture))
                {
                    g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                     Screen.PrimaryScreen.Bounds.Y,
                                     0, 0,
                                     bmpScreenCapture.Size,
                                     CopyPixelOperation.SourceCopy);
                }

                _captureProcess.CaptureInterface.UpdateMainBitmap(bmpScreenCapture);
            }

        }
    }
}
