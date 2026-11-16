using System;
using UnityEngine;

/// <summary>
/// UI 프리팹과 분리하여 씬마다 다른 미니맵 지형의 월드 X/Z 좌표를 저장합니다
/// </summary>
[CreateAssetMenu(menuName = "DungeonCore/UI/Minimap Layout")]
public class RoomMinimapLayout : ScriptableObject
{
    [Serializable]
    public class Region
    {
        public string name;
        public Rect[] tiles = Array.Empty<Rect>();
        public bool circular;
        public Vector2 center;
        [Min(.1f)] public float radius = 24;
    }
    public Region[] regions = Array.Empty<Region>();
    [Min(16)] public float visibleWorldSpan = 64;
}
