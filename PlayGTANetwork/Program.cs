using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace PlayGTANetwork
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        //[STAThread]
        public static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            IEnumerable<Type> validTypes;
            try
            {
                var ourAssembly = Assembly.LoadFrom("GTANetwork.dll");

                var types = ourAssembly.GetExportedTypes();
                validTypes = types.Where(t =>
                    !t.IsInterface &&
                    !t.IsAbstract)
                    .Where(t => typeof (ISubprocessBehaviour).IsAssignableFrom(t));
            }
            catch (Exception e)
            {
                MessageBox.Show("ERROR: " + e.Message, "CRITICAL ERROR");
                goto end;
            }


            if (!validTypes.Any())
            {
                MessageBox.Show("Failed to load assembly \"GTANetwork.dll\": no assignable classes found.",
                    "CRITICAL ERROR");
                goto end;
            }

            ISubprocessBehaviour mainBehaviour = null;
            foreach (var type in validTypes)
            {
                mainBehaviour = Activator.CreateInstance(type) as ISubprocessBehaviour;
                if (mainBehaviour != null)
                    break;
            }

            if (mainBehaviour == null)
            {
                MessageBox.Show("Failed to load assembly \"GTANetwork.dll\": assignable class is null.",
                    "CRITICAL ERROR");
                goto end;
            }

            mainBehaviour.Start();

            end:
            { }
        }
    }

    public interface ISubprocessBehaviour
    {
        void Start();
    }
}
