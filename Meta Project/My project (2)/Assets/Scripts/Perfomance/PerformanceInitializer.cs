using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

public class PerformanceInitializer : MonoBehaviour
{
    [Header("Basic Settings")]
    [Tooltip("Target FPS for Meta Quest.")]
    public int targetFPS = 72; // FPS mục tiêu cho Meta Quest

    [Tooltip("Disable VSync so the runtime can manage frame pacing.")]
    public bool disableVSync = true; // Tắt VSync để runtime tự quản lý nhịp khung hình

    [Tooltip("Eye texture resolution scale. Keep >= 1 to avoid downscaling textures.")]
    [Range(0.5f, 1.2f)]
    public float renderScale = 1f; // Tỷ lệ độ phân giải render cho texture mắt (>= 1 để tránh giảm chất lượng)

    [Tooltip("Apply Quest specific quality overrides on startup.")]
    public bool autoAdjustQuality = true; // Tự động áp dụng cấu hình chất lượng dành cho Quest khi khởi động

    [Header("Texture Quality")]
    [Tooltip("Keep textures at full resolution regardless of quality level.")]
    public bool preserveTextureDetail = true; // Giữ chi tiết texture ở độ phân giải tối đa, bỏ giảm cấp theo chất lượng

    [Tooltip("Anisotropic filtering mode used when preserving texture detail.")]
    public AnisotropicFiltering anisotropicMode = AnisotropicFiltering.Enable; // Chế độ lọc anisotropic cho texture

    [Header("Lighting & LOD")]
    [Tooltip("Shadow distance override applied on Quest builds.")]
    public float questShadowDistance = 18f; // Khoảng cách đổ bóng tối đa trên Quest

    [Tooltip("Shadow resolution override applied on Quest builds.")]
    public ShadowResolution questShadowResolution = ShadowResolution.Medium; // Độ phân giải bóng trên Quest

    [Tooltip("LOD bias applied on Quest builds.")]
    public float questLodBias = 1.1f; // Hệ số điều chỉnh LOD trên Quest

    [Tooltip("Keep realtime shadows enabled on Quest.")]
    public bool enableShadows = false; // Bật bóng thời gian thực trên Quest

    [Tooltip("Keep realtime reflection probes enabled on Quest.")]
    public bool enableRealtimeReflectionProbes = false; // Bật probe phản xạ thời gian thực trên Quest

    [Header("Transparency Optimisation")]
    [Tooltip("Convert transparent materials to alpha clip instead of replacing their textures.")]
    public bool disableTransparentMaterials = false; // Chuyển vật liệu trong suốt sang alpha clip để tối ưu

    [Tooltip("Reduce emission on transparent particle systems to save GPU time.")]
    public bool disableTransparentParticles = true; // Giảm emission cho hệ hạt trong suốt để tiết kiệm GPU

    [Header("Adaptive GPU Budget")]
    [Tooltip("Monitor GPU metrics and adjust foveated rendering at runtime.")]
    public bool enableAdaptiveGpuBudget = true; // Bật cơ chế điều chỉnh ngân sách GPU thích ứng theo thời gian thực

    [Tooltip("Preferred GPU utilisation ratio (0-1).")]
    [Range(0.3f, 0.7f)]
    public float targetGpuUtilisation = 0.45f; // Tỷ lệ sử dụng GPU mục tiêu (0–1)

    [Tooltip("Allowed deviation before adjusting foveated rendering.")]
    [Range(0.05f, 0.2f)]
    public float gpuUtilisationTolerance = 0.08f; // Biên độ sai lệch cho phép trước khi điều chỉnh FFR

    [Tooltip("Seconds between GPU utilisation samples.")]
    public float gpuCheckInterval = 3f; // Khoảng thời gian giữa các lần đo sử dụng GPU (giây)

    [Tooltip("Minimum foveated rendering level allowed by the adaptive system.")]
    public OVRManager.FixedFoveatedRenderingLevel minFoveatedLevel = OVRManager.FixedFoveatedRenderingLevel.Low; // Mức FFR tối thiểu cho phép

    [Tooltip("Baseline foveated rendering level when gameplay starts.")]
    public OVRManager.FixedFoveatedRenderingLevel baseFoveatedLevel = OVRManager.FixedFoveatedRenderingLevel.Medium; // Mức FFR cơ bản khi bắt đầu chơi

    [Tooltip("Maximum foveated rendering level allowed by the adaptive system.")]
    public OVRManager.FixedFoveatedRenderingLevel maxFoveatedLevel = OVRManager.FixedFoveatedRenderingLevel.HighTop; // Mức FFR tối đa cho phép

    [Header("Post Processing")]
    [Tooltip("Disable post-processing behaviours at runtime.")]
    public bool disablePostProcessing = true; // Tắt các hiệu ứng hậu kỳ khi chạy

    private Coroutine gpuBudgetCoroutine; // Coroutine theo dõi và điều chỉnh ngân sách GPU
    private OVRManager.FixedFoveatedRenderingLevel[] cachedFfrLevels; // Bộ nhớ đệm danh sách các mức FFR

    private void Awake()
    {
        OptimizeForQuest();
    }

    private void OnDestroy()
    {
        if (gpuBudgetCoroutine != null)
        {
            StopCoroutine(gpuBudgetCoroutine);
            gpuBudgetCoroutine = null;
        }
    }

    private void OptimizeForQuest()
    {
        if (disableVSync)
        {
            QualitySettings.vSyncCount = 0;
        }

        Application.targetFrameRate = targetFPS;

        ConfigureXRRuntime();
        ApplyQualityProfile();

        if (disableTransparentMaterials)
        {
            OptimizeTransparentMaterials();
        }

        if (disableTransparentParticles)
        {
            OptimizeParticleSystems();
        }

        if (disablePostProcessing)
        {
            DisablePostProcessing();
        }

        if (enableAdaptiveGpuBudget && Application.isPlaying)
        {
            gpuBudgetCoroutine = StartCoroutine(MonitorGpuUtilisation());
        }

        Debug.Log("[Performance] Quest optimisation completed.");
    }

    private void ConfigureXRRuntime()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        float clampedScale = Mathf.Clamp(renderScale, 1f, 1.2f);
        if (XRSettings.isDeviceActive)
        {
            XRSettings.eyeTextureResolutionScale = clampedScale;
            XRSettings.useOcclusionMesh = true;
        }

        baseFoveatedLevel = ClampFoveatedLevel(baseFoveatedLevel);
        OVRManager.fixedFoveatedRenderingLevel = baseFoveatedLevel;
        OVRManager.useDynamicFixedFoveatedRendering = enableAdaptiveGpuBudget;
#endif
    }

    private void ApplyQualityProfile()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (autoAdjustQuality)
        {
            ApplyQuestQualityOverrides();
        }
#else
        if (autoAdjustQuality)
        {
            ApplyEditorQualityFallback();
        }
#endif

        if (preserveTextureDetail)
        {
            ApplyTextureSettings();
        }
    }

    private void ApplyQuestQualityOverrides()
    {
        int qualityLevel = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel, true);

        QualitySettings.shadows = enableShadows ? ShadowQuality.All : ShadowQuality.Disable;
        QualitySettings.shadowDistance = questShadowDistance;
        QualitySettings.shadowResolution = questShadowResolution;
        QualitySettings.realtimeReflectionProbes = enableRealtimeReflectionProbes;
        QualitySettings.lodBias = questLodBias;
#if UNITY_2019_1_OR_NEWER
        QualitySettings.skinWeights = SkinWeights.TwoBones;
#endif
        QualitySettings.softParticles = false;
        QualitySettings.maximumLODLevel = 0;
    }

    private void ApplyEditorQualityFallback()
    {
        int deviceLevel = SystemInfo.graphicsMemorySize;
        if (deviceLevel < 2000)
        {
            QualitySettings.SetQualityLevel(0, true);
        }
        else if (deviceLevel < 4000)
        {
            QualitySettings.SetQualityLevel(Mathf.Min(2, QualitySettings.names.Length - 1), true);
        }
        else
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        }

        Debug.Log($"[Performance] GraphicsMemory: {deviceLevel} MB | Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
    }

    private void ApplyTextureSettings()
    {
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = anisotropicMode;
    }

    private void OptimizeTransparentMaterials()
    {
        var renderers = FindObjectsOfType<Renderer>();
        int convertedMaterials = 0;

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var materials = renderer.materials;
            bool modified = false;

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || !IsMaterialTransparent(material))
                {
                    continue;
                }

                if (ConvertMaterialToAlphaClip(material))
                {
                    materials[i] = material;
                    modified = true;
                    convertedMaterials++;
                }
            }

            if (modified)
            {
                renderer.materials = materials;
            }
        }

        if (convertedMaterials > 0)
        {
            Debug.Log($"[Performance] Converted {convertedMaterials} transparent materials to alpha clip.");
        }
    }

    private void OptimizeParticleSystems()
    {
        var particleSystems = FindObjectsOfType<ParticleSystem>();
        int adjustedSystems = 0;

        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) continue;

            bool usesTransparentMaterial = false;
            foreach (var material in renderer.sharedMaterials)
            {
                if (IsMaterialTransparent(material))
                {
                    usesTransparentMaterial = true;
                    break;
                }
            }

            if (!usesTransparentMaterial) continue;

            var emission = ps.emission;
            if (emission.enabled)
            {
                var rate = emission.rateOverTime;
                switch (rate.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate.constant * 0.6f);
                        adjustedSystems++;
                        break;
                    case ParticleSystemCurveMode.TwoConstants:
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate.constantMin * 0.6f, rate.constantMax * 0.6f);
                        adjustedSystems++;
                        break;
                }
            }

            var main = ps.main;
            if (main.maxParticles > 0)
            {
                main.maxParticles = Mathf.CeilToInt(main.maxParticles * 0.7f);
            }
        }

        if (adjustedSystems > 0)
        {
            Debug.Log($"[Performance] Reduced emission on {adjustedSystems} transparent particle systems.");
        }
    }

    private bool IsMaterialTransparent(Material material)
    {
        if (material == null) return false;

        string renderType = material.GetTag("RenderType", false, string.Empty);
        if (renderType == "Transparent" || renderType == "Fade" || renderType == "TransparentCutout")
        {
            return true;
        }

        if (material.HasProperty("_SrcBlend") && material.HasProperty("_DstBlend"))
        {
            int srcBlend = material.GetInt("_SrcBlend");
            int dstBlend = material.GetInt("_DstBlend");

            if (srcBlend == (int)BlendMode.SrcAlpha && dstBlend == (int)BlendMode.OneMinusSrcAlpha)
            {
                return true;
            }
        }

        if (material.HasProperty("_Surface"))
        {
            float surfaceType = material.GetFloat("_Surface");
            if (Mathf.Approximately(surfaceType, 1f))
            {
                return true;
            }
        }

        return false;
    }

    private bool ConvertMaterialToAlphaClip(Material material)
    {
        bool changed = false;

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 1f);
            changed = true;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            changed = true;
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 1f);
            changed = true;
        }

        if (material.HasProperty("_Cutoff"))
        {
            float cutoff = material.GetFloat("_Cutoff");
            if (cutoff <= 0f)
            {
                material.SetFloat("_Cutoff", 0.5f);
            }
            changed = true;
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            changed = true;
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            changed = true;
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 1);
            changed = true;
        }

        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.enableInstancing = true;

        return changed;
    }

    private IEnumerator MonitorGpuUtilisation()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var display = OVRManager.display;
        if (display == null)
        {
            yield break;
        }

        float frameBudget = 1f / Mathf.Max(1, targetFPS);
        float lowerBound = Mathf.Clamp01(targetGpuUtilisation - gpuUtilisationTolerance);
        float upperBound = Mathf.Clamp01(targetGpuUtilisation + gpuUtilisationTolerance);

        while (true)
        {
            yield return new WaitForSeconds(gpuCheckInterval);

            float gpuTime = GetPluginGpuTime();
            if (gpuTime <= 0f)
            {
                gpuTime = GetDisplayGpuTime(display);
            }

            if (gpuTime <= 0f)
            {
                continue;
            }

            float utilisation = Mathf.Clamp01(gpuTime / frameBudget);

            if (utilisation > upperBound)
            {
                IncreaseFoveatedLevel();
            }
            else if (utilisation < lowerBound)
            {
                DecreaseFoveatedLevel();
            }
    }
#else
        yield break;
#endif
    }

    private float GetPluginGpuTime()
    {
        var type = typeof(OVRPlugin);

        try
        {
            var property = type.GetProperty("appGPUTime", BindingFlags.Public | BindingFlags.Static);
            if (property != null)
            {
                return Convert.ToSingle(property.GetValue(null, null));
            }

            property = type.GetProperty("AppGPUTime", BindingFlags.Public | BindingFlags.Static);
            if (property != null)
            {
                return Convert.ToSingle(property.GetValue(null, null));
            }

            var method = type.GetMethod("GetAppGPUTime", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                return Convert.ToSingle(method.Invoke(null, null));
            }

            method = type.GetMethod("GetAppGpuTime", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                return Convert.ToSingle(method.Invoke(null, null));
            }
        }
        catch
        {
            // ignored - fall back to display metrics
        }

        return 0f;
    }

    private float GetDisplayGpuTime(OVRDisplay display)
    {
        if (display == null)
        {
            return 0f;
        }

        var type = display.GetType();
        var property = type.GetProperty("appGPUTime");
        if (property != null)
        {
            return Convert.ToSingle(property.GetValue(display, null));
        }

        property = type.GetProperty("AppGPUTime");
        if (property != null)
        {
            return Convert.ToSingle(property.GetValue(display, null));
        }

        return 0f;
    }

    private void IncreaseFoveatedLevel()
    {
        int currentIndex = Mathf.Max(0, GetFfrIndex(OVRManager.fixedFoveatedRenderingLevel));
        int maxIndex = Mathf.Max(0, GetFfrIndex(maxFoveatedLevel));
        int newIndex = Mathf.Min(currentIndex + 1, maxIndex);
        if (newIndex > currentIndex)
        {
            OVRManager.fixedFoveatedRenderingLevel = GetFfrLevels()[newIndex];
            Debug.Log($"[Performance] Raised foveated level to {OVRManager.fixedFoveatedRenderingLevel}.");
        }
    }

    private void DecreaseFoveatedLevel()
    {
        int currentIndex = Mathf.Max(0, GetFfrIndex(OVRManager.fixedFoveatedRenderingLevel));
        int minIndex = Mathf.Max(0, GetFfrIndex(minFoveatedLevel));
        int newIndex = Mathf.Max(currentIndex - 1, minIndex);

        if (newIndex < currentIndex)
        {
            OVRManager.fixedFoveatedRenderingLevel = GetFfrLevels()[newIndex];
            Debug.Log($"[Performance] Lowered foveated level to {OVRManager.fixedFoveatedRenderingLevel}.");
        }
    }

    private OVRManager.FixedFoveatedRenderingLevel ClampFoveatedLevel(OVRManager.FixedFoveatedRenderingLevel level)
    {
        int index = Mathf.Max(0, GetFfrIndex(level));
        int minIndex = Mathf.Max(0, GetFfrIndex(minFoveatedLevel));
        int maxIndex = Mathf.Max(minIndex, GetFfrIndex(maxFoveatedLevel));
        index = Mathf.Clamp(index, minIndex, maxIndex);
        return GetFfrLevels()[index];
    }

    private OVRManager.FixedFoveatedRenderingLevel[] GetFfrLevels()
    {
        return cachedFfrLevels ?? (cachedFfrLevels = (OVRManager.FixedFoveatedRenderingLevel[])System.Enum.GetValues(typeof(OVRManager.FixedFoveatedRenderingLevel)));
    }

    private int GetFfrIndex(OVRManager.FixedFoveatedRenderingLevel level)
    {
        return System.Array.IndexOf(GetFfrLevels(), level);
    }

    private void DisablePostProcessing()
    {
        var allObjects = FindObjectsOfType<GameObject>();
        int disabledCount = 0;

        foreach (var obj in allObjects)
        {
            if (obj == null) continue;

            var components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                string typeName = component.GetType().ToString().ToLowerInvariant();
                if (typeName.Contains("postprocess"))
                {
                    if (component is Behaviour behaviour && behaviour.enabled)
                    {
                        behaviour.enabled = false;
                        disabledCount++;
                    }
                }
            }
        }

        if (disabledCount > 0)
        {
            Debug.Log($"[Performance] Disabled {disabledCount} post-processing behaviours.");
        }
    }
}
