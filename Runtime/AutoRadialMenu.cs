using System;
using System.Collections.Generic;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace AutoRadialMenu
{
    [AddComponentMenu("自动轮盘/自动轮盘")]
    public sealed class AutoRadialMenu : AvatarTagComponent
    {
        public enum ActionMode
        {
            ObjectToggle = 0,
            MaterialReplacement = 1
        }

        // 旧版数据结构保留，仅用于已有场景/Prefab 的兼容迁移。
        [Serializable]
        public sealed class MaterialReplacement
        {
            public Material source;
            public Material replacement;
        }

        [Serializable]
        public sealed class Option
        {
            public string label = "开启";
            public Texture2D icon;
            public List<GameObject> targets = new List<GameObject>();
            public GameObject materialRoot;
            public List<MaterialReplacement> materialReplacements = new List<MaterialReplacement>();
        }

        [Serializable]
        public sealed class MaterialSlotTarget
        {
            [Tooltip("材质在蒙皮网格渲染器中的索引。")]
            public int slotIndex;

            [Tooltip("按顺序对应大轮盘后续档位的替换材质。")]
            public List<Material> materials = new List<Material>();
        }

        [Serializable]
        public sealed class MaterialTarget
        {
            [Tooltip("这个材质目标使用的蒙皮网格渲染器。")]
            public SkinnedMeshRenderer renderer;

            [Tooltip("这个对象的所有材质槽及各自的替换材质。")]
            public List<MaterialSlotTarget> slots = new List<MaterialSlotTarget>();

            // 旧版 MaterialTarget 兼容字段：旧版一个对象只能控制一个材质槽。
            [HideInInspector] public int materialSlot;
            [HideInInspector] public List<Material> materials = new List<Material>();
        }

        [Tooltip("显示在 VRChat 表情菜单里的大轮盘名称。")]
        [SerializeField] private string menuLabel = "自动轮盘";

        [Tooltip("大轮盘使用的 Float 参数名。可以自己填写；留空时自动生成。")]
        [SerializeField] private string parameterName = "";

        [Tooltip("大轮盘图标。")]
        [SerializeField] private Texture2D icon;

        [Tooltip("选择这个大轮盘转动时要控制什么。")]
        [SerializeField] private ActionMode mode = ActionMode.ObjectToggle;

        [Tooltip("开关对象模式中，轮盘 0 区间为全部关闭。")]
        [SerializeField] private bool includeOffOption = true;

        [Tooltip("开关对象模式下，轮盘依次选择并单独开启这些对象。")]
        [SerializeField] private List<GameObject> objectTargets = new List<GameObject>();

        [Tooltip("材质替换模式下由同一个大轮盘同步控制的多个材质对象。")]
        [SerializeField] private List<MaterialTarget> materialTargets = new List<MaterialTarget>();

        // 以下字段只为旧版序列化兼容保留，不再显示或参与新版配置。
        [HideInInspector, SerializeField] private SkinnedMeshRenderer materialRenderer;
        [HideInInspector, SerializeField] private int materialSlot;
        [HideInInspector, SerializeField] private List<Material> materialChoices = new List<Material>();
        [HideInInspector, SerializeField] private List<Option> options = new List<Option>();

        public string MenuLabel
        {
            get { return menuLabel; }
            set { menuLabel = value; }
        }

        public string ParameterName
        {
            get { return parameterName; }
            set { parameterName = value; }
        }

        public Texture2D Icon
        {
            get { return icon; }
            set { icon = value; }
        }

        public ActionMode Mode
        {
            get { return mode; }
            set { mode = value; }
        }

        public bool IncludeOffOption
        {
            get { return includeOffOption; }
            set { includeOffOption = value; }
        }

        public List<GameObject> ObjectTargets
        {
            get { return objectTargets; }
        }

        public List<MaterialTarget> MaterialTargets
        {
            get { return materialTargets; }
        }

        public SkinnedMeshRenderer MaterialRenderer
        {
            get { return materialRenderer; }
            set { materialRenderer = value; }
        }

        public int MaterialSlot
        {
            get { return materialSlot; }
            set { materialSlot = value; }
        }

        public List<Material> MaterialChoices
        {
            get { return materialChoices; }
        }

        public List<Option> LegacyOptions
        {
            get { return options; }
        }
    }
}

