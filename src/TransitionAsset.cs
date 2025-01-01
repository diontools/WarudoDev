using UnityEngine;
using UnityEngine.UI;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Scenes;
using Warudo.Core.Utils;
using Warudo.Plugins.Core.Assets;
using Warudo.Plugins.Core.Utils;

#nullable enable

[AssetType(
    Id = "335a6099-413c-4b4c-a23f-8a257d1815f2",
    Title = "トランジション",
    Category = null,
    Singleton = false
)]
public class TransitionAsset : GameObjectAsset
{
    [DataInput]
    [Label("IMAGE_SOURCE")]
    [PreviewGallery]
    [AutoCompleteResource("Image")]
    public string? ImageSource;

    [Trigger]
    [Label("OPEN_IMAGES_FOLDER")]
    public void OpenImagesFolder()
    {
        var url = "file:///" + Application.streamingAssetsPath + "/Images";
        Debug.Log("Launching " + url);
        Application.OpenURL(url);
    }

    [DataInput]
    [Label("進行値")]
    [FloatSlider(0, 1)]
    public float Rate;

    private RectTransform? maskRectTransform;
    private RectTransform? imageRectTransform;
    private RawImage? rawImage;
    private ImageResource? createdImageResource;

    protected override GameObject CreateGameObject()
    {
        return new GameObject()
        {
            name = "ScreenCanvas",
        };
    }

    protected override void OnCreate()
    {
        base.OnCreate();

        var canvas = this.GameObject.GetOrAddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var canvasScaler = this.GameObject.GetOrAddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new(1, 1);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        canvasScaler.referencePixelsPerUnit = 100;
        this.GameObject.GetOrAddComponent<GraphicRaycaster>();
        // Log.UserError($"canvasScaler: {canvasScaler.referenceResolution}");

        var maskImageObject = new GameObject();
        maskImageObject.transform.parent = this.GameObject.transform;
        maskRectTransform = maskImageObject.GetOrAddComponent<RectTransform>();
        maskRectTransform.localPosition = new(0, -0.5f, 0);
        maskRectTransform.sizeDelta = new(3, 3);
        maskRectTransform.pivot = new(0.5f, 1);
        maskRectTransform.localRotation = Quaternion.Euler(0, 0, -100f);
        maskImageObject.GetOrAddComponent<CanvasRenderer>();
        var maskImage = maskImageObject.GetOrAddComponent<Image>();
        var mask = maskImageObject.GetOrAddComponent<Mask>();
        mask.showMaskGraphic = true;

        var imageObject = new GameObject();
        imageObject.transform.parent = maskImageObject.transform;
        imageRectTransform = imageObject.GetOrAddComponent<RectTransform>();
        imageRectTransform.localPosition = new Vector3(0, 0f, 0);
        imageRectTransform.sizeDelta = new Vector2(1, 1);
        imageRectTransform.pivot = new(0.5f, 0);
        imageRectTransform.localRotation = Quaternion.Euler(0, 0, 100f);
        imageObject.GetOrAddComponent<CanvasRenderer>();
        rawImage = imageObject.GetOrAddComponent<RawImage>();
        // rawImage.color = Color.blue;

        Watch(nameof(this.Rate), UpdateCanvas);
        UpdateCanvas();

        Watch(nameof(this.ImageSource), UpdateImageSource);
        UpdateImageSource();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        this.createdImageResource?.Destroy();
    }

    private void UpdateCanvas()
    {
        // var enabled = Enabled;
        // this.SetActive(enabled);
        // this.GameObject.SetActive(enabled);

        // if (!enabled) return;

        if (this.maskRectTransform != null)
        {
            this.maskRectTransform.localRotation = Quaternion.Euler(0, 0, -this.Rate * 360f);
        }

        if (this.imageRectTransform != null)
        {
            this.imageRectTransform.localRotation = Quaternion.Euler(0, 0, this.Rate * 360f);
        }
    }

    private void UpdateImageSource()
    {
        if (createdImageResource != null)
        {
            createdImageResource.Destroy();
            createdImageResource = null;
        }

        if (ImageSource.IsNullOrWhiteSpace())
        {
            return;
        }

        createdImageResource = Context.ResourceManager.ResolveResourceUri<ImageResource>(ImageSource);

        if (rawImage != null)
        {
            rawImage.texture = createdImageResource.GetTexture(Time.time);
        }
    }
}
