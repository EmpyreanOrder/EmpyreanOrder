using System;
using System.Collections.Generic;
using UnityEngine;

namespace Impostors.ObjectPools
{
    [Serializable]
    public abstract class ObjectPool<T>
    {
        // lists are used to make debug easier
        [SerializeField]
        private List<T> _availableObjects;
        [SerializeField]
        private List<T> _usedObjects;
        private bool _isInitialized;

        protected ObjectPool(int initialCapacity)
        {
            _availableObjects = new List<T>(initialCapacity);
            _usedObjects = new List<T>(initialCapacity);
            _isInitialized = false;
        }

        protected void CreateInitialInstances()
        {
            if (_isInitialized)
                throw new Exception("Object Pool is already initialized.");
            var initialCapacity = _availableObjects.Capacity;
            for (int i = 0; i < initialCapacity; i++)
            {
                var t = CreateObjectInstance();
                _availableObjects.Add(t);
            }

            _isInitialized = true;
        }
        
        public virtual T Get()
        {
            T result = default;
            if (_availableObjects.Count == 0)
            {
                result = CreateObjectInstance();
            }
            else
            {
                result = _availableObjects[_availableObjects.Count - 1];
                _availableObjects.RemoveAt(_availableObjects.Count - 1);
            }

            _usedObjects.Add(result);

            return result;
        }

        public virtual void Return(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            int indexInUsedObjects = _usedObjects.IndexOf(instance);
            if (indexInUsedObjects == -1)
                throw new ArgumentException("Trying to return object that is not part of this pool.", nameof(instance));
            _usedObjects.RemoveAt(indexInUsedObjects);
            _availableObjects.Add(instance);
        }

        protected abstract T CreateObjectInstance();

        protected abstract void ProcessReturnedInstance(T instance);
    }
}