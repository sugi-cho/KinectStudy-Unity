using UnityEngine;
using Windows.Kinect;

public class KinectColorTexture : MonoBehaviour
{
    [Header("compute shader")]
    public ComputeShader colorTexGen;
    ComputeBuffer colorBuffer;
    [SerializeField] RenderTexture colorTexture;

    Color[] colors;

    KinectSensor kinect;
    ColorFrameReader reader;
    byte[] colorData;

    int width;
    int height;

    public RenderTexture GetColorTexture()
    {
        return colorTexture;
    }

    // Use this for initialization
    void Start()
    {

        kinect = KinectSensor.GetDefault();
        if (kinect != null)
        {
            reader = kinect.ColorFrameSource.OpenReader();

            var desc = kinect.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
            width = desc.Width;
            height = desc.Height;

            colorTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            colorTexture.enableRandomWrite = true;
            colorTexture.Create();

            colorBuffer = new ComputeBuffer((int)desc.LengthInPixels, (int)desc.BytesPerPixel);
            colorData = new byte[desc.LengthInPixels * desc.BytesPerPixel];

            var r = GetComponent<Renderer>();
            if (r != null)
                r.material.mainTexture = colorTexture;

            if (!kinect.IsOpen)
                kinect.Open();

            colorTexGen.SetInt("_CWidth", width);
            colorTexGen.SetInt("_CHeight", height);
        }
    }

    private void OnApplicationQuit()
    {
        colorBuffer.Release();
        colorBuffer = null;

        if (reader != null)
            reader.Dispose();
        if (kinect != null)
            if (kinect.IsOpen)
                kinect.Close();

        reader = null;
        kinect = null;
    }

    private void Update()
    {
        if (reader != null)
        {
            var frame = reader.AcquireLatestFrame();

            if (frame != null)
            {
                frame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
                colorBuffer.SetData(colorData);

                var kernel = colorTexGen.FindKernel("buildColorTex");
                colorTexGen.SetBuffer(kernel, "_ColorData", colorBuffer);
                colorTexGen.SetTexture(kernel, "_ColorTex", colorTexture);
                colorTexGen.Dispatch(kernel, width / 8, height / 8, 1);

                frame.Dispose();
                frame = null;
            }
        }
        
    }
}