using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuonLib.AssetStash
{
    [Serializable]
    public class AssetData : ICloneable
    {
        [SerializeField] public string Guid;
        [SerializeField] public int ID;
        [SerializeField] public string Name;
        [SerializeField] public string Memo;
        [SerializeField] public string Type;
        [SerializeField] public int ParentID;
        [SerializeField] public bool IsExpanded;

        public bool IsGroup => Type == "Group";
        public bool IsExternal => Type == "External";

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