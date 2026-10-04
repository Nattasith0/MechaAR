using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Optional artwork for library cards; the original card keeps owning navigation.</summary>
public sealed class LibraryCardArtwork : MonoBehaviour
{
    [Serializable] struct ArtworkCrop { public Texture texture; public Rect uv; }
    [SerializeField] RawImage image;
    [SerializeField] AspectRatioFitter aspectRatio;
    [SerializeField] GameObject fallback;
    // Generated from existing image transparency by the explicit editor UI patch.
    // Future textures retain their complete image without changing catalog behavior.
    [SerializeField] ArtworkCrop[] artworkCrops = Array.Empty<ArtworkCrop>();

    public void Bind(Texture texture)
    {
        bool hasArtwork = texture != null && image != null;
        Rect uv = new Rect(0, 0, 1, 1);
        if (hasArtwork && artworkCrops != null)
            foreach (var crop in artworkCrops)
                if (crop.texture == texture && crop.uv.width > 0 && crop.uv.height > 0) { uv = crop.uv; break; }
        if (image != null)
        {
            image.texture = texture;
            image.uvRect = uv;
            image.gameObject.SetActive(hasArtwork);
        }
        if (hasArtwork && aspectRatio != null)
            aspectRatio.aspectRatio = texture.width * uv.width / Mathf.Max(1, texture.height * uv.height);
        if (fallback != null) fallback.SetActive(!hasArtwork);
    }
}
