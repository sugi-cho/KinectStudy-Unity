using UnityEngine;

using MessagePack;
using sugi.cc;

public class DepthFrameWebsocketData : WebSocketDataBehaviour<DepthFrameWebsocketData.FrameData>
{
    public static System.Action<FrameData> OnFrameDataReceived;
    uint receivedFrameCount;

    [SerializeField] ushort oneData;

    private void Update()
    {
        if (0 < receivedData.Count)
        {
            var data = receivedData.Dequeue();
            if (receivedFrameCount < data.frameCount)
            {
                receivedFrameCount = data.frameCount;
                if (OnFrameDataReceived != null)
                    OnFrameDataReceived.Invoke(data);
            }
        }

        //if (Input.GetKeyDown(KeyCode.Space))
        SendTestData();
    }

    void SendTestData()
    {
        var dataLength = 512 * 424;
        var sendData = new ushort[dataLength];
        for (var i = 0; i < dataLength; i++)
            sendData[i] = (ushort)Random.Range(900, 1000);
        var frameData = new FrameData()
        {
            frameCount = (uint)Time.frameCount,
            depthData = sendData
        };
        SendData(frameData);
    }

    [MessagePackObject(true)]
    public struct FrameData
    {
        public uint frameCount;
        public ushort[] depthData;
        public Vector4 floorClipPlane;
    }

}
