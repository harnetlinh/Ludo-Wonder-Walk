using UnityEngine;
using UnityEngine.XR;

public class PerformanceInitializer : MonoBehaviour
{
    [Header("Basic Settings")]
    [Tooltip("Target FPS cho game.")]
    public int targetFPS = 72;

    [Tooltip("Tắt VSync để giảm load.")]
    public bool disableVSync = true;

    [Tooltip("Hệ số giảm render scale (1 = mặc định, <1 = nhẹ hơn).")]
    [Range(0.5f, 1f)] public float renderScale = 0.8f;

    [Tooltip("Giảm chất lượng đồ họa khi khởi động.")]
    public bool autoAdjustQuality = true;

    [Header("Transparency Optimization")]
    [Tooltip("Tắt các material transparent để tối ưu hiệu năng.")]
    public bool disableTransparentMaterials = true;

    [Tooltip("Tìm và tắt các particle system transparent.")]
    public bool disableTransparentParticles = true;

    void Awake()
    {
        OptimizeForQuest();
    }

    private void OptimizeForQuest()
    {
        // Tắt VSync nếu có bật
        if (disableVSync)
        {
            QualitySettings.vSyncCount = 0;
        }
        
        // Fixed Foveated Rendering - High để tối ưu nhất
        OVRManager.fixedFoveatedRenderingLevel = OVRManager.FixedFoveatedRenderingLevel.High;

        // Giới hạn FPS
        Application.targetFrameRate = targetFPS;

        // Giảm độ phân giải render
#if UNITY_ANDROID && !UNITY_EDITOR
        XRSettings.eyeTextureResolutionScale = renderScale;
        
        // Đảm bảo passthrough được bật và tối ưu
        EnsurePassthroughOptimized();
#endif

        // Điều chỉnh chất lượng đồ họa tự động nếu cần
        if (autoAdjustQuality)
        {
            AdjustQuality();
        }

        // Tối ưu hóa transparency
        if (disableTransparentMaterials)
        {
            OptimizeTransparentMaterials();
        }

        if (disableTransparentParticles)
        {
            OptimizeParticleSystems();
        }

        // Tắt các hiệu ứng hậu kỳ
        DisablePostProcessing();

        Debug.Log("[Performance] Quest optimization completed");
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void EnsurePassthroughOptimized()
    {
        // Đảm bảo OVRManager tồn tại và được cấu hình cho passthrough
        var ovrManager = FindObjectOfType<OVRManager>();
        if (ovrManager != null)
        {
            // Giữ các setting quan trọng cho passthrough
            ovrManager.useRecommendedMSAALevel = false;
            OVRManager.eyeTrackedFoveatedRendering = false;
            
            // Quan trọng: Đảm bảo không vô tình tắt các component passthrough
            var passthrough = FindObjectOfType<OVRPassthroughLayer>();
            if (passthrough != null)
            {
                Debug.Log("[Performance] Passthrough layer found and preserved");
            }
        }
    }
#endif

    private void AdjustQuality()
    {
        // Tối ưu hóa chất lượng cụ thể cho Quest
#if UNITY_ANDROID && !UNITY_EDITOR
        // Force low quality settings for Quest
        QualitySettings.SetQualityLevel(0, true);
        
        // Các setting cụ thể cho mobile/Quest
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.antiAliasing = 0;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.pixelLightCount = 0;
        
        Debug.Log($"[Performance] Quest-optimized quality settings applied");
#else
        // Giữ logic cũ cho editor/PC
        int deviceLevel = SystemInfo.graphicsMemorySize;
        if (deviceLevel < 2000)
        {
            QualitySettings.SetQualityLevel(0);
        }
        else if (deviceLevel < 4000)
        {
            QualitySettings.SetQualityLevel(2);
        }
        else
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1);
        }
        Debug.Log($"[Performance] GraphicsMemory: {deviceLevel} MB | Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
#endif
    }

    private void OptimizeTransparentMaterials()
    {
        // Tìm và tối ưu tất cả renderers có material transparent
        var renderers = FindObjectsOfType<Renderer>();
        int optimizedCount = 0;

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var materials = renderer.materials;
            bool materialsModified = false;

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;

                // Kiểm tra nếu material là transparent
                if (IsMaterialTransparent(material))
                {
                    // Thay thế bằng material opaque đơn giản
                    materials[i] = CreateSimpleOpaqueMaterial();
                    materialsModified = true;
                    optimizedCount++;
                }
            }

            if (materialsModified)
            {
                renderer.materials = materials;
            }
        }

        Debug.Log($"[Performance] Optimized {optimizedCount} transparent materials");
    }

    private void OptimizeParticleSystems()
    {
        var particleSystems = FindObjectsOfType<ParticleSystem>();
        int optimizedCount = 0;

        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                // Tắt particle system nếu nó sử dụng transparent materials
                foreach (var material in renderer.materials)
                {
                    if (IsMaterialTransparent(material))
                    {
                        ps.gameObject.SetActive(false);
                        optimizedCount++;
                        break;
                    }
                }
            }
        }

        Debug.Log($"[Performance] Disabled {optimizedCount} transparent particle systems");
    }

    private bool IsMaterialTransparent(Material material)
    {
        if (material == null) return false;

        // Kiểm tra các render mode transparent phổ biến
        string renderType = material.GetTag("RenderType", false, "");
        if (renderType == "Transparent" || renderType == "Fade" || renderType == "TransparentCutout")
            return true;

        // Kiểm tra blend mode
        if (material.HasProperty("_SrcBlend") && material.HasProperty("_DstBlend"))
        {
            var srcBlend = material.GetInt("_SrcBlend");
            var dstBlend = material.GetInt("_DstBlend");
            
            // Transparent blend modes
            if (srcBlend == (int)UnityEngine.Rendering.BlendMode.SrcAlpha && 
                dstBlend == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
                return true;
        }

        // Kiểm tra surface type trong URP
        if (material.HasProperty("_Surface"))
        {
            float surfaceType = material.GetFloat("_Surface");
            if (surfaceType == 1) // 1 = Transparent trong URP
                return true;
        }

        return false;
    }

    private Material CreateSimpleOpaqueMaterial()
    {
        // Tạo một material opaque đơn giản - tương thích với cả Built-in và URP
        Shader simpleShader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (simpleShader == null)
        {
            // Fallback cho Built-in pipeline
            simpleShader = Shader.Find("Standard");
        }
        
        var simpleMaterial = new Material(simpleShader);
        simpleMaterial.color = Color.white;
        
        // Đảm bảo material là opaque
        if (simpleMaterial.HasProperty("_Surface"))
        {
            simpleMaterial.SetFloat("_Surface", 0); // 0 = Opaque trong URP
        }
        
        if (simpleMaterial.HasProperty("_Mode"))
        {
            simpleMaterial.SetFloat("_Mode", 0); // 0 = Opaque trong Built-in
        }

        return simpleMaterial;
    }

    private void DisablePostProcessing()
    {
        // Tắt Volume components (URP)
        var volumes = FindObjectsOfType<UnityEngine.Rendering.Volume>();
        foreach (var vol in volumes)
        {
            vol.enabled = false;
        }

        // Tắt Post Process Layers (Built-in pipeline) - sử dụng reflection để tránh lỗi compile
        var allObjects = FindObjectsOfType<GameObject>();
        int postProcessCount = 0;

        foreach (var obj in allObjects)
        {
            // Kiểm tra và tắt các component post processing mà không cần import namespace cụ thể
            var components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component != null && component.GetType().ToString().ToLower().Contains("postprocess"))
                {
                    if (component is Behaviour behaviour)
                    {
                        behaviour.enabled = false;
                        postProcessCount++;
                    }
                }
            }
        }

        Debug.Log($"[Performance] Disabled {volumes.Length} volumes and {postProcessCount} post-process components");
    }
}