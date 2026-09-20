#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AutoRadialMenu
{
    [CustomEditor(typeof(AutoRadialMenu))]
    internal sealed class AutoRadialMenuEditor : Editor
    {
        private const float DropAreaHeight = 30f;
        private readonly HashSet<string> initializedFoldouts = new HashSet<string>();

        private void OnEnable()
        {
            TryMigrateLegacyData();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("轮盘设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("menuLabel"), new GUIContent("菜单名称"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parameterName"), new GUIContent("参数名"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"), new GUIContent("图标"));

            var mode = serializedObject.FindProperty("mode");
            mode.enumValueIndex = EditorGUILayout.Popup(
                "功能",
                mode.enumValueIndex,
                new[] { "开关对象", "替换材质" }
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            if ((AutoRadialMenu.ActionMode)mode.enumValueIndex == AutoRadialMenu.ActionMode.ObjectToggle)
            {
                DrawObjectMode();
            }
            else
            {
                DrawMaterialMode();
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("构建时自动生成 VRChat 大轮盘，不修改当前编辑场景。", MessageType.Info);
            DrawInstallTarget();
        }

        private void DrawObjectMode()
        {
            EditorGUILayout.LabelField("开关对象", EditorStyles.boldLabel);

            var includeOff = serializedObject.FindProperty("includeOffOption");
            EditorGUILayout.PropertyField(includeOff, new GUIContent("包含全关区间"));

            var targets = serializedObject.FindProperty("objectTargets");
            EditorGUILayout.PropertyField(targets, new GUIContent("轮盘对象"), true);
            DrawDropArea(
                "将多个对象拖到这里",
                reference => reference is GameObject,
                objects => AddGameObjects(targets, objects)
            );

            var count = targets.arraySize + (includeOff.boolValue ? 1 : 0);
            if (targets.arraySize == 0)
            {
                EditorGUILayout.HelpBox("请至少加入一个要由轮盘控制的对象。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    includeOff.boolValue
                        ? string.Format("轮盘会被分成 {0} 个区间：全关 + {1} 个对象；每次只开启一个对象。", count, targets.arraySize)
                        : string.Format("轮盘会被分成 {0} 个区间；每次只开启一个对象。", count),
                    MessageType.None
                );
            }
        }

        private void DrawMaterialMode()
        {
            EditorGUILayout.LabelField("材质替换", EditorStyles.boldLabel);

            var targets = serializedObject.FindProperty("materialTargets");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("批量添加", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("在层级里多选对象后直接拖到下方，一次生成多个材质对象。", EditorStyles.wordWrappedMiniLabel);
            DrawDropArea(
                "拖入多个对象 / 蒙皮网格渲染器",
                reference => reference is GameObject || reference is SkinnedMeshRenderer,
                objects => AddMaterialTargets(targets, objects)
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            if (targets.arraySize > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("材质对象", EditorStyles.boldLabel);
                if (GUILayout.Button("全部展开", GUILayout.Width(72)))
                {
                    SetAllMaterialTargetFoldouts(targets, true);
                }
                if (GUILayout.Button("全部折叠", GUILayout.Width(72)))
                {
                    SetAllMaterialTargetFoldouts(targets, false);
                }
                EditorGUILayout.EndHorizontal();
            }

            for (var i = 0; i < targets.arraySize; i++)
            {
                DrawMaterialTargetCard(targets, i);
            }

            if (GUILayout.Button("＋ 添加空材质对象"))
            {
                var index = targets.arraySize++;
                var item = targets.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("renderer").objectReferenceValue = null;
                item.FindPropertyRelative("slots").ClearArray();
                item.FindPropertyRelative("materialSlot").intValue = 0;
                item.FindPropertyRelative("materials").ClearArray();
                item.isExpanded = true;
            }

            if (targets.arraySize == 0)
            {
                EditorGUILayout.HelpBox("还没有材质对象。可以批量拖入，也可以手动添加。", MessageType.Warning);
                return;
            }

            var lengths = new List<int>();
            var materialCount = 0;
            for (var i = 0; i < targets.arraySize; i++)
            {
                var slots = targets.GetArrayElementAtIndex(i).FindPropertyRelative("slots");
                materialCount += slots.arraySize;
                for (var slot = 0; slot < slots.arraySize; slot++)
                {
                    lengths.Add(
                        slots.GetArrayElementAtIndex(slot)
                            .FindPropertyRelative("materials")
                            .arraySize
                    );
                }
            }

            var maxChoices = lengths.Count > 0 ? lengths.Max() : 0;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                string.Format(
                    "{0} 个对象  ·  {1} 个材质  ·  {2} 个轮盘档位",
                    targets.arraySize,
                    materialCount,
                    maxChoices + 1
                ),
                MessageType.None
            );

            if (maxChoices == 0)
            {
                EditorGUILayout.HelpBox("还没有添加替换材质。", MessageType.Warning);
            }
            else if (lengths.Distinct().Count() > 1)
            {
                EditorGUILayout.HelpBox(
                    "不同材质的替换数量不一致：较短的列表在后续档位保持最后一个材质。",
                    MessageType.Info
                );
            }
        }

        private void DrawMaterialTargetCard(SerializedProperty targets, int index)
        {
            var item = targets.GetArrayElementAtIndex(index);
            var rendererProperty = item.FindPropertyRelative("renderer");
            var renderer = rendererProperty.objectReferenceValue as SkinnedMeshRenderer;
            if (renderer != null)
            {
                EnsureMaterialSlots(item, renderer);
            }
            var slots = item.FindPropertyRelative("slots");

            var foldoutKey = item.propertyPath;
            if (initializedFoldouts.Add(foldoutKey))
            {
                item.isExpanded = true;
            }

            var objectName = renderer != null ? renderer.name : "未指定对象";
            var maxReplacements = 0;
            for (var slot = 0; slot < slots.arraySize; slot++)
            {
                maxReplacements = Mathf.Max(
                    maxReplacements,
                    slots.GetArrayElementAtIndex(slot).FindPropertyRelative("materials").arraySize
                );
            }
            var summary = string.Format(
                "{0}  ·  {1} 个材质  ·  最多 {2} 个替换档",
                objectName,
                slots.arraySize,
                maxReplacements
            );

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, summary, true, EditorStyles.foldoutHeader);

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(26)))
                {
                    targets.MoveArrayElement(index, index - 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(index >= targets.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(26)))
                {
                    targets.MoveArrayElement(index, index + 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("×", GUILayout.Width(26)))
            {
                targets.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            if (item.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(rendererProperty, new GUIContent("对象"));
                if (EditorGUI.EndChangeCheck())
                {
                    renderer = rendererProperty.objectReferenceValue as SkinnedMeshRenderer;
                    RebuildMaterialSlots(item, renderer, null);
                }

                renderer = rendererProperty.objectReferenceValue as SkinnedMeshRenderer;
                if (renderer == null)
                {
                    EditorGUILayout.HelpBox("拖入一个含蒙皮网格渲染器的对象。", MessageType.Warning);
                }
                else if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
                {
                    EditorGUILayout.HelpBox("该对象没有材质槽。", MessageType.Warning);
                }
                else
                {
                    EnsureMaterialSlots(item, renderer);
                    slots = item.FindPropertyRelative("slots");
                    EditorGUILayout.LabelField(
                        string.Format("已识别 {0} 个材质", slots.arraySize),
                        EditorStyles.miniLabel
                    );

                    for (var slot = 0; slot < slots.arraySize; slot++)
                    {
                        DrawMaterialSlotCard(slots.GetArrayElementAtIndex(slot), renderer, slot);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        private void DrawMaterialSlotCard(
            SerializedProperty slotItem,
            SkinnedMeshRenderer renderer,
            int displayIndex)
        {
            var slotIndexProperty = slotItem.FindPropertyRelative("slotIndex");
            var materials = slotItem.FindPropertyRelative("materials");
            var slotIndex = Mathf.Clamp(slotIndexProperty.intValue, 0, renderer.sharedMaterials.Length - 1);
            slotIndexProperty.intValue = slotIndex;
            var original = renderer.sharedMaterials[slotIndex];

            var foldoutKey = slotItem.propertyPath;
            if (initializedFoldouts.Add(foldoutKey))
            {
                slotItem.isExpanded = true;
            }

            var originalName = original != null ? original.name : "空材质";
            var title = string.Format(
                "材质 {0}  ·  {1}  ·  {2} 个替换材质",
                displayIndex + 1,
                originalName,
                materials.arraySize
            );

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            slotItem.isExpanded = EditorGUILayout.Foldout(
                slotItem.isExpanded,
                title,
                true,
                EditorStyles.foldoutHeader
            );

            if (slotItem.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("原材质", original, typeof(Material), false);
                DrawMaterialChoices(materials);
                DrawDropArea(
                    "拖入多个替换材质，按轮盘顺序加入",
                    reference => reference is Material,
                    objects => AddMaterials(materials, objects)
                );
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialChoices(SerializedProperty materials)
        {
            EditorGUILayout.LabelField("轮盘材质", EditorStyles.boldLabel);

            for (var i = 0; i < materials.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("档位 " + (i + 1));
                EditorGUILayout.PropertyField(materials.GetArrayElementAtIndex(i), GUIContent.none);

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24)))
                    {
                        materials.MoveArrayElement(i, i - 1);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(i >= materials.arraySize - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(24)))
                    {
                        materials.MoveArrayElement(i, i + 1);
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("−", GUILayout.Width(24)))
                {
                    DeleteObjectReferenceArrayElement(materials, i);
                    serializedObject.ApplyModifiedProperties();
                    EditorGUILayout.EndHorizontal();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("＋ 添加空档位"))
            {
                var index = materials.arraySize++;
                materials.GetArrayElementAtIndex(index).objectReferenceValue = null;
            }

            if (materials.arraySize == 0)
            {
                EditorGUILayout.LabelField("还没有替换材质。", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private static void SetAllMaterialTargetFoldouts(SerializedProperty targets, bool expanded)
        {
            for (var i = 0; i < targets.arraySize; i++)
            {
                targets.GetArrayElementAtIndex(i).isExpanded = expanded;
            }
        }

        private static void DeleteObjectReferenceArrayElement(SerializedProperty array, int index)
        {
            var oldSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == oldSize && index < array.arraySize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

        private void DrawInstallTarget()
        {
            var menu = (AutoRadialMenu)target;
            var installer = menu.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller>();
            if (installer == null)
            {
                EditorGUILayout.HelpBox(
                    "需要指定安装位置时，可以手动添加 Modular Avatar 菜单安装器并选择目标菜单；否则安装到头像根菜单。",
                    MessageType.Info
                );
            }
            else if (installer.installTargetMenu == null)
            {
                EditorGUILayout.HelpBox(
                    "已找到 Modular Avatar 菜单安装器，但尚未选择目标菜单；当前会安装到头像根菜单。",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.ObjectField(
                    "当前安装目标",
                    installer.installTargetMenu,
                    typeof(VRCExpressionsMenu),
                    false
                );
            }
        }

        private void DrawDropArea(
            string label,
            Func<UnityEngine.Object, bool> filter,
            Action<UnityEngine.Object[]> addObjects)
        {
            var rect = GUILayoutUtility.GetRect(0, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(rect, label, EditorStyles.helpBox);

            var current = Event.current;
            if ((current.type != EventType.DragUpdated && current.type != EventType.DragPerform) ||
                !rect.Contains(current.mousePosition))
            {
                return;
            }

            var objects = DragAndDrop.objectReferences.Where(filter).ToArray();
            if (objects.Length == 0)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                addObjects(objects);
            }

            current.Use();
        }

        private void AddGameObjects(SerializedProperty targets, IEnumerable<UnityEngine.Object> objects)
        {
            var values = objects.OfType<GameObject>().ToList();
            if (values.Count == 0) return;

            Undo.RecordObject(target, "添加轮盘对象");
            var existing = new HashSet<GameObject>();
            for (var i = 0; i < targets.arraySize; i++)
            {
                existing.Add(targets.GetArrayElementAtIndex(i).objectReferenceValue as GameObject);
            }

            foreach (var value in values)
            {
                if (!existing.Add(value)) continue;
                var index = targets.arraySize++;
                targets.GetArrayElementAtIndex(index).objectReferenceValue = value;
            }

            SaveSerializedChanges();
        }

        private void AddMaterialTargets(SerializedProperty targets, IEnumerable<UnityEngine.Object> objects)
        {
            var renderers = objects
                .SelectMany(ResolveRenderers)
                .Where(renderer => renderer != null)
                .Distinct()
                .ToList();
            if (renderers.Count == 0) return;

            Undo.RecordObject(target, "添加材质对象");
            var existing = new HashSet<SkinnedMeshRenderer>();
            for (var i = 0; i < targets.arraySize; i++)
            {
                existing.Add(
                    targets.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("renderer")
                        .objectReferenceValue as SkinnedMeshRenderer
                );
            }

            foreach (var renderer in renderers)
            {
                if (!existing.Add(renderer)) continue;

                var index = targets.arraySize++;
                var item = targets.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("renderer").objectReferenceValue = renderer;
                item.FindPropertyRelative("slots").ClearArray();
                item.FindPropertyRelative("materialSlot").intValue = 0;
                item.FindPropertyRelative("materials").ClearArray();
                RebuildMaterialSlots(item, renderer, null);
            }

            SaveSerializedChanges();
        }

        private void AssignRenderer(
            SerializedProperty rendererProperty,
            SerializedProperty slotProperty,
            IEnumerable<UnityEngine.Object> objects)
        {
            var renderer = objects.Select(ResolveRenderer).FirstOrDefault(value => value != null);
            if (renderer == null) return;

            Undo.RecordObject(target, "设置材质对象");
            rendererProperty.objectReferenceValue = renderer;
            slotProperty.intValue = 0;
            SaveSerializedChanges();
        }

        private static SkinnedMeshRenderer ResolveRenderer(UnityEngine.Object value)
        {
            return ResolveRenderers(value).FirstOrDefault();
        }

        private static IEnumerable<SkinnedMeshRenderer> ResolveRenderers(UnityEngine.Object value)
        {
            var renderer = value as SkinnedMeshRenderer;
            if (renderer != null)
            {
                yield return renderer;
                yield break;
            }

            var gameObject = value as GameObject;
            if (gameObject == null) yield break;

            foreach (var childRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (childRenderer != null)
                {
                    yield return childRenderer;
                }
            }
        }

        private static bool EnsureMaterialSlots(SerializedProperty item, SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return false;

            var sharedMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
            var slots = item.FindPropertyRelative("slots");
            var isCurrent = slots.arraySize == sharedMaterials.Length;
            if (isCurrent)
            {
                for (var i = 0; i < slots.arraySize; i++)
                {
                    if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("slotIndex").intValue != i)
                    {
                        isCurrent = false;
                        break;
                    }
                }
            }

            if (isCurrent) return false;

            var preserved = new Dictionary<int, List<Material>>();
            for (var i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var slotIndex = slot.FindPropertyRelative("slotIndex").intValue;
                var materials = slot.FindPropertyRelative("materials");
                preserved[slotIndex] = ReadMaterialReferences(materials);
            }

            if (slots.arraySize == 0)
            {
                var legacyMaterials = item.FindPropertyRelative("materials");
                if (legacyMaterials != null && legacyMaterials.arraySize > 0)
                {
                    var legacySlot = Mathf.Max(0, item.FindPropertyRelative("materialSlot").intValue);
                    preserved[legacySlot] = ReadMaterialReferences(legacyMaterials);
                }
            }

            RebuildMaterialSlots(item, renderer, preserved);
            return true;
        }

        private static void RebuildMaterialSlots(
            SerializedProperty item,
            SkinnedMeshRenderer renderer,
            Dictionary<int, List<Material>> preserved)
        {
            var slots = item.FindPropertyRelative("slots");
            slots.ClearArray();
            if (renderer == null) return;

            var sharedMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
            for (var slotIndex = 0; slotIndex < sharedMaterials.Length; slotIndex++)
            {
                var index = slots.arraySize++;
                var slot = slots.GetArrayElementAtIndex(index);
                slot.FindPropertyRelative("slotIndex").intValue = slotIndex;
                var materials = slot.FindPropertyRelative("materials");
                materials.ClearArray();

                List<Material> values;
                if (preserved == null || !preserved.TryGetValue(slotIndex, out values)) continue;
                foreach (var material in values.Where(material => material != null))
                {
                    var materialIndex = materials.arraySize++;
                    materials.GetArrayElementAtIndex(materialIndex).objectReferenceValue = material;
                }
            }
        }

        private static List<Material> ReadMaterialReferences(SerializedProperty materials)
        {
            var result = new List<Material>();
            if (materials == null) return result;

            for (var i = 0; i < materials.arraySize; i++)
            {
                var material = materials.GetArrayElementAtIndex(i).objectReferenceValue as Material;
                if (material != null)
                {
                    result.Add(material);
                }
            }
            return result;
        }

        private void AddMaterials(SerializedProperty materials, IEnumerable<UnityEngine.Object> objects)
        {
            var values = objects.OfType<Material>().ToList();
            if (values.Count == 0) return;

            Undo.RecordObject(target, "添加替换材质");
            var existing = new HashSet<Material>();
            for (var i = 0; i < materials.arraySize; i++)
            {
                existing.Add(materials.GetArrayElementAtIndex(i).objectReferenceValue as Material);
            }

            foreach (var value in objects)
            {
                var material = value as Material;
                if (material == null) continue;
                if (!existing.Add(material)) continue;
                var index = materials.arraySize++;
                materials.GetArrayElementAtIndex(index).objectReferenceValue = material;
            }

            SaveSerializedChanges();
        }

        private void TryMigrateLegacyData()
        {
            serializedObject.Update();

            var options = serializedObject.FindProperty("options");
            var changed = false;
            var legacy = options != null && options.arraySize > 0
                ? options.GetArrayElementAtIndex(0)
                : null;

            var icon = serializedObject.FindProperty("icon");
            var legacyIcon = legacy != null ? legacy.FindPropertyRelative("icon") : null;
            if (icon.objectReferenceValue == null &&
                legacyIcon != null &&
                legacyIcon.objectReferenceValue != null)
            {
                icon.objectReferenceValue = legacyIcon.objectReferenceValue;
                changed = true;
            }

            var objectTargets = serializedObject.FindProperty("objectTargets");
            var legacyTargets = legacy != null ? legacy.FindPropertyRelative("targets") : null;
            if (objectTargets.arraySize == 0 &&
                legacyTargets != null &&
                legacyTargets.arraySize > 0)
            {
                for (var i = 0; i < legacyTargets.arraySize; i++)
                {
                    var value = legacyTargets.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (value == null) continue;
                    var index = objectTargets.arraySize++;
                    objectTargets.GetArrayElementAtIndex(index).objectReferenceValue = value;
                }
                changed = true;
            }

            var materialTargets = serializedObject.FindProperty("materialTargets");
            if (materialTargets.arraySize == 0)
            {
                var oldRenderer = serializedObject.FindProperty("materialRenderer")
                    .objectReferenceValue as SkinnedMeshRenderer;
                var oldSlot = serializedObject.FindProperty("materialSlot").intValue;
                var oldChoices = serializedObject.FindProperty("materialChoices");

                if (oldRenderer != null)
                {
                    AddMigratedMaterialTarget(materialTargets, oldRenderer, oldSlot, oldChoices);
                    changed = true;
                }
                else if (legacy != null)
                {
                    var legacyRoot = legacy.FindPropertyRelative("materialRoot").objectReferenceValue as GameObject;
                    var legacyRenderer = legacyRoot != null ? ResolveRenderer(legacyRoot) : null;
                    var legacyMappings = legacy.FindPropertyRelative("materialReplacements");

                    if (legacyRenderer != null)
                    {
                        var index = materialTargets.arraySize++;
                        var item = materialTargets.GetArrayElementAtIndex(index);
                        item.FindPropertyRelative("renderer").objectReferenceValue = legacyRenderer;
                        item.FindPropertyRelative("materialSlot").intValue = 0;
                        var materials = item.FindPropertyRelative("materials");
                        materials.ClearArray();

                        var existing = new HashSet<Material>();
                        var migrated = new List<Material>();
                        for (var i = 0; i < legacyMappings.arraySize; i++)
                        {
                            var replacement = legacyMappings.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("replacement").objectReferenceValue as Material;
                            if (replacement == null || !existing.Add(replacement)) continue;
                            var materialIndex = materials.arraySize++;
                            materials.GetArrayElementAtIndex(materialIndex).objectReferenceValue = replacement;
                            migrated.Add(replacement);
                        }
                        RebuildMaterialSlots(
                            item,
                            legacyRenderer,
                            new Dictionary<int, List<Material>> { { 0, migrated } }
                        );
                        changed = true;
                    }
                }
            }

            for (var i = 0; i < materialTargets.arraySize; i++)
            {
                var item = materialTargets.GetArrayElementAtIndex(i);
                var renderer = item.FindPropertyRelative("renderer").objectReferenceValue as SkinnedMeshRenderer;
                if (renderer != null && EnsureMaterialSlots(item, renderer))
                {
                    changed = true;
                }
            }

            if (objectTargets.arraySize == 0 &&
                materialTargets.arraySize > 0)
            {
                var mode = serializedObject.FindProperty("mode");
                if (mode.enumValueIndex == (int)AutoRadialMenu.ActionMode.ObjectToggle)
                {
                    mode.enumValueIndex = (int)AutoRadialMenu.ActionMode.MaterialReplacement;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSerializedChanges();
            }
        }

        private static void AddMigratedMaterialTarget(
            SerializedProperty targets,
            SkinnedMeshRenderer renderer,
            int slot,
            SerializedProperty oldChoices)
        {
            var index = targets.arraySize++;
            var item = targets.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("renderer").objectReferenceValue = renderer;
            var clampedSlot = renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0
                ? Mathf.Clamp(slot, 0, renderer.sharedMaterials.Length - 1)
                : 0;
            item.FindPropertyRelative("materialSlot").intValue = clampedSlot;
            var materials = item.FindPropertyRelative("materials");
            materials.ClearArray();
            var migrated = new List<Material>();

            for (var i = 0; i < oldChoices.arraySize; i++)
            {
                var material = oldChoices.GetArrayElementAtIndex(i).objectReferenceValue as Material;
                if (material == null) continue;
                var materialIndex = materials.arraySize++;
                materials.GetArrayElementAtIndex(materialIndex).objectReferenceValue = material;
                migrated.Add(material);
            }

            RebuildMaterialSlots(
                item,
                renderer,
                new Dictionary<int, List<Material>> { { clampedSlot, migrated } }
            );
        }

        private void SaveSerializedChanges()
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
#endif

