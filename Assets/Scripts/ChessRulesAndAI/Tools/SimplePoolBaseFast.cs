using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public abstract class SimplePoolBaseFast<T>
{
    protected LinkedList<T> m_freeList = new LinkedList<T>();

    // Use this for initialization
    public LinkedListNode<T> GetNewObject()
    {
        if (m_freeList.Count == 0)
        {
            return new LinkedListNode<T>(CreateNewObject());
        }

        LinkedListNode<T> toReturn = m_freeList.Last;
        m_freeList.RemoveLast();

        return toReturn;
    }

    protected abstract T CreateNewObject();

    public void ReturnObject(LinkedListNode<T> obj)
    {
        m_freeList.AddLast(obj);
    }
}
