using System;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    private const float ViewerMoveThresholdForChunkUpdate = 25f;

    private const float SqrViewerMoveThresholdForChunkUpdate =
        ViewerMoveThresholdForChunkUpdate * ViewerMoveThresholdForChunkUpdate;

    public LodInfo[] detailLevels;
    private static float _maxViewDst;
    public Transform viewer;
    public Material mapMaterial;
    private static Vector2 _viewerPosition;
    private Vector2 m_ViewerPositionOld;
    private static MapGenerator _mapGenerator;
    private int m_ChunkSize;
    private int m_ChunksVisibleInViewDst;

    private readonly Dictionary<Vector2, TerrainChunk> m_TerrainChunkDictionary =
        new Dictionary<Vector2, TerrainChunk>();

    private static readonly List<TerrainChunk> TerrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    private void Start()
    {
        _mapGenerator = FindObjectOfType<MapGenerator>();
        _maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        m_ChunkSize = _mapGenerator.mapChunkSize - 1;
        m_ChunksVisibleInViewDst = Mathf.RoundToInt(_maxViewDst / m_ChunkSize);
        UpdateVisibleChunks();
    }

    private void Update()
    {
        _viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / _mapGenerator.terrainData.uniformScale;
        if (!((m_ViewerPositionOld - _viewerPosition).sqrMagnitude > SqrViewerMoveThresholdForChunkUpdate)) return;
        m_ViewerPositionOld = _viewerPosition;
        UpdateVisibleChunks();
    }

    private void UpdateVisibleChunks()
    {
        foreach (var t in TerrainChunksVisibleLastUpdate) t.SetVisible(false);
        TerrainChunksVisibleLastUpdate.Clear();
        var currentChunkCoordX = Mathf.RoundToInt(_viewerPosition.x / m_ChunkSize);
        var currentChunkCoordY = Mathf.RoundToInt(_viewerPosition.y / m_ChunkSize);
        for (var yOffset = -m_ChunksVisibleInViewDst; yOffset <= m_ChunksVisibleInViewDst; yOffset++)
        for (var xOffset = -m_ChunksVisibleInViewDst; xOffset <= m_ChunksVisibleInViewDst; xOffset++)
        {
            var viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
            if (m_TerrainChunkDictionary.ContainsKey(viewedChunkCoord))
                m_TerrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
            else
                m_TerrainChunkDictionary.Add(viewedChunkCoord,
                    new TerrainChunk(viewedChunkCoord, m_ChunkSize, detailLevels, transform, mapMaterial));
        }
    }

    private class TerrainChunk
    {
        private readonly GameObject meshObject;
        private readonly Vector2 position;
        private Bounds bounds;
        private readonly MeshRenderer meshRenderer;
        private readonly MeshFilter meshFilter;
        private readonly MeshCollider meshCollider;
        private readonly LodInfo[] detailLevels;
        private readonly LodMesh[] lodMeshes;
        private readonly LodMesh collisionLODMesh;
        private MapData mapData;
        private bool mapDataReceived;
        private int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LodInfo[] detailLevels, Transform parent, Material material)
        {
            this.detailLevels = detailLevels;
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            var positionV3 = new Vector3(position.x, 0, position.y);
            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;
            meshObject.transform.position = positionV3 * _mapGenerator.terrainData.uniformScale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * _mapGenerator.terrainData.uniformScale;
            SetVisible(false);
            lodMeshes = new LodMesh[detailLevels.Length];
            for (var i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LodMesh(detailLevels[i].lod, UpdateTerrainChunk);
                if (detailLevels[i].useForCollider) collisionLODMesh = lodMeshes[i];
            }

            _mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        private void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;
            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (!mapDataReceived) return;
            var viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(_viewerPosition));
            var visible = viewerDstFromNearestEdge <= _maxViewDst;
            if (visible)
            {
                var lodIndex = 0;
                for (var i = 0; i < detailLevels.Length - 1; i++)
                    if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
                        lodIndex = i + 1;
                    else
                        break;
                if (lodIndex != previousLODIndex)
                {
                    var lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.HasMesh)
                    {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.Mesh;
                    }
                    else if (!lodMesh.HasRequestedMesh)
                    {
                        lodMesh.RequestMesh(mapData);
                    }
                }

                if (lodIndex == 0)
                {
                    if (collisionLODMesh.HasMesh)
                        meshCollider.sharedMesh = collisionLODMesh.Mesh;
                    else if (!collisionLODMesh.HasRequestedMesh) collisionLODMesh.RequestMesh(mapData);
                }

                TerrainChunksVisibleLastUpdate.Add(this);
            }

            SetVisible(visible);
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }

    private class LodMesh
    {
        public Mesh Mesh;
        public bool HasRequestedMesh;
        public bool HasMesh;
        private readonly int m_Lod;
        private readonly Action m_UpdateCallback;

        public LodMesh(int lod, Action updateCallback)
        {
            this.m_Lod = lod;
            this.m_UpdateCallback = updateCallback;
        }

        private void OnMeshDataReceived(MeshData meshData)
        {
            Mesh = meshData.CreateMesh();
            HasMesh = true;
            m_UpdateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            HasRequestedMesh = true;
            _mapGenerator.RequestMeshData(mapData, m_Lod, OnMeshDataReceived);
        }
    }

    [Serializable]
    public struct LodInfo
    {
        public int lod;
        public float visibleDstThreshold;
        public bool useForCollider;
    }
}