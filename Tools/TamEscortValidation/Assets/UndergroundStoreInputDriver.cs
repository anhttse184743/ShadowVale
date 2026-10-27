using System;
using System.Collections;
using UnityEngine;
[DefaultExecutionOrder(-10000)]
public sealed class UndergroundStoreInputDriver:MonoBehaviour
{
    public IEnumerator Scenario;
    public Action<Exception> Completed;
    void Update()
    {
        if(Scenario==null)return;
        try{if(Scenario.MoveNext())return;Scenario=null;Completed?.Invoke(null);}
        catch(Exception error){Scenario=null;Completed?.Invoke(error);}
    }
}
