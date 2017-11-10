using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Windows.Kinect;

public class KinectPointCloud : MonoBehaviour
{
    struct PointCloudData
    {
        public Vector3 pos;
        public Color color;
    }

    KinectSensor kinect;
    MultiSourceFrameReader reader;

    byte[] colorData;
    ushort[] depthData;
    CameraSpacePoint[] cameraSpacePoints;
    ColorSpacePoint[] colorSpacePoints;

    int depthDataLength;

    [Header("Compute Shader")]
    public ComputeShader pointCloudCS;
    ComputeBuffer colorBuffer;
    ComputeBuffer colorSpacePointBuffer;
    ComputeBuffer cameraSpacePointBuffer;
    ComputeBuffer pointCloudBuffer;

    public Material pointVisualizer;

    void Start()
    {
        kinect = KinectSensor.GetDefault();
        if (kinect != null)
        {
            reader = kinect.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);

            var colorDesc = kinect.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            var colorPixels = (int)colorDesc.LengthInPixels;
            var colorBytePerPixel = (int)colorDesc.BytesPerPixel;
            colorData = new byte[colorPixels * colorBytePerPixel];
            colorBuffer = new ComputeBuffer(colorPixels, colorBytePerPixel);


            var depthDesc = kinect.DepthFrameSource.FrameDescription;
            depthDataLength = (int)depthDesc.LengthInPixels;

            depthData = new ushort[depthDataLength];
            colorSpacePoints = new ColorSpacePoint[depthDataLength];
            cameraSpacePoints = new CameraSpacePoint[depthDataLength];

            colorSpacePointBuffer = new ComputeBuffer(depthDataLength, Marshal.SizeOf(typeof(ColorSpacePoint)));
            cameraSpacePointBuffer = new ComputeBuffer(depthDataLength, Marshal.SizeOf(typeof(CameraSpacePoint)));
            pointCloudBuffer = new ComputeBuffer(depthDataLength, Marshal.SizeOf(typeof(PointCloudData)));

            if (!kinect.IsOpen)
                kinect.Open();

            pointCloudCS.SetInt("_CWidth", colorDesc.Width);
            pointCloudCS.SetInt("_CHeight", colorDesc.Height);
            pointCloudCS.SetInt("_DWidth", depthDesc.Width);
            pointCloudCS.SetInt("_DHeight", depthDesc.Height);
        }

        Debug.Log(Marshal.SizeOf(typeof(CameraSpacePoint)));
    }

    private void OnApplicationQuit()
    {
        new[] { colorBuffer, colorSpacePointBuffer, cameraSpacePointBuffer, pointCloudBuffer }
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

                if (colorFrame != null)
                {
                    colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
                    colorFrame.Dispose();

                    colorBuffer.SetData(colorData);
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
            }

            var kernel = pointCloudCS.FindKernel("buildPointCloud");
            pointCloudCS.SetBuffer(kernel, "_ColorData", colorBuffer);
            pointCloudCS.SetBuffer(kernel, "_ColorSpacePointData", colorSpacePointBuffer);
            pointCloudCS.SetBuffer(kernel, "_CameraSpacePointData", cameraSpacePointBuffer);
            pointCloudCS.SetBuffer(kernel, "_PointCloudData", pointCloudBuffer);
            pointCloudCS.Dispatch(kernel, depthDataLength / 8, 1, 1);
        }
    }

    private void OnRenderObject()
    {
        if (pointVisualizer == null)
            return;

        pointVisualizer.SetBuffer("_PointCloudData", pointCloudBuffer);
        pointVisualizer.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, depthDataLength);
    }
}
