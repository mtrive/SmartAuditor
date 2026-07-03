// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System;

namespace SmartAuditor.Editor.Utils
{
    internal static class HierarchyTraversal
    {
        /// <summary>
        /// Resolves a scene by name, or returns the active scene when <paramref name="sceneName"/> is null or empty.
        /// </summary>
        /// <param name="sceneName">The name of the scene to find. If null or empty, the active scene is returned.</param>
        public static Scene FindScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return SceneManager.GetActiveScene();
            }

            return SceneManager.GetSceneByName(sceneName);
        }

        /// <summary>
        /// Traverses all objects in a given GameObject hierarchy.
        /// </summary>
        /// <param name="go">The GameObject</param>
        /// <param name="hierarchyPath">The current hierarchy path (empty string for go)</param>
        /// <param name="action">Action to perform on each GameObject. Returns true to continue traversing children, false to stop.</param>
        public static void TraverseGameObjectHierarchy(GameObject go, string hierarchyPath, Func<GameObject, string, bool> action)
        {
            if (go == null)
                throw new ArgumentNullException(nameof(go));

            hierarchyPath = string.IsNullOrEmpty(hierarchyPath) ? go.name : $"{hierarchyPath}/{go.name}";

            var shouldContinue = action(go, hierarchyPath);
            if (!shouldContinue)
                return;

            foreach (Transform child in go.transform)
            {
                TraverseGameObjectHierarchy(child.gameObject, hierarchyPath, action);
            }
        }

        /// <summary>
        /// Traverses all objects inside a prefab asset without instantiating it in the scene.
        /// </summary>
        /// <param name="prefabPath">The asset path of the prefab</param>
        /// <param name="action">Action to perform on each GameObject. Returns true to continue traversing children, false to stop.</param>
        public static void TraversePrefab(string prefabPath, Func<GameObject, string, bool> action)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return;

            var prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabContents == null)
                return;

            try
            {
                TraverseGameObjectHierarchy(prefabContents, string.Empty, action);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

    }
}
