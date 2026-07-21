using System;
using UnityEngine;

/// <summary>
/// 固定容量的循环队列，队满时拒绝入队。
/// </summary>
[Serializable]
public sealed class CircularQueue<T>
{
    [SerializeField] private T[] buffer = Array.Empty<T>();
    [SerializeField] private int head;
    [SerializeField] private int tail;
    [SerializeField] private int count;

    public CircularQueue()
    {
    }

    public CircularQueue(int capacity)
    {
        Reset(capacity);
    }

    public int Capacity => buffer != null ? buffer.Length : 0;
    public int Count => count;
    public bool IsEmpty => count <= 0;
    public bool IsFull => Capacity > 0 && count >= Capacity;

    public void Reset(int capacity)
    {
        int safeCapacity = Mathf.Max(1, capacity);
        buffer = new T[safeCapacity];
        head = 0;
        tail = 0;
        count = 0;
    }

    public bool TryEnqueue(T item)
    {
        if (IsFull)
            return false;

        buffer[tail] = item;
        tail = (tail + 1) % Capacity;
        count++;
        return true;
    }

    public bool TryDequeue(out T item)
    {
        if (IsEmpty)
        {
            item = default;
            return false;
        }

        item = buffer[head];
        buffer[head] = default;
        head = (head + 1) % Capacity;
        count--;
        return true;
    }

    public bool TryPeek(out T item)
    {
        if (IsEmpty)
        {
            item = default;
            return false;
        }

        item = buffer[head];
        return true;
    }

    public bool TryGetAt(int logicalIndex, out T item)
    {
        if (logicalIndex < 0 || logicalIndex >= count)
        {
            item = default;
            return false;
        }

        item = buffer[(head + logicalIndex) % Capacity];
        return true;
    }

    public void Clear()
    {
        if (buffer == null || buffer.Length == 0)
        {
            head = 0;
            tail = 0;
            count = 0;
            return;
        }

        Array.Clear(buffer, 0, buffer.Length);
        head = 0;
        tail = 0;
        count = 0;
    }
}
