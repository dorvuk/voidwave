using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UnderwaterWobbleEffect : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public bool applyInSceneView = false;
        public bool applyInPreview = false;
    }

    public Settings settings = new Settings();

    Material material;
    UnderwaterPass pass;

    public override void Create()
    {
        var shader = settings.shader ? settings.shader : Shader.Find("Hidden/UnderwaterWobble");
        if (shader != null)
            material = CoreUtils.CreateEngineMaterial(shader);

        var passEvent = settings.renderPassEvent;
        if (passEvent < RenderPassEvent.AfterRenderingTransparents)
            passEvent = RenderPassEvent.AfterRenderingTransparents;

        pass = new UnderwaterPass(material)
        {
            renderPassEvent = passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || pass == null)
            return;

        if (!settings.applyInSceneView && renderingData.cameraData.isSceneViewCamera)
            return;

        if (!settings.applyInPreview && renderingData.cameraData.isPreviewCamera)
            return;

        renderer.EnqueuePass(pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (material == null || pass == null)
            return;

        if (!settings.applyInSceneView && renderingData.cameraData.isSceneViewCamera)
            return;

        if (!settings.applyInPreview && renderingData.cameraData.isPreviewCamera)
            return;

        pass.SetSource(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
    }

    class UnderwaterPass : ScriptableRenderPass
    {
        static readonly int UnderwaterBlendId = Shader.PropertyToID("_UnderwaterBlend");

        readonly Material material;
        RTHandle source;
        RTHandle tempColor;

        public UnderwaterPass(Material material)
        {
            this.material = material;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void SetSource(RTHandle source)
        {
            this.source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(
                ref tempColor,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_UnderwaterTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null)
                return;

            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;

            if (Shader.GetGlobalFloat(UnderwaterBlendId) <= 0.001f)
                return;

            var cmd = CommandBufferPool.Get("UnderwaterPostFx");
            using (new ProfilingScope(cmd, new ProfilingSampler("UnderwaterPostFx")))
            {
                Blitter.BlitCameraTexture(cmd, source, tempColor, material, 0);
                Blitter.BlitCameraTexture(cmd, tempColor, source);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempColor?.Release();
        }
    }
}
