// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using UnityEngine;
using UnityEngineInternal;
using Object = UnityEngine.Object;


namespace UnityEditor
{
    internal class LightingWindowLightmapPreviewTab
    {
        Vector2 m_ScrollPositionLightmaps = Vector2.zero;
        Vector2 m_ScrollPositionMaps = Vector2.zero;
        int m_SelectedLightmap = -1;

        static Styles s_Styles;
        class Styles
        {
            public GUIStyle selectedLightmapHighlight = "LightmapEditorSelectedHighlight";

            public GUIContent LightProbes = EditorGUIUtility.TextContent("Light Probes|A different LightProbes.asset can be assigned here. These assets are generated by baking a scene containing light probes.");
            public GUIContent LightingDataAsset = EditorGUIUtility.TextContent("Lighting Data Asset|A different LightingData.asset can be assigned here. These assets are generated by baking a scene in the OnDemand mode.");
            public GUIContent MapsArraySize = EditorGUIUtility.TextContent("Array Size|The length of the array of lightmaps.");
        }

        static void DrawHeader(Rect rect, bool showdrawDirectionalityHeader, bool showShadowMaskHeader, float maxLightmaps)
        {
            // we first needed to get the amount of space that the first texture would get
            // as that's done now, let's request the rect for the header
            rect.width = rect.width / maxLightmaps;

            // display the header
            EditorGUI.DropShadowLabel(rect, "Intensity");
            rect.x += rect.width;
            if (showdrawDirectionalityHeader)
            {
                EditorGUI.DropShadowLabel(rect, "Directionality");
                rect.x += rect.width;
            }

            if (showShadowMaskHeader)
            {
                EditorGUI.DropShadowLabel(rect, "Shadowmask");
            }
        }

        void MenuSelectLightmapUsers(Rect rect, int lightmapIndex)
        {
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                string[] menuText = { "Select Lightmap Users" };
                Rect r = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 1, 1);
                EditorUtility.DisplayCustomMenu(r, EditorGUIUtility.TempContent(menuText), -1, SelectLightmapUsers, lightmapIndex);
                Event.current.Use();
            }
        }

        void SelectLightmapUsers(object userData, string[] options, int selected)
        {
            int lightmapIndex = (int)userData;
            ArrayList newSelection = new ArrayList();
            MeshRenderer[] renderers = Object.FindObjectsOfType(typeof(MeshRenderer)) as MeshRenderer[];
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null && renderer.lightmapIndex == lightmapIndex)
                    newSelection.Add(renderer.gameObject);
            }
            Terrain[] terrains = Object.FindObjectsOfType(typeof(Terrain)) as Terrain[];
            foreach (Terrain terrain in terrains)
            {
                if (terrain != null && terrain.lightmapIndex == lightmapIndex)
                    newSelection.Add(terrain.gameObject);
            }
            Selection.objects = newSelection.ToArray(typeof(Object)) as Object[];
        }

        public void LightmapPreview(Rect r)
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            const float headerHeight = 20;
            const float spacing = 10;
            GUI.Box(r, "", "PreBackground");
            m_ScrollPositionLightmaps = EditorGUILayout.BeginScrollView(m_ScrollPositionLightmaps, GUILayout.Height(r.height));
            int lightmapIndex = 0;
            bool haveDirectionalityLightMaps = false;
            bool haveShadowMaskLightMaps = false;

            foreach (LightmapData li in LightmapSettings.lightmaps)
            {
                if (li.lightmapDir != null) haveDirectionalityLightMaps = true;
                if (li.shadowMask != null)   haveShadowMaskLightMaps = true;
            }

            float maxLightmaps = 1.0f;
            if (haveDirectionalityLightMaps) ++maxLightmaps;
            if (haveShadowMaskLightMaps) ++maxLightmaps;

            // display the header
            Rect headerRect = GUILayoutUtility.GetRect(r.width, r.width, headerHeight, headerHeight);
            DrawHeader(headerRect, haveDirectionalityLightMaps, haveShadowMaskLightMaps, maxLightmaps);

            foreach (LightmapData li in LightmapSettings.lightmaps)
            {
                if (li.lightmapColor == null && li.lightmapDir == null && li.shadowMask == null)
                {
                    lightmapIndex++;
                    continue;
                }

                int lightmapColorMaxSize = li.lightmapColor ? Math.Max(li.lightmapColor.width, li.lightmapColor.height) : -1;
                int lightmapDirMaxSize = li.lightmapDir ? Math.Max(li.lightmapDir.width, li.lightmapDir.height) : -1;
                int lightMaskMaxSize = li.shadowMask ? Math.Max(li.shadowMask.width, li.shadowMask.height) : -1;

                Texture2D biggerLightmap;
                if (lightmapColorMaxSize > lightmapDirMaxSize)
                {
                    biggerLightmap = lightmapColorMaxSize > lightMaskMaxSize ? li.lightmapColor : li.shadowMask;
                }
                else
                {
                    biggerLightmap = lightmapDirMaxSize > lightMaskMaxSize ? li.lightmapDir : li.shadowMask;
                }

                // get rect for textures in this row
                GUILayoutOption[] layout = { GUILayout.MaxWidth(r.width), GUILayout.MaxHeight(biggerLightmap.height)};
                Rect rect = GUILayoutUtility.GetAspectRect(maxLightmaps, layout);

                // display the textures
                float rowSpacing = spacing * 0.5f;
                rect.width /= maxLightmaps;
                rect.width -= rowSpacing;
                rect.x += rowSpacing / 2;
                EditorGUI.DrawPreviewTexture(rect, li.lightmapColor);
                MenuSelectLightmapUsers(rect, lightmapIndex);

                if (li.lightmapDir)
                {
                    rect.x += rect.width + rowSpacing;
                    EditorGUI.DrawPreviewTexture(rect, li.lightmapDir);
                    MenuSelectLightmapUsers(rect, lightmapIndex);
                }
                if (li.shadowMask)
                {
                    rect.x += rect.width + rowSpacing;
                    EditorGUI.DrawPreviewTexture(rect, li.shadowMask);
                    MenuSelectLightmapUsers(rect, lightmapIndex);
                }
                GUILayout.Space(spacing);
                lightmapIndex++;
            }

            EditorGUILayout.EndScrollView();
        }

        public void UpdateLightmapSelection()
        {
            MeshRenderer renderer;
            Terrain terrain = null;
            // if the active object in the selection is a renderer or a terrain, we're interested in it's lightmapIndex
            if (Selection.activeGameObject == null ||
                ((renderer = Selection.activeGameObject.GetComponent<MeshRenderer>()) == null &&
                 (terrain = Selection.activeGameObject.GetComponent<Terrain>()) == null))
            {
                m_SelectedLightmap = -1;
                return;
            }
            m_SelectedLightmap = renderer != null ? renderer.lightmapIndex : terrain.lightmapIndex;
        }

        public void Maps()
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            GUI.changed = false;

            if (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.OnDemand)
            {
                SerializedObject so = new SerializedObject(LightmapEditorSettings.GetLightmapSettings());
                SerializedProperty LightingDataAsset = so.FindProperty("m_LightingDataAsset");
                EditorGUILayout.PropertyField(LightingDataAsset, s_Styles.LightingDataAsset);
                so.ApplyModifiedProperties();
            }

            GUILayout.Space(10);

            LightmapData[] lightmaps = LightmapSettings.lightmaps;

            m_ScrollPositionMaps = GUILayout.BeginScrollView(m_ScrollPositionMaps);
            using (new EditorGUI.DisabledScope(true))
            {
                bool showDirLightmap = false;
                bool showShadowMask = false;
                foreach (LightmapData lightmapData in lightmaps)
                {
                    if (lightmapData.lightmapDir != null)
                        showDirLightmap = true;
                    if (lightmapData.shadowMask != null)
                        showShadowMask = true;
                }

                for (int i = 0; i < lightmaps.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(5);
                    lightmaps[i].lightmapColor = LightmapField(lightmaps[i].lightmapColor, i);
                    if (showDirLightmap)
                    {
                        GUILayout.Space(10);
                        lightmaps[i].lightmapDir = LightmapField(lightmaps[i].lightmapDir, i);
                    }
                    if (showShadowMask)
                    {
                        GUILayout.Space(10);
                        lightmaps[i].shadowMask = LightmapField(lightmaps[i].shadowMask, i);
                    }
                    GUILayout.Space(5);
                    GUILayout.BeginVertical();
                    GUILayout.Label("Index: " + i, EditorStyles.miniBoldLabel);

                    if (LightmapEditorSettings.lightmapper == LightmapEditorSettings.Lightmapper.ProgressiveCPU)
                    {
                        LightmapConvergence lc = Lightmapping.GetLightmapConvergence(i);

                        if (lc.IsValid())
                        {
                            GUILayout.Label("Occupied: " + InternalEditorUtility.CountToString((ulong)lc.occupiedTexelCount), EditorStyles.miniLabel);

                            GUIContent direct = EditorGUIUtility.TextContent("Direct: " + lc.minDirectSamples + " / " + lc.maxDirectSamples + " / " + lc.avgDirectSamples + "|min / max / avg samples per texel");
                            GUILayout.Label(direct, EditorStyles.miniLabel);

                            GUIContent gi = EditorGUIUtility.TextContent("Global Illumination: " + lc.minGISamples + " / " + lc.maxGISamples + " / " + lc.avgGISamples + "|min / max / avg samples per texel");
                            GUILayout.Label(gi, EditorStyles.miniLabel);
                        }
                        else
                        {
                            GUILayout.Label("Occupied: N/A", EditorStyles.miniLabel);
                            GUILayout.Label("Direct: N/A", EditorStyles.miniLabel);
                            GUILayout.Label("Global Illumination: N/A", EditorStyles.miniLabel);
                        }
                        float mraysPerSec = Lightmapping.GetLightmapBakePerformance(i);
                        if (mraysPerSec >= 0.0)
                            GUILayout.Label(mraysPerSec.ToString("0.00") + " mrays/sec", EditorStyles.miniLabel);
                        else
                            GUILayout.Label("N/A mrays/sec", EditorStyles.miniLabel);
                    }

                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        Texture2D LightmapField(Texture2D lightmap, int index)
        {
            Rect rect = GUILayoutUtility.GetRect(100, 100, EditorStyles.objectField);
            MenuSelectLightmapUsers(rect, index);
            Texture2D retval = EditorGUI.ObjectField(rect, lightmap, typeof(Texture2D), false) as Texture2D;
            if (index == m_SelectedLightmap && Event.current.type == EventType.Repaint)
                s_Styles.selectedLightmapHighlight.Draw(rect, false, false, false, false);

            return retval;
        }
    }
} // namespace
