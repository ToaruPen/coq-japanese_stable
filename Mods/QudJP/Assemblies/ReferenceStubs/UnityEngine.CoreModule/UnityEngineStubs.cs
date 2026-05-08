using System;
using System.Collections;

namespace UnityEngine;

public class Object
{
    public string name { get; set; } = string.Empty;
    public HideFlags hideFlags { get; set; }

    public int GetInstanceID() => 0;

    public static void DontDestroyOnLoad(Object target)
    {
    }
}

[Flags]
public enum HideFlags
{
    None = 0,
    HideAndDontSave = 61,
}

public class Component : Object
{
    public GameObject gameObject { get; set; } = null!;
    public Transform transform { get; set; } = null!;

    public T? GetComponent<T>() where T : Component => default;
    public Component? GetComponent(string type) => null;
    public T? GetComponentInChildren<T>(bool includeInactive = false) where T : Component => default;
    public T[] GetComponents<T>() where T : Component => Array.Empty<T>();
    public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component => Array.Empty<T>();
}

public class Behaviour : Component
{
    public bool enabled { get; set; }
    public bool isActiveAndEnabled { get; set; }
}

public class MonoBehaviour : Behaviour
{
    public Coroutine? StartCoroutine(IEnumerator routine) => null;
}

public sealed class Coroutine
{
}

public class GameObject : Object
{
    public GameObject()
    {
        transform = new Transform { gameObject = this };
        transform.transform = transform;
    }

    public GameObject(string name)
        : this()
    {
        this.name = name;
    }

    public GameObject(string name, params Type[] components)
        : this(name)
    {
        _ = components;
    }

    public bool activeSelf { get; set; }
    public bool activeInHierarchy { get; set; }
    public int layer { get; set; }
    public Transform transform { get; set; }

    public void SetActive(bool value)
    {
        activeSelf = value;
    }

    public T AddComponent<T>() where T : Component, new() => AttachComponent(new T());
    public Component? AddComponent(Type componentType)
    {
        var component = Activator.CreateInstance(componentType) as Component;
        return component is null ? null : AttachComponent(component);
    }

    private T AttachComponent<T>(T component) where T : Component
    {
        component.gameObject = this;
        component.transform = component as Transform ?? transform;
        return component;
    }

    public T? GetComponent<T>() where T : Component => default;
    public Component? GetComponent(string type) => null;
    public T? GetComponentInChildren<T>(bool includeInactive = false) where T : Component => default;
    public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component => Array.Empty<T>();
}

public class Transform : Component
{
    public Transform? parent { get; set; }
    public Transform root => this;
    public int childCount { get; set; }
    public Vector3 localPosition { get; set; }
    public Vector3 localScale { get; set; }
    public Quaternion localRotation { get; set; }

    public Transform? Find(string name) => null;
    public Transform GetChild(int index) => new Transform();
    public int GetSiblingIndex() => 0;
    public Vector3 TransformPoint(Vector3 position) => position;
    public void SetParent(Transform parent, bool worldPositionStays = true)
    {
        this.parent = parent;
    }

    public void SetAsLastSibling()
    {
    }
}

public class RectTransform : Transform
{
    public Rect rect { get; set; }
    public Vector2 anchorMin { get; set; }
    public Vector2 anchorMax { get; set; }
    public Vector2 anchoredPosition { get; set; }
    public Vector2 sizeDelta { get; set; }
    public Vector2 pivot { get; set; }
    public Vector2 offsetMin { get; set; }
    public Vector2 offsetMax { get; set; }
}

public struct Rect
{
    public float width { get; set; }
    public float height { get; set; }
    public Vector2 center { get; set; }
}

public struct Vector2
{
    public float x { get; set; }
    public float y { get; set; }

    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public struct Vector3
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }

    public Vector3(float x, float y, float z = 0)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static implicit operator Vector3(Vector2 value) => new(value.x, value.y);
}

public struct Vector4
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
    public float w { get; set; }
}

public struct Quaternion
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
    public float w { get; set; }
}

public struct Color
{
    public float r { get; set; }
    public float g { get; set; }
    public float b { get; set; }
    public float a { get; set; }
}

public class Material : Object
{
    public bool HasProperty(string name) => false;
    public Color GetColor(string name) => default;
    public int GetInt(string name) => 0;
    public float GetFloat(string name) => 0;
}

public class CanvasRenderer : Component
{
    public bool cull { get; set; }
    public void SetAlpha(float alpha)
    {
        _ = alpha;
    }
}

public class CanvasGroup : Component
{
    public float alpha { get; set; }
}

public static class Canvas
{
    public static void ForceUpdateCanvases()
    {
    }
}

public static class Resources
{
    public static T[] FindObjectsOfTypeAll<T>() where T : Object => Array.Empty<T>();
}

public static class Debug
{
    public static void Log(object message)
    {
    }

    public static void LogWarning(object message)
    {
    }

    public static void LogError(object message)
    {
    }
}

public static class Time
{
    public static int frameCount { get; set; }
}

public sealed class WaitForEndOfFrame
{
}
