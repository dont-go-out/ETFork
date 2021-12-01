using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Condition = System.Diagnostics.ConditionalAttribute;

[CreateAssetMenu(menuName = "SRPLearn/XRendererPipelineAsset")]
public class XRendererPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        return new XRenderPipeline();
    }
}


public class XRenderPipeline : RenderPipeline
{

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        //遍历摄像机，进行渲染
        foreach(var camera in cameras){
            RenderPerCamera(context,camera);
        }
        //提交渲染命令
        context.Submit();
    }

    private CommandBuffer cameraBuff = new CommandBuffer(){name = "CommandBuffer -----"};
    private CullingResults cullingResults;
    private ShaderTagId _shaderTagForwardBase = new ShaderTagId("ForwardBase");
    private ShaderTagId _shaderTagSrp = new ShaderTagId("SRPDefaultUnlit");
    private const string renderLabel = "------Render Camera";
    private void RenderPerCamera(ScriptableRenderContext context,Camera camera){


        CameraClearFlags clearFlag = camera.clearFlags;
        cameraBuff.ClearRenderTarget(
            (clearFlag & CameraClearFlags.Depth) != 0,
            (clearFlag & CameraClearFlags.Color) != 0,
            camera.backgroundColor);
        cameraBuff.BeginSample(renderLabel);
        context.ExecuteCommandBuffer(cameraBuff);
        cameraBuff.Clear();

        //将camera相关参数，设置到渲染管线中
        context.SetupCameraProperties(camera);
        //对场景进行裁剪
        camera.TryGetCullingParameters( out var cullingParams);

        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
        
        this.cullingResults = context.Cull(ref cullingParams);
        
        //相关参数，用来计算物体渲染时的排序
        var sortingSetting = new SortingSettings(camera);
        var drawSetting = new DrawingSettings(this._shaderTagSrp,sortingSetting);
        
        //相关参数，用来过滤需要渲染的物体
        //绘制不透明物体
        var filterSetting = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults,ref drawSetting,ref filterSetting);
        context.DrawSkybox(camera);
        
        //绘制透明物体
        filterSetting.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults,ref drawSetting,ref filterSetting);
        
        DarwDefaultPiple(context,camera);
        
        cameraBuff.EndSample(renderLabel);
        context.ExecuteCommandBuffer(cameraBuff);
        cameraBuff.Clear();
        
    }

    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    
    static Material errorMaterial;
    private static DrawingSettings legacyDrawingSettings;
    private static FilteringSettings AllFilterSetting = new FilteringSettings(RenderQueueRange.all);
    [Condition("UNITY_EDITOR")]
    private void DarwDefaultPiple(ScriptableRenderContext context,Camera camera)
    {
        if (errorMaterial == null) {
            errorMaterial =
                    new Material(Shader.Find("Hidden/InternalErrorShader")){hideFlags = HideFlags.HideAndDontSave};
            
            legacyDrawingSettings = new DrawingSettings(legacyShaderTagIds[1],new SortingSettings(camera))
            {
                overrideMaterial = errorMaterial
            };
            /*var i = 0;
            foreach (ShaderTagId legacyShaderTagId in legacyShaderTagIds)
            {
                legacyDrawingSettings.SetShaderPassName(i,legacyShaderTagId);
            }*/
            //legacyDrawingSettings.overrideMaterial = errorMaterial;
        }
        
        context.DrawRenderers(cullingResults,ref legacyDrawingSettings,ref AllFilterSetting);
    }
}
