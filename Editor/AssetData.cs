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

        [SerializeField] public string Guid;
        [SerializeField] public int ID;
        [SerializeField] public string Name;
        [SerializeField] public string Memo;
        [SerializeField] public string Type;
        [SerializeField] public int ParentID;
        [SerializeField] public bool IsExpanded;

        public bool IsGroup => Type == GroupType;
        public bool IsExternal => Type == ExternalType;

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
            };
        }
    }
}