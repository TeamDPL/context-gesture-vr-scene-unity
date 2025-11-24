using UnityEngine;
using System.Collections.Generic;
using System;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public static void Enqueue(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    private static MainThreadDispatcher _instance;
    public static void Init()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
    }

    [RuntimeInitializeOnLoadMethod]
    static void AutoInit() { Init(); }
}