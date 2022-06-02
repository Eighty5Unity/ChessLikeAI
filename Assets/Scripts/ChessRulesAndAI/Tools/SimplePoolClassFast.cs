using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplePoolClassFast<T> : SimplePoolBaseFast<T> where T : new()
{
    protected override T CreateNewObject()
    {
        return new T();
    }
}
