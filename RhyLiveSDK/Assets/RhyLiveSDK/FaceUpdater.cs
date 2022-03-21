using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using extOSC;

namespace RhythMo.RhyLiveSDK
{

public class FaceUpdater : MonoBehaviour
{
    [SerializeField]
    private int smoothness = 3;
    
    private float[] velocityBlendShape = new float[57];
 
    private Dictionary<string, float> sourceBlendShapes = new Dictionary<string, float>
    {
        {"browDownLeft", 0},
        {"browDownRight", 0},
        {"browInnerUp", 0},
        {"browOuterUpLeft", 0},
        {"browOuterUpRight", 0},
        {"cheekPuff", 0},
        {"cheekSquintLeft", 0},
        {"cheekSquintRight", 0},
        {"eyeBlinkLeft", 0},
        {"eyeBlinkRight", 0},
        {"eyeLookDownLeft", 0},
        {"eyeLookDownRight", 0},
        {"eyeLookInLeft", 0},
        {"eyeLookInRight", 0},
        {"eyeLookOutLeft", 0},
        {"eyeLookOutRight", 0},
        {"eyeLookUpLeft", 0},
        {"eyeLookUpRight", 0},
        {"eyeSquintLeft", 0},
        {"eyeSquintRight", 0},
        {"eyeWideLeft", 0},
        {"eyeWideRight", 0},
        {"jawForward", 0},
        {"jawLeft", 0},
        {"jawOpen", 0},
        {"jawRight", 0},
        {"mouthClose", 0},
        {"mouthDimpleLeft", 0},
        {"mouthDimpleRight", 0},
        {"mouthFrownLeft", 0},
        {"mouthFrownRight", 0},
        {"mouthFunnel", 0},
        {"mouthLeft", 0},
        {"mouthLowerDownLeft", 0},
        {"mouthLowerDownRight", 0},
        {"mouthPressLeft", 0},
        {"mouthPressRight", 0},
        {"mouthPucker", 0},
        {"mouthRight", 0},
        {"mouthRollLower", 0},
        {"mouthRollUpper", 0},
        {"mouthShrugLower", 0},
        {"mouthShrugUpper", 0},
        {"mouthSmileLeft", 0},
        {"mouthSmileRight", 0},
        {"mouthStretchLeft", 0},
        {"mouthStretchRight", 0},
        {"mouthUpperUpLeft", 0},
        {"mouthUpperUpRight", 0},
        {"noseSneerLeft", 0},
        {"noseSneerRight", 0},
        {"tongueOut", 0}
    };

    private List<string> updatingBlendShapeKeys;
    public static int FloatDataCount {get; private set;}
    private float[] floatsbuffer;

    private void Awake() {
        this.updatingBlendShapeKeys = new List<string>(sourceBlendShapes.Keys).ToList();
        FaceUpdater.FloatDataCount = this.updatingBlendShapeKeys.Count;

        this.floatsbuffer = new float[this.updatingBlendShapeKeys.Count];
    }

    public void UpdateFromFloats(byte[] data, int startIdx, int len)
    {
        int count = len / sizeof(float);
        if (count > updatingBlendShapeKeys.Count) {
            count = updatingBlendShapeKeys.Count;
        } // avoiding overflow
        
        Buffer.BlockCopy(data, startIdx, floatsbuffer, 0, count * sizeof(float));

        for (int i = 0; i < count; i++) {
            string key = this.updatingBlendShapeKeys[i];
            this.sourceBlendShapes[key] = floatsbuffer[i];
        }
    }

    public void UpdateFromFloats(List<float> data)
    {
        int count = data.Count;
        if (count > updatingBlendShapeKeys.Count) {
            count = updatingBlendShapeKeys.Count;
        } // avoid overflow

        for (int i = 0; i < count; i++) {
            string key = this.updatingBlendShapeKeys[i];
            this.sourceBlendShapes[key] = data[i];
        }
    }

    public void UpdateFromFloats(List<OSCValue> data)
    {
        int count = data.Count;
        if (count > updatingBlendShapeKeys.Count) {
            count = updatingBlendShapeKeys.Count;
        } // avoid overflow

        for (int i = 0; i < count; i++) {
            string key = this.updatingBlendShapeKeys[i];
            this.sourceBlendShapes[key] = data[i].FloatValue;
        }
    }

    public Dictionary<string, float> GetARKitBlendShapeDictionary() {
        return this.sourceBlendShapes;
    }

    public void SmoothApplyToFaceRenderer(SkinnedMeshRenderer faceRenderer) {
        for (int i = 0; i < faceRenderer.sharedMesh.blendShapeCount; i++) {
            string curBlendShapeName = faceRenderer.sharedMesh.GetBlendShapeName(i);
            if (this.sourceBlendShapes.ContainsKey(curBlendShapeName)) {
                Debug.LogFormat("{0} {1}", curBlendShapeName, this.sourceBlendShapes[curBlendShapeName] * 100);
                faceRenderer.SetBlendShapeWeight(i, this.sourceBlendShapes[curBlendShapeName] * 100);
            }
        }
    }
}

}