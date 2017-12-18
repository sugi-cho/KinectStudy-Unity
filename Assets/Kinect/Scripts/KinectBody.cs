using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Windows.Kinect;
using Joint = Windows.Kinect.Joint;

public class KinectBody : MonoBehaviour
{

    public bool isTracked;
    public ulong trackingId;
    [Header("hand state")]
    HandState handRightState;
    HandState handLeftState;

    [SerializeField] KinectJoint[] jointArray;
    Dictionary<JointType, KinectJoint> joints;

    public void SetBodyData(Body sourceBody, Quaternion kinectRot, float kinectHeight)
    {
        trackingId = sourceBody.TrackingId;
        isTracked = sourceBody.IsTracked;

        if (!isTracked)
            return;

        if (jointArray == null || jointArray.Length != sourceBody.Joints.Count)
        {
            jointArray = sourceBody.Joints.Select(pair =>
            {
                var joint = new KinectJoint();
                joint.type = pair.Key;
                return joint;
            }).ToArray();
            joints = jointArray.ToDictionary(j => j.type, j => j);
        }
        foreach (var key in sourceBody.Joints.Keys)
        {
            var sourceJoint = sourceBody.Joints[key];
            var sourceOrientation = sourceBody.JointOrientations[key];
            joints[key].SetJointData(sourceJoint, sourceOrientation, kinectRot, kinectHeight);
        }

        handLeftState = sourceBody.HandLeftState;
        handRightState = sourceBody.HandRightState;
    }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDrawGizmos()
    {
        if (jointArray != null)
            foreach (var joint in jointArray)
            {
                Gizmos.DrawCube(joint.position, Vector3.one * 0.1f);
                var dir = joint.rotation * Vector3.forward;
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(joint.position, joint.position + dir * 0.5f);
            }
    }

    [System.Serializable]
    public class KinectJoint
    {
        public JointType type;

        public Vector3 position;
        public Quaternion rotation;

        public void SetJointData(Joint sourceJoint, JointOrientation sourceOrientation, Quaternion kinectRot, float kinectHeight)
        {
            var pos = sourceJoint.Position;
            var ori = sourceOrientation.Orientation;

            position.x = pos.X;
            position.y = pos.Y;
            position.z = pos.Z;
            position = kinectRot * position;
            position.y += kinectHeight;

            rotation.Set(ori.X, ori.Y, ori.Z, ori.W);
            rotation *= kinectRot;
        }
    }
}
