#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(AutoRadialMenu.AutoRadialMenuNDMFPlugin))]

namespace AutoRadialMenu
{
    public sealed class AutoRadialMenuNDMFPlugin : Plugin<AutoRadialMenuNDMFPlugin>
    {
        private const string BuildRootName = "__AutoRadialMenu_BuildGenerated";

        public override string QualifiedName
        {
            get { return "com.muyu.auto-radial-menu"; }
        }

        public override string DisplayName
        {
            get { return "自动轮盘"; }
        }

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("自动轮盘：生成", GenerateMenus);
        }

        private static void GenerateMenus(BuildContext context)
        {
            if (context == null || context.AvatarRootTransform == null) return;

            var menus = context.AvatarRootTransform.GetComponentsInChildren<AutoRadialMenu>(true);
            foreach (var menu in menus)
            {
                try
                {
                    GenerateMenu(menu, context.AvatarRootTransform);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, menu);
                }
            }
        }

        private static void GenerateMenu(AutoRadialMenu menu, Transform avatarRoot)
        {
            var host = menu.gameObject;
            var manualInstaller = FindManualInstaller(menu);

            var oldBuildRoot = host.transform.Find(BuildRootName);
            if (oldBuildRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldBuildRoot.gameObject);
            }

            var buildRootObject = new GameObject(BuildRootName);
            buildRootObject.transform.SetParent(host.transform, false);

            var installer = buildRootObject.AddComponent<ModularAvatarMenuInstaller>();
            installer.installTargetMenu = manualInstaller != null && manualInstaller.enabled
                ? manualInstaller.installTargetMenu
                : null;

            var parameterName = string.IsNullOrWhiteSpace(menu.ParameterName)
                ? "__AutoRadial/" + Sanitize(host.name)
                : menu.ParameterName.Trim();

            var menuItem = buildRootObject.AddComponent<ModularAvatarMenuItem>();
            menuItem.label = string.IsNullOrWhiteSpace(menu.MenuLabel) ? host.name : menu.MenuLabel;
            menuItem.isSynced = true;
            menuItem.isSaved = true;
            menuItem.automaticValue = false;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                icon = ResolveIcon(menu),
                value = 1f,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
                subParameters = new[]
                {
                    new VRCExpressionsMenu.Control.Parameter { name = parameterName }
                },
                labels = Array.Empty<VRCExpressionsMenu.Control.Label>()
            };

            var parameters = buildRootObject.AddComponent<ModularAvatarParameters>();
            parameters.parameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    nameOrPrefix = parameterName,
                    remapTo = string.Empty,
                    internalParameter = false,
                    isPrefix = false,
                    syncType = ParameterSyncType.Float,
                    localOnly = false,
                    defaultValue = 0f,
                    saved = true,
                    hasExplicitDefaultValue = true
                }
            };

            var clip = new AnimationClip
            {
                name = "自动轮盘_" + Sanitize(menuItem.label),
                frameRate = 60f
            };

            var hasAnimation = menu.Mode == AutoRadialMenu.ActionMode.ObjectToggle
                ? BuildObjectAnimation(menu, avatarRoot, clip)
                : BuildMaterialAnimation(menu, avatarRoot, clip);

            if (!hasAnimation)
            {
                return;
            }

            var controller = CreateController(parameterName, clip, menuItem.label);
            var mergeAnimator = buildRootObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = false;
            mergeAnimator.deleteAttachedAnimator = true;
        }

        private static bool BuildObjectAnimation(AutoRadialMenu menu, Transform avatarRoot, AnimationClip clip)
        {
            var targets = ResolveObjectTargets(menu)
                .Where(target => target != null)
                .Distinct()
                .ToList();

            if (targets.Count == 0)
            {
                Debug.LogWarning("[自动轮盘] 开关对象模式没有配置任何对象。", menu);
                return false;
            }

            var offOffset = menu.IncludeOffOption ? 1 : 0;
            var stateCount = targets.Count + offOffset;
            if (stateCount <= 0) return false;

            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                var activeState = targetIndex + offOffset;
                var curve = CreateSteppedFloatCurve(
                    stateCount,
                    state => state == activeState ? 1f : 0f
                );

                var path = AnimationUtility.CalculateTransformPath(targets[targetIndex].transform, avatarRoot);
                clip.SetCurve(path, typeof(GameObject), "m_IsActive", curve);
            }

            return true;
        }

        private static bool BuildMaterialAnimation(AutoRadialMenu menu, Transform avatarRoot, AnimationClip clip)
        {
            var targets = ResolveMaterialTargets(menu)
                .Where(target => target != null && target.renderer != null)
                .ToList();
            if (targets.Count == 0)
            {
                Debug.LogWarning("[自动轮盘] 替换材质模式没有配置任何材质对象。", menu);
                return false;
            }

            var preparedTargets = new List<Tuple<SkinnedMeshRenderer, int, Material, List<Material>>>();
            var maxChoices = 0;

            foreach (var target in targets)
            {
                var renderer = target.renderer;
                var sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    Debug.LogWarning(
                        string.Format("[自动轮盘] “{0}”没有材质槽，已跳过。", renderer.name),
                        menu
                    );
                    continue;
                }

                var slotTargets = ResolveMaterialSlotTargets(target)
                    .Where(slotTarget => slotTarget != null)
                    .GroupBy(slotTarget => slotTarget.slotIndex)
                    .Select(group => group.First())
                    .ToList();

                foreach (var slotTarget in slotTargets)
                {
                    var slot = Mathf.Clamp(slotTarget.slotIndex, 0, sharedMaterials.Length - 1);
                    var original = sharedMaterials[slot];
                    if (original == null)
                    {
                        Debug.LogWarning(
                            string.Format("[自动轮盘] “{0}”的第 {1} 个材质为空，已跳过。", renderer.name, slot + 1),
                            menu
                        );
                        continue;
                    }

                    var choices = (slotTarget.materials ?? new List<Material>())
                        .Where(material => material != null && material != original)
                        .Distinct()
                        .ToList();

                    maxChoices = Mathf.Max(maxChoices, choices.Count);
                    preparedTargets.Add(Tuple.Create(renderer, slot, original, choices));
                }
            }

            if (preparedTargets.Count == 0)
            {
                Debug.LogWarning("[自动轮盘] 没有可用的材质对象。", menu);
                return false;
            }

            if (maxChoices == 0)
            {
                Debug.LogWarning("[自动轮盘] 替换材质模式没有配置任何新材质。", menu);
                return false;
            }

            var stateCount = maxChoices + 1;
            foreach (var prepared in preparedTargets)
            {
                var renderer = prepared.Item1;
                var slot = prepared.Item2;
                var original = prepared.Item3;
                var choices = prepared.Item4;
                var keyframes = new List<ObjectReferenceKeyframe>();

                for (var state = 0; state < stateCount; state++)
                {
                    Material material;
                    if (state == 0 || choices.Count == 0)
                    {
                        material = original;
                    }
                    else
                    {
                        material = choices[Mathf.Min(state - 1, choices.Count - 1)];
                    }

                    keyframes.Add(new ObjectReferenceKeyframe
                    {
                        time = state / (float)stateCount,
                        value = material
                    });
                }

                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = 1f,
                    value = choices.Count > 0 ? choices[choices.Count - 1] : original
                });

                var binding = new EditorCurveBinding
                {
                    path = AnimationUtility.CalculateTransformPath(renderer.transform, avatarRoot),
                    type = typeof(SkinnedMeshRenderer),
                    propertyName = string.Format("m_Materials.Array.data[{0}]", slot)
                };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());
            }

            return true;
        }

        private static List<AutoRadialMenu.MaterialSlotTarget> ResolveMaterialSlotTargets(
            AutoRadialMenu.MaterialTarget target)
        {
            if (target.slots != null && target.slots.Count > 0)
            {
                return target.slots;
            }

            return new List<AutoRadialMenu.MaterialSlotTarget>
            {
                new AutoRadialMenu.MaterialSlotTarget
                {
                    slotIndex = Mathf.Max(0, target.materialSlot),
                    materials = target.materials != null
                        ? new List<Material>(target.materials)
                        : new List<Material>()
                }
            };
        }

        private static AnimationCurve CreateSteppedFloatCurve(int stateCount, Func<int, float> valueAtState)
        {
            var keys = new List<Keyframe>();
            for (var state = 0; state < stateCount; state++)
            {
                keys.Add(new Keyframe(state / (float)stateCount, valueAtState(state)));
            }

            keys.Add(new Keyframe(1f, valueAtState(stateCount - 1)));

            var curve = new AnimationCurve(keys.ToArray());
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }

            return curve;
        }

        private static AnimatorController CreateController(string parameterName, AnimationClip clip, string displayName)
        {
            var safeName = "自动轮盘_" + Sanitize(displayName);
            var controller = new AnimatorController { name = safeName };
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = parameterName,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0f
            });

            controller.AddLayer(safeName);
            var layer = controller.layers[0];
            layer.name = safeName;
            layer.defaultWeight = 1f;

            var state = layer.stateMachine.AddState(safeName);
            state.motion = clip;
            state.writeDefaultValues = false;
            state.timeParameterActive = true;
            state.timeParameter = parameterName;
            layer.stateMachine.defaultState = state;

            return controller;
        }

        private static List<GameObject> ResolveObjectTargets(AutoRadialMenu menu)
        {
            if (menu.ObjectTargets != null && menu.ObjectTargets.Any(target => target != null))
            {
                return menu.ObjectTargets;
            }

            var legacy = menu.LegacyOptions != null && menu.LegacyOptions.Count > 0
                ? menu.LegacyOptions[0]
                : null;
            return legacy != null && legacy.targets != null
                ? legacy.targets
                : new List<GameObject>();
        }

        private static List<AutoRadialMenu.MaterialTarget> ResolveMaterialTargets(AutoRadialMenu menu)
        {
            if (menu.MaterialTargets != null &&
                menu.MaterialTargets.Any(target => target != null && target.renderer != null))
            {
                return menu.MaterialTargets;
            }

            if (menu.MaterialRenderer != null)
            {
                return new List<AutoRadialMenu.MaterialTarget>
                {
                    new AutoRadialMenu.MaterialTarget
                    {
                        renderer = menu.MaterialRenderer,
                        materialSlot = menu.MaterialSlot,
                        materials = menu.MaterialChoices != null
                            ? new List<Material>(menu.MaterialChoices)
                            : new List<Material>()
                    }
                };
            }

            var legacy = menu.LegacyOptions != null && menu.LegacyOptions.Count > 0
                ? menu.LegacyOptions[0]
                : null;
            if (legacy == null || legacy.materialRoot == null)
            {
                return new List<AutoRadialMenu.MaterialTarget>();
            }

            var renderer = legacy.materialRoot.GetComponent<SkinnedMeshRenderer>() ??
                           legacy.materialRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                return new List<AutoRadialMenu.MaterialTarget>();
            }

            var materials = (legacy.materialReplacements ?? new List<AutoRadialMenu.MaterialReplacement>())
                .Where(mapping => mapping != null && mapping.replacement != null)
                .Select(mapping => mapping.replacement)
                .Distinct()
                .ToList();

            return new List<AutoRadialMenu.MaterialTarget>
            {
                new AutoRadialMenu.MaterialTarget
                {
                    renderer = renderer,
                    materialSlot = 0,
                    materials = materials
                }
            };
        }

        private static Texture2D ResolveIcon(AutoRadialMenu menu)
        {
            if (menu.Icon != null) return menu.Icon;

            var legacy = menu.LegacyOptions != null && menu.LegacyOptions.Count > 0
                ? menu.LegacyOptions[0]
                : null;
            return legacy != null ? legacy.icon : null;
        }

        private static ModularAvatarMenuInstaller FindManualInstaller(AutoRadialMenu menu)
        {
            return menu.GetComponent<ModularAvatarMenuInstaller>();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "轮盘";

            var chars = value.Where(char.IsLetterOrDigit).ToArray();
            var result = new string(chars);
            return string.IsNullOrEmpty(result) ? "轮盘" : result;
        }
    }
}
#endif

