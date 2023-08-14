using System;
using UnityEngine;

namespace Impostors.ObjectPools
{
    [Serializable]
    public class MaterialObjectPool : ObjectPool<Material>
    {
        private readonly Shader _shader;
        public MaterialObjectPool(int initialCapacity, Shader shader) : base(initialCapacity)
        {
            _shader = shader;
            CreateInitialInstances();
        }

        protected override Material CreateObjectInstance()
        {
            return new Material(_shader);
        }

        protected override void ProcessReturnedInstance(Material instance)
        {
            instance.mainTexture = null;
        }
    }
}