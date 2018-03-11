using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Windows.Kinect;

public class KinectMesh : MonoBehaviour
{
    struct VertexData
    {
        public Vector3 pos;
        public Vector2 uv;
    }

    KinectSensor kinect;
    MultiSourceFrameReader reader;

    byte[] colorData;
    byte[] bodyIndexData;
    ushort[] depthData;
    CameraSpacePoint[] cameraSpacePoints;
    ColorSpacePoint[] colorSpacePoints;
    Body[] bodyData;
    [SerializeField] Windows.Kinect.Vector4 floorClipPlane;

    int depthDataLength;

    [Header("Compute Shader")]
    public ComputeShader pointCloudCS;
    [SerializeField] Texture2D colorTex;
    [SerializeField] Texture2D bodyIndexTex;
    ComputeBuffer colorSpacePointBuffer;
    ComputeBuffer cameraSpacePointBuffer;
    ComputeBuffer vertexBuffer;

    public Material meshVisalizer;
    public Camera cameraToRender;
    public KinectBody[] kinectBodies;
    public bool useKinectRot = true;

    void Start()
    {
        kinect = KinectSensor.GetDefault();

        if (kinect != null)
        {
            reader = kinect.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.Body | FrameSourceTypes.BodyIndex);

            var colorDesc = kinect.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);

            var colorPixels = (int)colorDesc.LengthInPixels;
            var colorBytePerPixel = (int)colorDesc.BytesPerPixel;
            colorData = new byte[colorPixels * colorBytePerPixel];
            colorTex = new Texture2D(colorDesc.Width, colorDesc.Height, TextureFormat.RGBA32, false);


            var depthDesc = kinect.DepthFrameSource.FrameDescription;
            depthDataLength = (int)depthDesc.LengthInPixels;

            depthData = new ushort[depthDataLength];
            colorSpacePoints = new ColorSpacePoint[depthDataLength];
            cameraSpacePoints = new CameraSpacePoint[depthDataLength];

            colorSpacePointBuffer = new ComputeBuffer(depthDataLength, Marshal.SizeOf(typeof(ColorSpacePoint)));
            cameraSpacePointBuffer = new ComputeBuffer(depthDataLength, Marshal.SizeOf(typeof(CameraSpacePoint)));
            vertexBuffer = new ComputeBuffer(depthDataLength, Marshal.SizeOf(typeof(VertexData)));

            var bodyIndexDesc = kinect.BodyIndexFrameSource.FrameDescription;
            bodyIndexData = new byte[bodyIndexDesc.LengthInPixels * bodyIndexDesc.BytesPerPixel];
            bodyIndexTex = new Texture2D(bodyIndexDesc.Width, bodyIndexDesc.Height, TextureFormat.R8, false);
            bodyData = new Body[kinect.BodyFrameSource.BodyCount];
            if (kinectBodies == null || kinectBodies.Length != kinect.BodyFrameSource.BodyCount)
            {
                kinectBodies = bodyData.Select((b, idx) =>
                {
                    var kinectBody = new GameObject(string.Format("body.{0}", idx.ToString("00"))).AddComponent<KinectBody>();
                    kinectBody.transform.SetParent(transform);
                    return kinectBody;
                }).ToArray();
            }

            if (!kinect.IsOpen)
                kinect.Open();

            pointCloudCS.SetInt("_CWidth", colorDesc.Width);
            pointCloudCS.SetInt("_CHeight", colorDesc.Height);
            pointCloudCS.SetInt("_DWidth", depthDesc.Width);
            pointCloudCS.SetInt("_DHeight", depthDesc.Height);
            pointCloudCS.SetVector("_ResetRot", new UnityEngine.Vector4(0, 0, 0, 1));
        }
    }

    private void OnApplicationQuit()
    {
        new[] { colorSpacePointBuffer, cameraSpacePointBuffer, vertexBuffer }
        .ToList().ForEach(b => b.Release());
        reader.Dispose();
        if (kinect != null)
            if (kinect.IsOpen)
                kinect.Close();
    }

    void Update()
    {
        if (reader != null)
        {
            var frame = reader.AcquireLatestFrame();
            if (frame != null)
            {
                var colorFrame = frame.ColorFrameReference.AcquireFrame();
                var depthFrame = frame.DepthFrameReference.AcquireFrame();
                var bodyFrame = frame.BodyFrameReference.AcquireFrame();
                var bodyIndexFrame = frame.BodyIndexFrameReference.AcquireFrame();

                if (colorFrame != null)
                {
                    colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
                    colorFrame.Dispose();

                    colorTex.LoadRawTextureData(colorData);
                    colorTex.Apply();
                }
                if (depthFrame != null)
                {
                    depthFrame.CopyFrameDataToArray(depthData);
                    depthFrame.Dispose();

                    kinect.CoordinateMapper.MapDepthFrameToColorSpace(depthData, colorSpacePoints);
                    kinect.CoordinateMapper.MapDepthFrameToCameraSpace(depthData, cameraSpacePoints);

                    colorSpacePointBuffer.SetData(colorSpacePoints);
                    cameraSpacePointBuffer.SetData(cameraSpacePoints);
                }
                if (bodyFrame != null)
                {
                    var temp = floorClipPlane;
                    floorClipPlane = bodyFrame.FloorClipPlane;
                    if (floorClipPlane.W == 0)
                        floorClipPlane.W = temp.W;
                    var kinectRot = Quaternion.FromToRotation(new Vector3(floorClipPlane.X, floorClipPlane.Y, floorClipPlane.Z), Vector3.up);
                    var kinectHeight = floorClipPlane.W;
                    if (!useKinectRot)
                    {
                        kinectRot = Quaternion.identity;
                        kinectHeight = 0f;
                    }
                    pointCloudCS.SetVector("_ResetRot", new UnityEngine.Vector4(kinectRot.x, kinectRot.y, kinectRot.z, kinectRot.w));
                    pointCloudCS.SetFloat("_KinectHeight", kinectHeight);

                    bodyFrame.GetAndRefreshBodyData(bodyData);
                    for (var i = 0; i < bodyData.Length; i++)
                        kinectBodies[i].SetBodyData(bodyData[i], kinectRot, kinectHeight);

                    bodyFrame.Dispose();

                    if (cameraToRender != null)
                    {
                        cameraToRender.transform.position = Vector3.up * kinectHeight;
                        cameraToRender.transform.rotation = kinectRot;
                    }
                }
                if (bodyIndexFrame != null)
                {
                    bodyIndexFrame.CopyFrameDataToArray(bodyIndexData);
                    bodyIndexFrame.Dispose();

                    bodyIndexTex.LoadRawTextureData(bodyIndexData);
                    bodyIndexTex.Apply();
                }
            }

            var kernel = pointCloudCS.FindKernel("buildVertex");
            pointCloudCS.SetBuffer(kernel, "_ColorSpacePointData", colorSpacePointBuffer);
            pointCloudCS.SetBuffer(kernel, "_CameraSpacePointData", cameraSpacePointBuffer);
            pointCloudCS.SetBuffer(kernel, "_VertexDataBuffer", vertexBuffer);
            pointCloudCS.Dispatch(kernel, depthDataLength / 8, 1, 1);
        }
    }

    private void OnRenderObject()
    {
        if (meshVisalizer == null)
            return;

        meshVisalizer.SetTexture("_ColorTex", colorTex);
        meshVisalizer.SetTexture("_BodyIdxTex", bodyIndexTex);
        meshVisalizer.SetBuffer("_VertexData", vertexBuffer);
        meshVisalizer.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, depthDataLength);
    }
}
