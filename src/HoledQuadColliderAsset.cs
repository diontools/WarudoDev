using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Scenes;
using Warudo.Plugins.Core.Assets;
using Warudo.Plugins.Core.Assets.Mixins;

#nullable enable

[AssetType(
    Id = "d790ebc1-46f5-4343-8e24-e86337818b64",
    Title = "穴あきQuadコライダー",
    Category = "CATEGORY_PROP",
    Singleton = false
)]
public class HoledQuadColliderAsset : GameObjectAsset
{
    private MeshRenderer? renderer;
    private MeshCollider? collider;
    private MeshFilter? filter;
    
    [Mixin(33)]
    public Attachable? Attachable;

    [DataInput]
    [Label("表示")]
    public bool Visibled = true;

    [DataInput]
    [Label("穴の半径")]
    [FloatSlider(0, 0.5f)]
    public float Radius = 0.2f;

    protected override GameObject CreateGameObject()
    {
        return GameObject.CreatePrimitive(PrimitiveType.Quad);
    }

    protected override void OnCreate()
    {
        base.OnCreate();

        // Log.UserError(Utility.DumpGameObject(this.GameObject));

        this.Attachable?.Initialize(this.GameObject);
        this.renderer = this.GameObject.GetComponent<MeshRenderer>();
        this.collider = this.GameObject.GetComponent<MeshCollider>();
        this.filter = this.GameObject.GetComponent<MeshFilter>();

        Watch(nameof(Visibled), ApplyRenderer);
        ApplyRenderer();

        Watch(nameof(Enabled), ApplyEnabled);
        ApplyEnabled();

        Watch(nameof(Radius), ApplyRadius);
        ApplyRadius();

        void ApplyRenderer()
        {
            if (this.renderer is { } renderer)
            {
                renderer.enabled = this.Visibled;
            }
        }

        void ApplyEnabled()
        {
            var enabled = this.Enabled;
            this.SetActive(enabled);
            this.GameObject.SetActive(enabled);
        }

        void ApplyRadius()
        {
            var mesh = CreateMesh(this.Radius);
            this.filter.mesh = mesh;
            this.collider.sharedMesh = mesh;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    private static Mesh CreateMesh(float holeRadius)
    {
        const int circleSegments = 8;
        const float quadSize = 1f;

        // 頂点の計算
        var vertexCount = 4 + circleSegments; // 4つの四隅 + 円周上の頂点
        var vertices = new Vector3[vertexCount];
        var uv = new Vector2[vertexCount];
        var triangles = new int[vertexCount * 3];

        // Quadの四隅
        var halfSize = quadSize / 2;
        vertices[0] = new Vector3(halfSize, halfSize, 0);
        vertices[1] = new Vector3(-halfSize, halfSize, 0);
        vertices[2] = new Vector3(-halfSize, -halfSize, 0);
        vertices[3] = new Vector3(halfSize, -halfSize, 0);

        uv[0] = new Vector2(0, 0);
        uv[1] = new Vector2(0, 1);
        uv[2] = new Vector2(1, 1);
        uv[3] = new Vector2(1, 0);

        // 円周上の頂点
        for (int i = 0; i < circleSegments; i++)
        {
            var angle = 2 * Mathf.PI * i / circleSegments;
            var x = Mathf.Cos(angle) * holeRadius;
            var y = Mathf.Sin(angle) * holeRadius;
            vertices[4 + i] = new Vector3(x, y, 0);
            uv[4 + i] = new Vector2((x + halfSize) / quadSize, (y + halfSize) / quadSize);
        }

        // 三角形
        for (var i = 0; i < 4; i++)
        {
            var offset = i * 9;
            triangles[offset + 0] = i;
            triangles[offset + 1] = (i - 1 + 4) % 4;
            triangles[offset + 2] = 4 + i * 2;

            triangles[offset + 3] = i;
            triangles[offset + 4] = 4 + i * 2;
            triangles[offset + 5] = triangles[offset + 4] + 1;

            triangles[offset + 6] = i;
            triangles[offset + 7] = triangles[offset + 5];
            triangles[offset + 8] = (triangles[offset + 7] + 1) is { } v && v < 12 ? v : 4;
        }

        // メッシュに適用
        var mesh = new Mesh
        {
            vertices = vertices,
            uv = uv,
            triangles = triangles,
        };

        mesh.RecalculateNormals();
        return mesh;
    }
}
