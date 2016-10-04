using GTANetworkShared;

namespace GTANetworkServer
{
    public abstract class Entity
    {
        internal Entity(API father, NetHandle handle)
        {
            Base = father;
            Handle = handle;
        }

        public NetHandle Handle { get; protected set; }
        protected API Base { get; set; }

        public static implicit operator NetHandle(Entity c)
        {
            return c.Handle;
        }

        #region Properties

        public bool freezePosition
        {
            set
            {
                Base.setEntityPositionFrozen(this, value);
            }
        }

        public virtual Vector3 position
        {
            set
            {
                Base.setEntityPosition(this, value);
            }
            get
            {
                return Base.getEntityPosition(this);
            }
        }

        public virtual Vector3 rotation
        {
            set
            {
                Base.setEntityRotation(this, value);
            }
            get
            {
                return Base.getEntityRotation(this);
            }
        }

        public bool exists
        {
            get { return Base.doesEntityExist(this); }
        }

        public EntityType type
        {
            get { return Base.getEntityType(this); }
        }

        public int transparency
        {
            set
            {
                Base.setEntityTransparency(this, value);
            }
            get { return Base.getEntityTransparency(this); }
        }

        public int dimension
        {
            set
            {
                Base.setEntityDimension(this, value);
            }
            get { return Base.getEntityDimension(this); }
        }

        public bool invincible
        {
            set
            {
                Base.setEntityInvincible(this, value);
            }
            get { return Base.getEntityInvincible(this); }
        }

        public bool collisionless
        {
            set
            {
                Base.setEntityCollisionless(this, value);
            }
            get { return Base.getEntityCollisionless(this); }
        }

        public int model
        {
            get { return Base.getEntityModel(this); }
        }
        
        #endregion

        #region Methods

        public void delete()
        {
            Base.deleteEntity(this);
        }

        public void movePosition(Vector3 target, int duration)
        {
            Base.moveEntityPosition(this, target, duration);
        }

        public void moveRotation(Vector3 target, int duration)
        {
            Base.moveEntityRotation(this, target, duration);
        }

        public void attachTo(NetHandle entity, string bone, Vector3 offset, Vector3 rotation)
        {
            Base.attachEntityToEntity(this, entity, bone, offset, rotation);
        }

        public void detach()
        {
            Base.detachEntity(this);
        }

        public void detach(bool resetCollision)
        {
            Base.detachEntity(this, resetCollision);
        }

        public void createParticleEffect(string ptfxLib, string ptfxName, Vector3 offset, Vector3 rotation, float scale, int bone = -1)
        {
            Base.createParticleEffectOnEntity(ptfxLib, ptfxName, this, offset, rotation, scale, bone, dimension);
        }

        public void setData(string key, object value)
        {
            Base.setEntityData(this, key, value);
        }

        public dynamic getData(string key)
        {
            return Base.getEntityData(this, key);
        }

        public void resetData(string key)
        {
            Base.resetEntityData(this, key);
        }

        public bool hasData(string key)
        {
            return Base.hasEntityData(this, key);
        }

        public void setLocalData(string key, object value)
        {
            Base.setLocalEntityData(this, key, value);
        }

        public dynamic getLocalData(string key)
        {
            return Base.getLocalEntityData(this, key);
        }

        public void resetLocalData(string key)
        {
            Base.resetLocalEntityData(this, key);
        }

        public bool hasLocalData(string key)
        {
            return Base.hasLocalEntityData(this, key);
        }

        #endregion
    }
}