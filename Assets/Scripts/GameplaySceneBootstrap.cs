using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 从 LoginScene 切入游戏时，环境光/天空盒/Light Probe 有时未正确刷新，背光面会全黑。
/// 单独打开 SampleScene 时无此问题。
/// </summary>
static class GameplaySceneBootstrap
{
    private const string GameplaySceneName = "SampleScene";

    private static readonly Color AmbientSky = new Color(0.212f, 0.227f, 0.259f);
    private static readonly Color AmbientEquator = new Color(0.114f, 0.125f, 0.133f);
    private static readonly Color AmbientGround = new Color(0.047f, 0.043f, 0.035f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != GameplaySceneName)
            return;

        var runner = new GameObject("GameplaySceneBootstrapRunner");
        runner.AddComponent<Runner>().Begin(scene);
    }

    private sealed class Runner : MonoBehaviour
    {
        public void Begin(Scene scene)
        {
            StartCoroutine(RefreshLighting(scene));
        }

        private IEnumerator RefreshLighting(Scene scene)
        {
            ApplyEnvironment(scene);
            DisableInvalidLightProbes(scene);

            yield return null;

            ApplyEnvironment(scene);
            DisableInvalidLightProbes(scene);
            LightProbes.Tetrahedralize();
            DynamicGI.UpdateEnvironment();

            GameplayCursor.LockForGameplay();

            Destroy(gameObject);
        }

        private static void ApplyEnvironment(Scene scene)
        {
            Light sunLight = FindDirectionalLight(scene);

            if (RenderSettings.skybox == null)
            {
                Material defaultSkybox = Resources.GetBuiltinResource<Material>("Default-Skybox.mat");
                if (defaultSkybox != null)
                    RenderSettings.skybox = defaultSkybox;
            }

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.reflectionIntensity = 1f;

            if (sunLight == null)
                return;

            RenderSettings.sun = sunLight;
            sunLight.enabled = false;
            sunLight.enabled = true;
        }

        private static Light FindDirectionalLight(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var lights = root.GetComponentsInChildren<Light>(true);
                foreach (var light in lights)
                {
                    if (light.type == LightType.Directional)
                        return light;
                }
            }

            return null;
        }

        private static void DisableInvalidLightProbes(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.gameObject.isStatic)
                    {
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    }
                }
            }
        }
    }
}
