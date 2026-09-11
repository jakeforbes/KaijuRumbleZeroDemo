using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>Refracts the already-rendered sprite scene only while a Stomp wave is active.</summary>
public sealed class StompDistortionFeature : ScriptableRendererFeature
{
    Material material;
    DistortionPass pass;
    readonly Vector4[] waves = new Vector4[8];
    readonly float[] ages = new float[8];

    public override void Create()
    {
        pass = new DistortionPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ShockwaveFx.Distortions.Count == 0 || renderingData.cameraData.cameraType != CameraType.Game) return;
        if (material == null)
        {
            var shader = Resources.Load<Shader>("StompDistortion");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
        }
        var camera = renderingData.cameraData.camera;
        int count = 0;
        foreach (var wave in ShockwaveFx.Distortions)
        {
            if (wave == null || !wave.isActiveAndEnabled || wave.NormalizedAge >= 1) continue;
            Vector3 at = wave.transform.position;
            Vector3 centre = camera.WorldToViewportPoint(at);
            if (centre.z <= 0) continue;
            Vector3 scale = wave.transform.localScale;
            float rx = Mathf.Abs(camera.WorldToViewportPoint(at + camera.transform.right * scale.x).x - centre.x);
            float ry = Mathf.Abs(camera.WorldToViewportPoint(at + camera.transform.up * scale.y).y - centre.y);
            waves[count] = new Vector4(centre.x, centre.y, Mathf.Max(.0001f,rx), Mathf.Max(.0001f,ry));
            ages[count] = wave.NormalizedAge;
            if (++count == waves.Length) break;
        }
        if (count == 0) return;
        material.SetInt("_WaveCount",count);
        material.SetVectorArray("_Waves",waves);
        material.SetFloatArray("_WaveAges",ages);
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    sealed class DistortionPass : ScriptableRenderPass
    {
        Material material;
        public void Setup(Material value) { material = value; requiresIntermediateTexture = true; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var desc = graph.GetTextureDesc(source);
            desc.name = "Stomp refracted scene"; desc.clearBuffer = false;
            var destination = graph.CreateTexture(desc);
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source,destination,material,0),
                              passName:"Stomp displacement");
            resources.cameraColor = destination;
        }
    }
}
