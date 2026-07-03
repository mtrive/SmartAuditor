// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Utils
{
    internal static class ShaderStripping
    {
        // Mirrors GraphicsSettings.m_FogStripping (Automatic = 0, Custom = 1).
        internal enum FogStripping
        {
            Automatic,
            Custom
        }

        internal static void GetPreservedFogModes(out bool linear, out bool exponential, out bool exponentialSquared)
        {
            linear = false;
            exponential = false;
            exponentialSquared = false;

            var serializedObject = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            serializedObject.Update();

            var fogStripping = serializedObject.FindProperty("m_FogStripping");
            if (fogStripping == null || (FogStripping)fogStripping.enumValueIndex == FogStripping.Automatic)
                return;

            var keepLinear = serializedObject.FindProperty("m_FogKeepLinear");
            var keepExp = serializedObject.FindProperty("m_FogKeepExp");
            var keepExp2 = serializedObject.FindProperty("m_FogKeepExp2");

            linear = keepLinear?.boolValue ?? false;
            exponential = keepExp?.boolValue ?? false;
            exponentialSquared = keepExp2?.boolValue ?? false;
        }

        internal static bool IsFogModePreserved(FogMode fogMode)
        {
            GetPreservedFogModes(out var linear, out var exponential, out var exponentialSquared);

            switch (fogMode)
            {
                case FogMode.Linear:
                    return linear;

                case FogMode.Exponential:
                    return exponential;

                case FogMode.ExponentialSquared:
                    return exponentialSquared;

                default:
                    return false;
            }
        }
    }
}
