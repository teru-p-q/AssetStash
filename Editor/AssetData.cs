using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuonLib.AssetStash
{
    [Serializable]
    public class AssetData : ICloneable
    {
        public const string GroupType = "Group";
        public const string ExternalType = "External";
        public const string SceneObjectType = "SceneObject";

        // SceneObject の場合、Guid は所属シーンのアセット GUID、GlobalId は GlobalObjectId の文字列表現
        [SerializeField] public string Guid;
        [SerializeField] public int ID;
        [SerializeField] public string Name;
        [SerializeField] public string Memo;
        [SerializeField] public string Type;
        [SerializeField] public int ParentID;
        [SerializeField] public bool IsExpanded;
        [SerializeField] public string GlobalId;

        public bool IsGroup => Type == GroupType;
        public bool IsExternal => Type == ExternalType;
        public bool IsSceneObject => Type == SceneObjectType;

        public object Clone()
        {
            return new AssetData
            {
                Guid = Guid,
                ID = ID,
                Name = Name,
                Memo = Memo,
                Type = Type,
                ParentID = ParentID,
                IsExpanded = IsExpanded,
                GlobalId = GlobalId,
            };
        }
    }
}