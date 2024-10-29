using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace GIProbesRuntime
{
    public class GIRenderFeature : ScriptableRendererFeature
    {
        class GIPass : ScriptableRenderPass
        {
            private ComputeShader myComputeShader;

            private Camera [] giCameras;
            private GIBoundingBox[] boundingBoxes = new GIBoundingBox[128];
            private GILightBaker lightBaker;

            private float lightCheckerResolution;
            private float lightMapResolution;
            private Vector3 lightMapScale;
            private Vector3 lightMapOrigin;

            private int colorSpreadIterations;
            private int blurIterations;

            private int colorSpreadKernelSize;
            private int blurKernelSize;

            RTHandle tempColorTarget;
            RTHandle lightCheckerTarget;

            RenderTexture giVolumeTextureUp;
            RenderTexture giVolumeTextureDown;
            RenderTexture giVolumeTextureLeft;
            RenderTexture giVolumeTextureRight;
            RenderTexture giVolumeTextureFront;
            RenderTexture giVolumeTextureBack;

            bool recalculateLightMap;

            private readonly List<ShaderTagId> shaderTagIdList = new List<ShaderTagId>();
            private FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            Material lcMat;



            public GIPass( ComputeShader computeShader, Material lcm,
            RenderTexture lmu, RenderTexture lmd, RenderTexture lml, RenderTexture lmr, RenderTexture lmf, RenderTexture lmb,
            float lcr, GIBoundingBox[] gibbs, Camera[] gic, GILightBaker lb, float lMResolution, Vector3 lMScale, Vector3 lMOrigin,
            int cSIterations, int bIterations, int cSKernelSize, int bKernelSize)
            {
                myComputeShader = computeShader;

                //tempColorTarget.Init("GI_RENDER_FEATURE");
                tempColorTarget = RTHandles.Alloc("_RGIPRenderFeature", name: "_RGIPRenderFeature");
                lightCheckerTarget = RTHandles.Alloc("_LightCheckerTexture", name: "_LightCheckerTexture");

                lcMat = lcm;

                giVolumeTextureUp = lmu;
                giVolumeTextureDown = lmd;
                giVolumeTextureLeft = lml;
                giVolumeTextureRight = lmr;
                giVolumeTextureFront = lmf;
                giVolumeTextureBack = lmb;

                for (int i = 0; i <  Mathf.Min(gibbs.Length, 128); i++)
                {
                    boundingBoxes[i] = gibbs[i];
                }
                giCameras = gic;
                lightBaker = lb;

                lightCheckerResolution = lcr;
                lightMapResolution = lMResolution;
                lightMapScale = lMScale;
                lightMapOrigin = lMOrigin;
                colorSpreadIterations = cSIterations;
                blurIterations = bIterations;
                colorSpreadKernelSize = cSKernelSize;
                blurKernelSize = bKernelSize;

                shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
                shaderTagIdList.Add(new ShaderTagId("LightweightForward"));
                shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            }


            /// <summary>
            /// Update bounding box info when new bounding boxes are created or destroyed.
            /// </summary>
            public void UpdateBoundingBoxInfo()
            {
                GIBoundingBox[] gibbs =  GameObject.FindObjectsOfType<GIBoundingBox>();
                for (int i = 0; i < boundingBoxes.Length; i++)
                {
                    if(i < Mathf.Min(gibbs.Length, 128))
                        boundingBoxes[i] = gibbs[i];
                    else
                        boundingBoxes[i] = null;
                        
                }
            }

            /// <summary>
            /// Update camera info when new GI Cameras are created or destroyed.
            /// </summary>
            public void UpdateGICameraInfo()
            {
                GICameraProperties[] lcGos = GameObject.FindObjectsOfType<GICameraProperties>();

                //Camera [] lcc = new Camera[lcGos.Length];
                Camera [] gic = new Camera[lcGos.Length];

                bool hasLC = true;

                for (int i = 0; i < lcGos.Length; i++)
                {
                    gic[i] = lcGos[i].gameObject.GetComponent<Camera>();
                }

                if(gic != null && hasLC)
                {
                    giCameras = gic;
                }
                
            }

            /// <summary>
            /// Update Lightbaker if using a different Lightbaker.
            /// </summary>
            public void UpdateLightBaker()
            {
                if(GameObject.FindObjectOfType<GILightBaker>())
                    lightBaker = GameObject.FindObjectOfType<GILightBaker>();
            }

            /// <summary>
            /// Recalculate Light Maps when the Light Map's resolution, scale, or offset has changed.
            /// </summary>
            /// <param name="newLMR">The new Light Map resolution.</param>
            /// <param name="newO">The new Light Map offset.</param>
            /// <param name="newS">The new Light Map scale.</param>
            public void RecalculateLightMap()
            {
                recalculateLightMap = true;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                //Exit if this is not a regular camera
                if (renderingData.cameraData.camera.cameraType != CameraType.Game)
                    return;
                    
                if(renderingData.cameraData.camera.gameObject.GetComponent<GICameraProperties>())
                {
                    RenderTextureDescriptor cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                    cameraTextureDescriptor.depthBufferBits = 0;
                    cameraTextureDescriptor.width = Mathf.RoundToInt(cameraTextureDescriptor.width * lightCheckerResolution);
                    cameraTextureDescriptor.height = Mathf.RoundToInt(cameraTextureDescriptor.height * lightCheckerResolution);
                    cmd.GetTemporaryRT(Shader.PropertyToID(lightCheckerTarget.name), cameraTextureDescriptor, FilterMode.Bilinear);
                    //ConfigureTarget(lightCheckerTarget.nameID);
                    ConfigureTarget(new RenderTargetIdentifier(lightCheckerTarget.nameID, 0, CubemapFace.Unknown,  RenderTargetIdentifier.AllDepthSlices));
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                //Exit if this is not a regular camera
                if (renderingData.cameraData.camera.cameraType != CameraType.Game)
                    return;
                    
                CommandBuffer cmd = CommandBufferPool.Get(name: "GIPass");

                if (!lcMat)
                {
                    Debug.Log("Must add Light Checker Material.");
                    return;
                }

                // Create Direct Lighting Pass
                Camera camera = renderingData.cameraData.camera;
                context.DrawSkybox(camera);
            
                DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData,
                SortingCriteria.CommonOpaque);

                RenderStateBlock renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);
                renderStateBlock.depthState = new DepthState(false, CompareFunction.LessEqual);

                drawSettings.overrideMaterial = lcMat;
                drawSettings.overrideMaterialPassIndex = 0;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStateBlock);
                
                context.ExecuteCommandBuffer(cmd);              
                CommandBufferPool.Release(cmd);

                // Main GI Camera Shader
                cmd = CommandBufferPool.Get(name: "GIPass");

                int useVR = 0;

                for (int i = 0; i < giCameras.Length; i++)
                {
                    if(renderingData.cameraData.camera == giCameras[i])
                    {
                        if(giCameras[i].GetComponent<GICameraProperties>().vrCamera)
                            useVR = 1;

                        cmd.GetTemporaryRT(Shader.PropertyToID(tempColorTarget.name), renderingData.cameraData.cameraTargetDescriptor);
                    }
                }

                cmd.SetGlobalTexture("_GILC", lightCheckerTarget);

                cmd.SetGlobalFloat("lightMapRes", lightMapResolution);
                cmd.SetGlobalVector("lightMapScale", new Vector4(lightMapScale.x, lightMapScale.y, lightMapScale.z, 1));
                cmd.SetGlobalVector("lightMapOffsetVec4", new Vector4(lightMapOrigin.x, lightMapOrigin.y, lightMapOrigin.z, 1));

                List<Matrix4x4> matrices = new List<Matrix4x4>();
                List<Vector4> scales = new List<Vector4>();
                List<float> falloffs = new List<float>();
                List<float> strengths = new List<float>();
                for (int i = 0; i < boundingBoxes.Length; i++)
                {
                    if(boundingBoxes[i] != null)
                    {
                        matrices.Add(Matrix4x4.TRS(boundingBoxes[i].transform.position, boundingBoxes[i].transform.rotation, Vector3.one));
                        scales.Add(new Vector4(boundingBoxes[i].transform.localScale.x, boundingBoxes[i].transform.localScale.y, boundingBoxes[i].transform.localScale.z, 1));
                        falloffs.Add(boundingBoxes[i].boundingBoxFalloff);
                        strengths.Add(boundingBoxes[i].boundingBoxStength);
                    }
                    else
                    {
                        matrices.Add(Matrix4x4.zero);
                        scales.Add(Vector3.zero);
                        falloffs.Add(0);
                        strengths.Add(0);
                    }
                }

                if(boundingBoxes.Length > 0)
                {
                    cmd.SetGlobalMatrixArray(Shader.PropertyToID("_BBTransforms"), matrices);
                    cmd.SetGlobalVectorArray(Shader.PropertyToID("_BBScales"), scales);
                    cmd.SetGlobalFloatArray(Shader.PropertyToID("_BBFalloff"), falloffs);
                    cmd.SetGlobalFloatArray(Shader.PropertyToID("_BBStrengths"), strengths);
                }

                for (int i = 0; i < giCameras.Length; i++)
                {
                    if(renderingData.cameraData.camera == giCameras[i])
                    {
                        if(useVR == 0)
                        {
                            giCameras[i].GetComponent<GICameraProperties>().gIImageEffect.SetInt("useVR", 0);
                            Blit(cmd, renderingData.cameraData.renderer.cameraColorTarget, tempColorTarget, giCameras[i].GetComponent<GICameraProperties>().gIImageEffect, 0);
                        }
                        else
                        {
                            giCameras[i].GetComponent<GICameraProperties>().gIImageEffect.SetInt("useVR", 1);
                            cmd.SetRenderTarget(new RenderTargetIdentifier(renderingData.cameraData.renderer.cameraColorTarget, 0, CubemapFace.Unknown, -1));
                            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, giCameras[i].GetComponent<GICameraProperties>().gIImageEffect);
                            
                        }
                    }
                }
                

                if(recalculateLightMap)
                {
                    ComputeBuffer giProbePositionBuffer = new ComputeBuffer(lightBaker.probePositions.Count, sizeof(float) * 3);
                    giProbePositionBuffer.SetData(lightBaker.probePositions);

                    ComputeBuffer giProbeUpColorBuffer = new ComputeBuffer(lightBaker.probeUpColor.Count, sizeof(float) * 3);
                    giProbeUpColorBuffer.SetData(lightBaker.probeUpColor);

                    ComputeBuffer giProbeDownColorBuffer = new ComputeBuffer(lightBaker.probeDownColor.Count, sizeof(float) * 3);
                    giProbeDownColorBuffer.SetData(lightBaker.probeDownColor);

                    ComputeBuffer giProbeLeftColorBuffer = new ComputeBuffer(lightBaker.probeLeftColor.Count, sizeof(float) * 3);
                    giProbeLeftColorBuffer.SetData(lightBaker.probeLeftColor);

                    ComputeBuffer giProbeRightColorBuffer = new ComputeBuffer(lightBaker.probeRightColor.Count, sizeof(float) * 3);
                    giProbeRightColorBuffer.SetData(lightBaker.probeRightColor);

                    ComputeBuffer giProbeFrontColorBuffer = new ComputeBuffer(lightBaker.probeFrontColor.Count, sizeof(float) * 3);
                    giProbeFrontColorBuffer.SetData(lightBaker.probeFrontColor);

                    ComputeBuffer giProbeBackColorBuffer = new ComputeBuffer(lightBaker.probeBackColor.Count, sizeof(float) * 3);
                    giProbeBackColorBuffer.SetData(lightBaker.probeBackColor);

                    ComputeBuffer giProbeMinDistBuffer = new ComputeBuffer(lightBaker.probeMinDist.Count, sizeof(float));
                    giProbeMinDistBuffer.SetData(lightBaker.probeMinDist);
                    

                    int kid = myComputeShader.FindKernel("CSMain");

                    myComputeShader.SetBuffer(kid, "ProbePositions", giProbePositionBuffer);

                    myComputeShader.SetBuffer(kid, "ProbeColorUp", giProbeUpColorBuffer);
                    myComputeShader.SetBuffer(kid, "ProbeColorDown", giProbeDownColorBuffer);
                    myComputeShader.SetBuffer(kid, "ProbeColorLeft", giProbeLeftColorBuffer);
                    myComputeShader.SetBuffer(kid, "ProbeColorRight", giProbeRightColorBuffer);
                    myComputeShader.SetBuffer(kid, "ProbeColorFront", giProbeFrontColorBuffer);
                    myComputeShader.SetBuffer(kid, "ProbeColorBack", giProbeBackColorBuffer);
                    myComputeShader.SetBuffer(kid, "ProbeMinDist", giProbeMinDistBuffer);

                    myComputeShader.SetFloat("TexSize", lightMapResolution);
                    myComputeShader.SetVector("TexOffset", new Vector4(lightMapOrigin.x, lightMapOrigin.y, lightMapOrigin.z, 1));
                    myComputeShader.SetVector("TexWorldScale", new Vector4(lightMapScale.x, lightMapScale.y, lightMapScale.z, 1));
                    myComputeShader.SetFloat("colorSpreadKernel", colorSpreadKernelSize);
                    myComputeShader.SetFloat("blurKernel", blurKernelSize);

                    myComputeShader.SetTexture(kid, "ResultUp", giVolumeTextureUp);
                    myComputeShader.SetTexture(kid, "ResultDown", giVolumeTextureDown);
                    myComputeShader.SetTexture(kid, "ResultLeft", giVolumeTextureLeft);
                    myComputeShader.SetTexture(kid, "ResultRight", giVolumeTextureRight);
                    myComputeShader.SetTexture(kid, "ResultFront", giVolumeTextureFront);
                    myComputeShader.SetTexture(kid, "ResultBack", giVolumeTextureBack);

                    // Main pass will use the GI Probe info to update 3D Light Maps.
                    myComputeShader.Dispatch(kid, (int)lightMapResolution / 8, (int)lightMapResolution / 8, (int)lightMapResolution / 8);

                    kid = myComputeShader.FindKernel("SpreadColor");
                    myComputeShader.SetBuffer(kid, "ProbePositions", giProbePositionBuffer);
                    myComputeShader.SetTexture(kid, "ResultUp", giVolumeTextureUp);
                    myComputeShader.SetTexture(kid, "ResultDown", giVolumeTextureDown);
                    myComputeShader.SetTexture(kid, "ResultLeft", giVolumeTextureLeft);
                    myComputeShader.SetTexture(kid, "ResultRight", giVolumeTextureRight);
                    myComputeShader.SetTexture(kid, "ResultFront", giVolumeTextureFront);
                    myComputeShader.SetTexture(kid, "ResultBack", giVolumeTextureBack);
                    // Spread Color pass fills in uncolored areas of the 3D Light Maps.
                    for (int i = 0; i < colorSpreadIterations; i++)
                    {
                        myComputeShader.Dispatch(kid, (int)lightMapResolution / 8, (int)lightMapResolution / 8, (int)lightMapResolution / 8);
                    }

                    kid = myComputeShader.FindKernel("Blur");
                    myComputeShader.SetBuffer(kid, "ProbePositions", giProbePositionBuffer);
                    myComputeShader.SetTexture(kid, "ResultUp", giVolumeTextureUp);
                    myComputeShader.SetTexture(kid, "ResultDown", giVolumeTextureDown);
                    myComputeShader.SetTexture(kid, "ResultLeft", giVolumeTextureLeft);
                    myComputeShader.SetTexture(kid, "ResultRight", giVolumeTextureRight);
                    myComputeShader.SetTexture(kid, "ResultFront", giVolumeTextureFront);
                    myComputeShader.SetTexture(kid, "ResultBack", giVolumeTextureBack);
                    // Blur Color pass smoothes the final result in the 3D Light Maps.
                    for (int i = 0; i < blurIterations; i++)
                    {
                        myComputeShader.Dispatch(kid, (int)lightMapResolution / 8, (int)lightMapResolution / 8, (int)lightMapResolution / 8);
                    }

                    cmd.SetGlobalTexture("_GIVolumeUp", giVolumeTextureUp);
                    cmd.SetGlobalTexture("_GIVolumeDown", giVolumeTextureDown);
                    cmd.SetGlobalTexture("_GIVolumeLeft", giVolumeTextureLeft);
                    cmd.SetGlobalTexture("_GIVolumeRight", giVolumeTextureRight);
                    cmd.SetGlobalTexture("_GIVolumeFront", giVolumeTextureFront);
                    cmd.SetGlobalTexture("_GIVolumeBack", giVolumeTextureBack);

                    giProbePositionBuffer.Release();
                    giProbeUpColorBuffer.Release();
                    giProbeDownColorBuffer.Release();
                    giProbeLeftColorBuffer.Release();
                    giProbeRightColorBuffer.Release();
                    giProbeFrontColorBuffer.Release();
                    giProbeBackColorBuffer.Release();
                    giProbeMinDistBuffer.Release();

                    recalculateLightMap = false;
                }

                if(useVR == 0)
                {
                    Blit(cmd, tempColorTarget, renderingData.cameraData.renderer.cameraColorTarget);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);

            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                //cmd.ReleaseTemporaryRT(tempColorTarget.id);
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(tempColorTarget.name));
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(lightCheckerTarget.name));
            }
        }

        private GIPass _gIPass;

        [Header("Resources")]

        [Tooltip("The Compute Shader that bakes the lighting into a 3D Light Maps.")]
        public ComputeShader myComputeShader;

        [Tooltip("The Material used to create the Direct Lighting Pass.")]
        public Material myLightCheckerMat;

        public enum LightMapResolution
        {
            _32,
            _64,
            _128,
            _256,
        }

        [Header("Light Map Settings")]

        [Tooltip("The resolution of the 3D Light Map. By default, each pixel is 1m x 1m x 1m big.")]
        public LightMapResolution lightMapResolution = LightMapResolution._128;
        [Tooltip("The scale of the 3D Light Map. How big is one pixel in the Light Map in meters.")]
        public Vector3 lightMapScale = new Vector3(1,1,1);

        [Tooltip("The center of the 3D Light Map.")]
        public Vector3 lightMapOrigin;

        [Tooltip("The resolution of the Light Checker render texture, multiple of the parent camera resolution.")]
        [Range(0.0f, 1.0f)]
        public float lightCheckerResolution = 1;

        [Tooltip("How many iterations to repeat the spreading color operation in the 3D Light Map.")]
        [Range(0, 10)]
        public int colorSpreadIterations = 3;

        [Tooltip("How many iterations to repeat the blur operation in the 3D Light Map.")]
        [Range(0, 10)]
        public int blurIterations = 1;

        [Tooltip("The kernel size for spreading color in the 3D Light Map [(x*2 + 1) x (x*2 + 1) x (x*2 + 1)]. Affects performance the most.")]
        [Range(1, 10)]
        public int colorSpreadKernelSize = 2;

        [Tooltip("The kernel size for bluring the 3D Light Map [(x*2 + 1) x (x*2 + 1) x (x*2 + 1)]. Affects performance the most.")]
        [Range(1, 10)]
        public int blurKernelSize = 4;

        public override void Create()
        {
            GIBoundingBox[] gibbs =  GameObject.FindObjectsOfType<GIBoundingBox>();
            GICameraProperties[] lcGos = GameObject.FindObjectsOfType<GICameraProperties>();

            Camera [] gic = new Camera[lcGos.Length];
            bool hasLC = true;

            for (int i = 0; i < lcGos.Length; i++)
            {
                gic[i] = lcGos[i].gameObject.GetComponent<Camera>();
            }
            
            GILightBaker lb = null;
            if(GameObject.FindObjectOfType<GILightBaker>())
                lb = GameObject.FindObjectOfType<GILightBaker>();

            if(gic != null && lb != null && gic.Length > 0 && hasLC)
            {
                int lmr = 0;
                if(lightMapResolution == LightMapResolution._32)
                    lmr = 32;
                if(lightMapResolution == LightMapResolution._64)
                    lmr = 64;
                if(lightMapResolution == LightMapResolution._128)
                    lmr = 128;
                if(lightMapResolution == LightMapResolution._256)
                    lmr = 256;

                RenderTexture giVolumeTextureUp = new RenderTexture(lmr, lmr, 0);
                giVolumeTextureUp.enableRandomWrite = true;
                giVolumeTextureUp.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                giVolumeTextureUp.volumeDepth = lmr;
                giVolumeTextureUp.Create();

                RenderTexture giVolumeTextureDown = new RenderTexture(lmr, lmr, 0);
                giVolumeTextureDown.enableRandomWrite = true;
                giVolumeTextureDown.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                giVolumeTextureDown.volumeDepth = lmr;
                giVolumeTextureDown.Create();

                RenderTexture giVolumeTextureLeft = new RenderTexture(lmr, lmr, 0);
                giVolumeTextureLeft.enableRandomWrite = true;
                giVolumeTextureLeft.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                giVolumeTextureLeft.volumeDepth = lmr;
                giVolumeTextureLeft.Create();

                RenderTexture giVolumeTextureRight = new RenderTexture(lmr, lmr, 0);
                giVolumeTextureRight.enableRandomWrite = true;
                giVolumeTextureRight.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                giVolumeTextureRight.volumeDepth = lmr;
                giVolumeTextureRight.Create();

                RenderTexture giVolumeTextureFront = new RenderTexture(lmr, lmr, 0);
                giVolumeTextureFront.enableRandomWrite = true;
                giVolumeTextureFront.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                giVolumeTextureFront.volumeDepth = lmr;
                giVolumeTextureFront.Create();

                RenderTexture giVolumeTextureBack = new RenderTexture(lmr, lmr, 0);
                giVolumeTextureBack.enableRandomWrite = true;
                giVolumeTextureBack.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                giVolumeTextureBack.volumeDepth = lmr;
                giVolumeTextureBack.Create();
            
                _gIPass = new GIPass(myComputeShader, myLightCheckerMat,
                giVolumeTextureUp, giVolumeTextureDown, giVolumeTextureLeft, giVolumeTextureRight,
                giVolumeTextureFront, giVolumeTextureBack, lightCheckerResolution, gibbs, gic, lb, lmr, lightMapScale,
                lightMapOrigin, colorSpreadIterations, blurIterations, colorSpreadKernelSize, blurKernelSize);

                _gIPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if(_gIPass != null)
            {
                _gIPass.ConfigureInput(ScriptableRenderPassInput.Normal);
                _gIPass.ConfigureInput(ScriptableRenderPassInput.Color);
                renderer.EnqueuePass(_gIPass);
            }
        }

        /// <summary>
        /// Recalculate Light Maps when GI Probe info has changed. Called from external scripts.
        /// </summary>
        public void RecalculateLightMap()
        {
            if(_gIPass != null)
                _gIPass.RecalculateLightMap();
        }

        /// <summary>
        /// Update bounding box info when new bounding boxes are created or destroyed. Called from external scripts.
        /// </summary>
        public void UpdateBoundingBoxInfo()
        {
            if(_gIPass != null)
                _gIPass.UpdateBoundingBoxInfo();
        }

        /// <summary>
        /// Update camera info when new GI Cameras are created or destroyed. Called from external scripts.
        /// </summary>
        public void UpdateGICameraInfo()
        {
            if(_gIPass != null)
                _gIPass.UpdateGICameraInfo();
        }

        /// <summary>
        /// Update Lightbaker if using a different Lightbaker. Called from external scripts.
        /// </summary>
        public void UpdateLightBaker()
        {
            if(_gIPass != null)
                _gIPass.UpdateLightBaker();
        }
    }
}
