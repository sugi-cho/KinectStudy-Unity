using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenScript : MonoBehaviour,IKinectTouchable {
    public GameObject touchVis;
    public Camera mainCam;

    public void OnKinectBodyTouch(KinectTouchScreen.BodyTouchData bodyTouch)
    {
        var pos = bodyTouch.handLPos;
        pos = mainCam.ViewportToWorldPoint(pos);
        touchVis.transform.position = pos;
    }
}
