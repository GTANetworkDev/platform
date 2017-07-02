using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GTANetworkShared;

namespace GTANetworkServer
{
    public delegate void ColShapeEvent(ColShape shape, NetHandle entity);

    public abstract class ColShape
    {
        public abstract bool Check(Vector3 pos);

        public event ColShapeEvent onEntityEnterColShape;
        public event ColShapeEvent onEntityExitColShape;
        public int handle;

        public int dimension
        {
            get { return _dimension; }
            set
            {
                _dimension = value;
                if (value != 0)
                {
                    lock (EntitiesInContact)
                    {
                        EntitiesInContact.RemoveAll(
                            ent =>
                                Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(ent).Dimension !=
                                value && Program.ServerInstance.NetEntityHandler.NetToProp<EntityProperties>(ent).Dimension != 0);
                    }
                }
            }
        }

        private Dictionary<string, object> _data = new Dictionary<string, object>();

        public bool containsEntity(NetHandle ent)
        {
            return EntitiesInContact.Contains(ent.Value);
        }

        public void setData(string key, object data)
        {
            _data.Set(key, data);
        }

        public dynamic getData(string key)
        {
            return _data.Get(key);
        }

        public bool hasData(string key)
        {
            return _data.ContainsKey(key);
        }

        public void resetData(string key)
        {
            _data.Remove(key);
        }

        public IEnumerable<NetHandle> getAllEntities()
        {
            return new List<NetHandle>(EntitiesInContact.Select(i => new NetHandle(i)));
        }

        internal void InvokeEnterColshape(NetHandle ent)
        {
            onEntityEnterColShape?.Invoke(this, ent);
        }

        internal void InvokeExitColshape(NetHandle ent)
        {
            onEntityExitColShape?.Invoke(this, ent);
        }

        internal List<int> EntitiesInContact = new List<int>();
        private int _dimension;

        internal static Vector3[] NormalizeEdges(Vector3 edgeA, Vector3 edgeB)
        {
            // Correct for inside out vectors
            Vector3 tmp1 = edgeA.Copy(); // smaller edge
            Vector3 tmp2 = edgeB.Copy(); // larger edge

            tmp1.X = Math.Min(edgeA.X, edgeB.X);
            tmp2.X = Math.Max(edgeA.X, edgeB.X);

            tmp1.Y = Math.Min(edgeA.Y, edgeB.Y);
            tmp2.Y = Math.Max(edgeA.Y, edgeB.Y);

            tmp1.Z = Math.Min(edgeA.Z, edgeB.Z);
            tmp2.Z = Math.Max(edgeA.Z, edgeB.Z);

            return new Vector3[2] { tmp1, tmp2 };
        }
    }

    public class SphereColShape : ColShape
    {
        internal SphereColShape(Vector3 center, float range)
        {
            Range = range;
            Center = center;
        }

        private float _rangeSquared;
        private float _range;
        public Vector3 Center;

        private NetHandle _attachedNetHandle;

        public float Range
        {
            get
            {
                return _range;
            }
            set
            {
                _rangeSquared = value*value;
                _range = value;
            }
        }

        public override bool Check(Vector3 pos)
        {
            var c = Center;

            if (!_attachedNetHandle.IsNull)
                c = Program.ServerInstance.PublicAPI.getEntityPosition(_attachedNetHandle);

            return c.DistanceToSquared(pos) <= _rangeSquared;
        }

        public void attachToEntity(NetHandle entity)
        {
            _attachedNetHandle = entity;
        }

        private void detach()
        {
            _attachedNetHandle = new NetHandle(0);
        }
    }

    public class CylinderColShape : ColShape
    {
        internal CylinderColShape(Vector3 center, float range, float height)
        {
            Range = range;
            Center = center;
            _height = height;
        }

        private float _rangeSquared;
        private float _range;
        private float _height;
        public Vector3 Center;

        private NetHandle _attachedNetHandle;

        public float Range
        {
            get
            {
                return _range;
            }
            set
            {
                _rangeSquared = value * value;
                _range = value;
            }
        }

        public float Height
        {
            get { return _height; }
            set { _height = value; }
        }

        public override bool Check(Vector3 pos)
        {
            var c = Center;

            if (!_attachedNetHandle.IsNull)
                c = Program.ServerInstance.PublicAPI.getEntityPosition(_attachedNetHandle);

            return c.DistanceToSquared2D(pos) <= _rangeSquared && pos.Z > c.Z - Height && pos.Z < c.Z + Height;
        }

        public void attachToEntity(NetHandle entity)
        {
            _attachedNetHandle = entity;
        }

        private void detach()
        {
            _attachedNetHandle = new NetHandle(0);
        }
    }

    public class Rectangle2DColShape : ColShape
    {
        internal Rectangle2DColShape(Vector3 start, Vector3 stop)
        {
            Vector3[] edges = NormalizeEdges(start, stop);
            Start = edges[0];
            End = edges[1];

            // API compatibility
            Vector3 deltas = End - Start;
            Width = deltas.X;
            Height = deltas.Y;
            X = Start.X;
            Y = Start.Y;
        }

        internal Rectangle2DColShape(float x, float y, float w, float h) :
            this(new Vector3(x, y, 0f), new Vector3(x+w, y+h, 0f))
        {
        
        }

        public Vector3 Start;
        public Vector3 End;

        // API compatibility
        public float Width;
        public float Height;
        public float X;
        public float Y;

        public override bool Check(Vector3 pos)
        {
            return (pos.X > Start.X && pos.Y > Start.Y) && 
                   (pos.X < End.X && pos.Y < End.X);
        }
    }

    public class Rectangle3DColShape : ColShape
    {
        internal Rectangle3DColShape(Vector3 start, Vector3 end)
        {
            Vector3[] edges = NormalizeEdges(start, end);

            Start = edges[0];
            End = edges[1];
        }

        public Vector3 Start;
        public Vector3 End;

        public override bool Check(Vector3 pos)
        {
            return (pos.X > Start.X && pos.Y > Start.Y && pos.Z > Start.Z) &&
                   (pos.X < End.X && pos.Y < End.Y && pos.Z < End.Z);
        }
    }


    public class ColShapeManager
    {
        public ColShapeManager()
        {
            MainThread = new Thread(MainLoop);
            MainThread.IsBackground = true;
            MainThread.Start();
        }

        public Thread MainThread;
        public bool HasToStop;

        public List<ColShape> ColShapes = new List<ColShape>();

        private readonly EntityType[] _validTypes = new[]
        {
            EntityType.Player,
            EntityType.Prop,
            EntityType.Vehicle,
        };

        public void Shutdown()
        {
            HasToStop = true;
            MainThread.Abort();
        }

        private int _shapeHandles = 0;
        public void Add(ColShape shape)
        {
            shape.handle = ++_shapeHandles;
            lock (ColShapes) ColShapes.Add(shape);
        }

        public void Remove(ColShape shape)
        {
            lock(ColShapes) ColShapes.Remove(shape);
        }

        public void MainLoop()
        {
            while (!HasToStop)
            {
                try
                {
                    Dictionary<int, EntityProperties> entities = Program.ServerInstance.NetEntityHandler.ToCopy();

                    var entList = entities.Where(pair => _validTypes.Contains((EntityType) pair.Value.EntityType));

                    List<ColShape> localShapes;
                    lock (ColShapes)
                    {
                        localShapes = new List<ColShape>(ColShapes);
                    }

                    foreach (var shape in localShapes)
                        foreach (var entity in entList.Where(ent => shape.dimension == 0 || ent.Value.Dimension == 0 || ent.Value.Dimension == shape.dimension))
                        {
                            if (entity.Value == null || entity.Value.Position == null) continue;
                            if (shape.Check(entity.Value.Position))
                            {
                                if (!shape.EntitiesInContact.Contains(entity.Key))
                                {
                                    NetHandle ent = new NetHandle(entity.Key);

                                    lock (Program.ServerInstance.RunningResources)
                                            Program.ServerInstance.RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                            {
                                                en.InvokeColshapeEnter(shape, ent);
                                            }));

                                    shape.InvokeEnterColshape(ent);

                                    shape.EntitiesInContact.Add(entity.Key);
                                }
                            }
                            else
                            {
                                if (shape.EntitiesInContact.Contains(entity.Key))
                                {
                                    NetHandle ent = new NetHandle(entity.Key);

                                    lock (Program.ServerInstance.RunningResources)
                                            Program.ServerInstance.RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                            {
                                                en.InvokeColshapeExit(shape, ent);
                                            }));

                                    shape.InvokeExitColshape(ent);

                                    shape.EntitiesInContact.Remove(entity.Key);
                                }
                            }
                        }
                }
                catch (Exception ex)
                {
                    Program.Output("COLSHAPE FAILURE");
                    Program.Output(ex.ToString());
                }

                Thread.Sleep(100);
            }
        }
    }
}