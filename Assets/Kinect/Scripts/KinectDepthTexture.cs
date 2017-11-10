using UnityEngine;
using System.Runtime.InteropServices;
using Windows.Kinect;

public class KinectDepthTexture : MonoBehaviour
{

    public ComputeShader depthTexGen;
    [SerializeField] RenderTexture depthTexture;
    ComputeBuffer depthBuffer;
    ushort[] depthData;
    int[] depthDataInt;

    KinectSensor kinect;
    DepthFrameReader reader;

    int width;
    int height;

    private void Start()
    {
        kinect = KinectSensor.GetDefault();
        if (kinect != null)
        {
            reader = kinect.DepthFrameSource.OpenReader();
            var desc = kinect.DepthFrameSource.FrameDescription;
            width = desc.Width;
            height = desc.Height;

            depthTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
            depthTexture.enableRandomWrite = true;
            depthTexture.Create();
            var r = GetComponent<Renderer>();
            if (r != null)
                r.material.mainTexture = depthTexture;

            depthBuffer = new ComputeBuffer((int)desc.LengthInPixels, Marshal.SizeOf(typeof(int)));
            depthData = new ushort[desc.LengthInPixels];
            depthDataInt = new int[desc.LengthInPixels];

            depthTexGen.SetInt("_DWidth", width);
            depthTexGen.SetInt("_DHeight", height);
        }
    }

    private void OnDestroy()
    {
        depthBuffer.Release();
        depthBuffer = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (reader != null)
        {
            var frame = reader.AcquireLatestFrame();
            if (frame != null)
            {
                frame.CopyFrameDataToArray(depthData);

                for (var i = 0; i < depthData.Length; i++)
                    depthDataInt[i] = depthData[i];
                depthBuffer.SetData(depthDataInt);

                var kernel = depthTexGen.FindKernel("buildDepthTex");
                depthTexGen.SetBuffer(kernel, "_DepthData", depthBuffer);
                depthTexGen.SetTexture(kernel, "_DepthTex", depthTexture);
                depthTexGen.Dispatch(kernel, width / 8, height / 8, 1);

                frame.Dispose();
                frame = null;
            }
        }
    }
}
