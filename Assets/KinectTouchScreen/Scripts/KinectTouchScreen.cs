using UnityEngine;
using UnityEngine.Events;

using Windows.Kinect;
using sugi.cc;

public class KinectTouchScreen : SingletonMonoBehaviour<KinectTouchScreen>
{
    [System.Serializable]
    public class BodyTouchEvent : UnityEvent<BodyTouchData> { }

    public BodyTouchEvent onBody;
    public Setting setting;
    KinectSensor kinect;
    MultiSourceFrameReader reader;

    public BodyTouchData[] bodies;

    byte[] colorData;
    ushort[] depthData;
    Body[] bodyData;

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;

    [SerializeField] Transform debugTrs;

    void Start()
    {
        kinect = KinectSensor.GetDefault();
        if (kinect != null)
        {
            reader = kinect.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.Body);

            var colorDesc = kinect.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);

            var colorPixels = (int)colorDesc.LengthInPixels;
            var colorBytePerPixel = (int)colorDesc.BytesPerPixel;
            colorData = new byte[colorPixels * colorBytePerPixel];

            var depthDesc = kinect.DepthFrameSource.FrameDescription;
            var depthDataLength = (int)depthDesc.LengthInPixels;

            depthData = new ushort[depthDataLength];
            bodyData = new Body[kinect.BodyFrameSource.BodyCount];
            bodies = new BodyTouchData[kinect.BodyFrameSource.BodyCount];
        }

        mesh = Instantiate(sugi.cc.Helper.GetPrimitiveMesh(PrimitiveType.Plane));
        mesh.MarkDynamic();
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.AddComponent<MeshCollider>();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;

        SettingManager.AddSettingMenu(setting, "touchScreenSetting.json");
    }
    private void OnApplicationQuit()
    {
        if (kinect.IsOpen)
            kinect.Close();
    }
    void Update()
    {
        if (!kinect.IsOpen)
            kinect.Open();
        if (debugTrs != null)
            Debug.Log(GetScreenPos(debugTrs.position));
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
                }
                if (depthFrame != null)
                {
                    depthFrame.CopyFrameDataToArray(depthData);
                    depthFrame.Dispose();
                }
                if (bodyFrame != null)
                {
                    bodyFrame.GetAndRefreshBodyData(bodyData);
                    bodyFrame.Dispose();

                    for (var i = 0; i < bodies.Length; i++)
                    {
                        var body = bodyData[i];
                        var data = bodies[i];
                        data.isTracked = body.IsTracked;

                        if (data.isTracked)
                        {
                            data.trackingId = body.TrackingId;
                            data.headPos.x = body.Joints[JointType.Head].Position.X;
                            data.headPos.y = body.Joints[JointType.Head].Position.Y;
                            data.headPos.z = body.Joints[JointType.Head].Position.Z;

                            data.handLPos.x = body.Joints[JointType.HandLeft].Position.X;
                            data.handLPos.y = body.Joints[JointType.HandLeft].Position.Y;
                            data.handLPos.z = body.Joints[JointType.HandLeft].Position.Z;

                            data.handRPos.x = body.Joints[JointType.HandRight].Position.X;
                            data.handRPos.y = body.Joints[JointType.HandRight].Position.Y;
                            data.handRPos.z = body.Joints[JointType.HandRight].Position.Z;

                            data.headPos = GetScreenPos(data.headPos);
                            data.handLPos = GetScreenPos(data.handLPos);
                            data.handRPos = GetScreenPos(data.handRPos);
                            onBody.Invoke(data);
                        }
                        bodies[i] = data;
                    }
                }
            }
        }
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawSphere(setting.point00, 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(setting.point01, 0.05f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(setting.point10, 0.05f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(setting.point11, 0.05f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(setting.point00, setting.point01);
        Gizmos.DrawLine(setting.point01, setting.point11);
        Gizmos.DrawLine(setting.point11, setting.point10);
        Gizmos.DrawLine(setting.point10, setting.point00);

        foreach (var body in bodies)
        {
            if (body.isTracked)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(body.headPos, 0.1f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(body.handLPos, 0.05f);
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(body.handRPos, 0.05f);
            }
        }
    }

    public Vector3 GetScreenPos(Vector3 pos)
    {
        var ray = new Ray(pos, setting.normal);
        Debug.DrawRay(ray.origin, ray.direction);
        var hitInfo = new RaycastHit();
        var uv = Vector3.back;//z = -1f
        if (meshCollider.Raycast(ray, out hitInfo, 2f))
        {
            uv = hitInfo.textureCoord;
            uv.x -= 0.5f;
            uv.y -= 0.5f;
            uv *= 2.0f;
            if (setting.mirrorX)
                uv.x *= -1f;
            uv.x += 0.5f;
            uv.y += 0.5f;
            uv.z = hitInfo.distance;
        }
        return uv;
    }
    Vector3 MapDepthPointToCameraPoint(Vector2 depthPoint)
    {
        var point = new DepthSpacePoint() { X = depthPoint.x, Y = depthPoint.y };
        var idx = (int)(depthPoint.x + depthPoint.y * 512);
        var depth = Instance.depthData[idx];
        var cameraPoint = Instance.kinect.CoordinateMapper.MapDepthPointToCameraSpace(point, depth);
        return new Vector3(cameraPoint.X, cameraPoint.Y, cameraPoint.Z);
    }
    void EditMeshVerts()
    {
        var vertices = mesh.vertices;
        for (var i = 0; i < vertices.Length; i++)
        {
            var uv = mesh.uv[i];
            var vert0 = Vector3.Lerp(setting.point00, setting.point10, uv.x);
            var vert1 = Vector3.Lerp(setting.point01, setting.point11, uv.x);

            vert0 = (vert0 - setting.center) * 2f + setting.center;
            vert1 = (vert1 - setting.center) * 2f + setting.center;

            vertices[i] = Vector3.Lerp(vert0, vert1, uv.y);
        }
        mesh.vertices = vertices;
        meshCollider.inflateMesh = true;
        meshRenderer.enabled = false;
    }

    [System.Serializable]
    public struct BodyTouchData
    {
        public bool isTracked;
        public ulong trackingId;
        public Vector3 headPos;
        public Vector3 handLPos;
        public Vector3 handRPos;
    }

    [System.Serializable]
    public class Setting : SettingManager.Setting
    {
        public bool mirrorX;
        public Vector3 point00;
        public Vector3 point01;
        public Vector3 point10;
        public Vector3 point11;

        [Header("depth_frame(512*424)")]
        public Vector2 depthPoint00;
        public Vector2 depthPoint01;
        public Vector2 depthPoint10;
        public Vector2 depthPoint11;

        public bool autoUpdatePoints;
        public Vector3 center { get; private set; }
        public Vector3 normal { get; private set; }

        protected override void OnLoad()
        {
            base.OnLoad();
            SetScreenInfo();
            Instance.EditMeshVerts();
        }

        public override void OnGUIFunc()
        {
            base.OnGUIFunc();
            if (autoUpdatePoints || GUILayout.Button("UpdatePoints"))
                SetPoints();

        }
        void SetPoints()
        {
            point00 = Instance.MapDepthPointToCameraPoint(depthPoint00);
            point01 = Instance.MapDepthPointToCameraPoint(depthPoint01);
            point10 = Instance.MapDepthPointToCameraPoint(depthPoint10);
            point11 = Instance.MapDepthPointToCameraPoint(depthPoint11);
            Instance.EditMeshVerts();

            SetScreenInfo();
            dataEditor.Load();
        }
        void SetScreenInfo()
        {
            center = (point00 + point01 + point10 + point11) / 4f;
            var right = (point10 + point11 - point00 - point01).normalized;
            var up = (point01 + point11 - point00 - point10).normalized;
            normal = Vector3.Cross(right, up);
        }
    }
}


public interface IKinectTouchable
{
    void OnKinectBodyTouch(KinectTouchScreen.BodyTouchData bodyTouch);
}