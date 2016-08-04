using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTANetwork
{
    public class ModelSpoofer
    {
        public ModelSpoofer(GTA.Entity ent, int fakeModel)
        {
            _entityToSpoof = ent;
            _modelToSpoof = fakeModel;
        }

        private GTA.Entity _entityToSpoof;
        private int _modelToSpoof;
        const int modelOffset = 0;

        public unsafe void Pulse()
        {
            if (_entityToSpoof == null || _modelToSpoof == 0) return;

            var modelPointer = new IntPtr(_entityToSpoof.MemoryAddress) + modelOffset;
            int model = Marshal.ReadInt32(modelPointer, 0);

            if (model != _modelToSpoof)
            {
                var bytes = BitConverter.GetBytes(_modelToSpoof);
                Marshal.Copy(bytes, 0, modelPointer, bytes.Length);
            }
        }

        public unsafe static void Spoof(GTA.Entity ent, int model)
        {
            if (ent == null || model == 0) return;

            var modelPointer = new IntPtr(ent.MemoryAddress) + modelOffset;
            var bytes = BitConverter.GetBytes(model);
            Marshal.Copy(bytes, 0, modelPointer, bytes.Length);
        }
    }
}
