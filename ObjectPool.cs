using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MarySGameEngine;

/// <summary>
/// Generic object pool to reduce garbage collection pressure
/// </summary>
/// <typeparam name="T">Type of objects to pool</typeparam>
public class ObjectPool<T> where T : class
{
    private readonly ConcurrentQueue<T> _objects = new ConcurrentQueue<T>();
    private readonly Func<T> _objectGenerator;
    private readonly Action<T> _resetAction;
    private readonly int _maxSize;
    private int _currentSize;

    public ObjectPool(Func<T> objectGenerator, Action<T> resetAction = null, int maxSize = 100)
    {
        _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        _resetAction = resetAction;
        _maxSize = maxSize;
    }

    /// <summary>
    /// Get an object from the pool or create a new one
    /// </summary>
    public T Get()
    {
        if (_objects.TryDequeue(out T item))
        {
            return item;
        }
        
        return _objectGenerator();
    }

    /// <summary>
    /// Return an object to the pool
    /// </summary>
    public void Return(T item)
    {
        if (item == null) return;
        
        if (_currentSize < _maxSize)
        {
            _resetAction?.Invoke(item);
            _objects.Enqueue(item);
            _currentSize++;
        }
    }

    /// <summary>
    /// Clear the pool
    /// </summary>
    public void Clear()
    {
        while (_objects.TryDequeue(out _))
        {
            // Empty the queue
        }
        _currentSize = 0;
    }

    /// <summary>
    /// Get the current size of the pool
    /// </summary>
    public int Count => _currentSize;
}

/// <summary>
/// Static object pools for common types
/// </summary>
public static class ObjectPools
{
    public static readonly ObjectPool<List<object>> ListPool = new ObjectPool<List<object>>(
        () => new List<object>(),
        list => list.Clear(),
        50
    );

    public static readonly ObjectPool<Dictionary<string, object>> DictionaryPool = new ObjectPool<Dictionary<string, object>>(
        () => new Dictionary<string, object>(),
        dict => dict.Clear(),
        20
    );

    public static readonly ObjectPool<System.Text.StringBuilder> StringBuilderPool = new ObjectPool<System.Text.StringBuilder>(
        () => new System.Text.StringBuilder(),
        sb => sb.Clear(),
        10
    );
}