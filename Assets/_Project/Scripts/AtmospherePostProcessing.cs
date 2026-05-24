using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class AtmospherePostProcessing : MonoBehaviour
{
    [SerializeField] Material atmosphereMaterial;
    [SerializeField] Transform planet;
    [SerializeField] Transform sun;

    [Header("Shader Parameters"), Space(5)]
    [SerializeField] float atmosphereRadius = 2f;
    [SerializeField] float planetRadius = 1f;
    [SerializeField] int numInScatteringPoints = 5;
    [SerializeField] int numOpticalDepthPoints = 5;
    [SerializeField] float intensity = 1;
    [SerializeField] float densityFalloff = 1;
    [SerializeField] Vector3 waveLengths = new Vector3(700, 530, 440);
    [SerializeField] float scatteringStrength = 1;


    Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth;
#if UNITY_EDITOR
        Camera.onPostRender += OnAnyPostRender;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        Camera.onPostRender -= OnAnyPostRender;
#endif
    }

#if UNITY_EDITOR
    void OnAnyPostRender(Camera cam)
    {
        if (cam.cameraType != CameraType.SceneView) return;
        if (atmosphereMaterial == null || planet == null) return;

        cam.depthTextureMode = DepthTextureMode.Depth;
        SetMaterialProps(cam);

        // Grab current scene view render into a temp RT
        RenderTexture active = RenderTexture.active;
        int w = cam.pixelWidth;
        int h = cam.pixelHeight;

        RenderTexture src = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);
        RenderTexture dst = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);

        src.DiscardContents();
        Graphics.CopyTexture(active != null ? active : cam.targetTexture, src);

        Graphics.Blit(src, dst, atmosphereMaterial);

        RenderTexture.active = active;
        Graphics.Blit(dst, active);

        RenderTexture.ReleaseTemporary(src);
        RenderTexture.ReleaseTemporary(dst);
    }
#endif

    void SetMaterialProps(Camera renderCam = null)
    {
        Vector3 camPos = renderCam != null ? renderCam.transform.position : cam.transform.position;
        atmosphereMaterial.SetVector("_CameraWorldPos", camPos);
        atmosphereMaterial.SetVector("planetCenter", planet.position);
        atmosphereMaterial.SetFloat("atmosphereRadius", atmosphereRadius);
        atmosphereMaterial.SetFloat("planetRadius", planetRadius);
        atmosphereMaterial.SetVector("dirToSun", (sun.position - planet.position).normalized);
        atmosphereMaterial.SetFloat("numInScatteringPoints", numInScatteringPoints);
        atmosphereMaterial.SetFloat("numOpticalDepthPoints", numOpticalDepthPoints);
        atmosphereMaterial.SetFloat("intensity", intensity);
        atmosphereMaterial.SetFloat("densityFalloff", densityFalloff);
        float sR = Mathf.Pow(400 / waveLengths.x, 4) * scatteringStrength;
        float sG = Mathf.Pow(400 / waveLengths.y, 4) * scatteringStrength;
        float sB = Mathf.Pow(400 / waveLengths.z, 4) * scatteringStrength;
        Vector3 scatteringCoefficients = new Vector3(sR, sG, sB);
        atmosphereMaterial.SetVector("scatteringCoefficients", scatteringCoefficients);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (atmosphereMaterial == null || planet == null)
        {
            Graphics.Blit(src, dest);
            return;
        }
        SetMaterialProps();
        Graphics.Blit(src, dest, atmosphereMaterial);
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
    }
}