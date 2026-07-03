using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartAuditor.Editor.Utils
{
    internal static class GameObjectUtils
    {
        internal static List<T> GetAllComponents<T>()
        {
            var allComponents = new List<T>();
            for (int n = 0; n < SceneManager.sceneCount; ++n)
            {
                var scene = SceneManager.GetSceneAt(n);
                var roots = scene.GetRootGameObjects();
                foreach (var go in roots)
                {
                    GetComponents(go, ref allComponents);
                }
            }

            return allComponents;
        }

        static void GetComponents<T>(GameObject go, ref List<T> components)
        {
            bool result = go.TryGetComponent(out T comp);
            if (result)
            {
                components.Add(comp);
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                GetComponents(go.transform.GetChild(i).gameObject, ref components);
            }
        }
    }
}
