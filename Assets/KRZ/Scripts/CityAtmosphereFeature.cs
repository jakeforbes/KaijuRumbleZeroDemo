using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>Night grading, luminous highlights and world-anchored drifting blue haze.</summary>
public sealed class CityAtmosphereFeature : ScriptableRendererFeature
{
    [Range(0, 1)] public float nightStrength = .92f;
    [Range(0, 2)] public float bloomStrength = .65f;
    [Range(0, 1)] public float fogStrength = .58f;
    Material material;
    AtmospherePass pass;
    public override void Create() => pass = new AtmospherePass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        var camera = data.cameraData.camera;
        if (data.cameraData.cameraType != CameraType.Game || !camera.orthographic) return;
        if (material == null)
        {
            var shader = Resources.Load<Shader>("CityAtmosphere");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
        }
        material.SetVector("_Atmosphere", new Vector4(nightStrength, bloomStrength, fogStrength, Time.time));
        material.SetVector("_WorldView", new Vector4(camera.transform.position.x, camera.transform.position.y,
            camera.orthographicSize * camera.aspect, camera.orthographicSize));
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);
    sealed class AtmospherePass : ScriptableRenderPass
    {
        Material material;
        public void Setup(Material value) { material = value; requiresIntermediateTexture = true; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var desc = graph.GetTextureDesc(source);
            desc.name = "Night city atmosphere"; desc.clearBuffer = false;
            var destination = graph.CreateTexture(desc);
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), passName: "City night and blue haze");
            resources.cameraColor = destination;
        }
    }
}
