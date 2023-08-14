// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

/// \ingroup Audio
/// Plays the specified SECTR_AudioCue at the nearest point along a spline to the listener. 
///
/// Many phenomena that emit sound (like streams or roads) are well described by splines.
/// This component makes it easy to play sounds that mimic the behavior of these sources.
/// The SplineSource will efficiently compute the nearest point on the spline to the active
/// listener, and position its sound instance at that position. This creates a very convincing
/// illusing of the sound eminating from the entire spline, while using only one actual
/// audio instance.
/// 
/// Liberally adapted from http://wiki.unity3d.com/index.php?title=Spline_Controller
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Spline Source")]
public class SECTR_SplineSource : SECTR_PointSource
{
    #region Private Details
    private class SplineNode
    {
        public Vector3 Point;
        public Quaternion Rot;
        public float T;
        public Vector2 EaseIO;

        public SplineNode(Vector3 p, Quaternion q, float t, Vector2 io)
        {
            Point = p;
            Rot = q;
            T = t;
            EaseIO = io;
        }

        public SplineNode(SplineNode o)
        {
            Point = o.Point;
            Rot = o.Rot;
            T = o.T;
            EaseIO = o.EaseIO;
        }
    }

    private List<SplineNode> nodes = new List<SplineNode>(8);
    #endregion

    #region Public Interface
    [SECTR_ToolTip("Array of scene objects to use as control points for the spline")]
    public List<Transform> SplinePoints = new List<Transform>();
    [SECTR_ToolTip("Determines if the spline is open or closed (i.e. a loop).")]
    public bool Closed = false;
    [SECTR_ToolTip("Determines if the audio source is allowed to leave the spline during interpolation.")]
    public bool AllowShortcuts = true;
    [SECTR_ToolTip("The maximum speed at which to interpolate the sound in space.", 0, -1)]
    public float MaxInterpSpeed = 100f;

    public override void Play()
    {
        base.Play();
        if (instance)
        {
            instance.Position = _GetClosestPointOnSpline(SECTR_AudioSystem.Listener.position);
        }
    }
    #endregion

    #region Unity Interface
    void Awake()
    {
        _SetupSpline();
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update += Update;
#endif
    }

    protected override void OnDisable()
    {
        base.OnDisable();

#if UNITY_EDITOR
        EditorApplication.update -= Update;
#endif
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        _SetupSpline();
        if (nodes.Count > 0)
        {
            Vector3 prevPos = nodes[0].Point;
            int numSamples = 1000;
            for (int c = 0; c < numSamples; c++)
            {
                float currT = c / (float)numSamples;
                Vector3 currPos = _GetHermiteAtT(currT);
                if (c > 0)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(prevPos, currPos);
                }
                prevPos = currPos;
            }
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(instance.Position, 2);
        }
    }
#endif

    void Update()
    {
        if (instance && nodes.Count > 0)
        {
            Vector3 closestPoint = _GetClosestPointOnSpline(SECTR_AudioSystem.Listener.position);
            if (AllowShortcuts)
            {
                Vector3 delta = closestPoint - instance.Position;
                float deltaMag = delta.magnitude;
                if (deltaMag > Mathf.Epsilon)
                {
                    delta /= deltaMag;
                    delta *= Mathf.Min(deltaMag, MaxInterpSpeed * Time.deltaTime);
                    closestPoint = instance.Position + delta;

                    closestPoint = transform.worldToLocalMatrix.MultiplyPoint3x4(closestPoint);
                    instance.LocalPosition = closestPoint;
                }
            }
            else
            {
                instance.Position = closestPoint;
            }
        }
    }
    #endregion

    #region Private Methods
    void _SetupSpline()
    {
        nodes.Clear();

        int numPoints = SplinePoints.Count;
        if (numPoints >= 2)
        {
            float step = (Closed) ? 1f / numPoints : 1f / (numPoints - 1);

            int pointIndex;
            for (pointIndex = 0; pointIndex < numPoints; pointIndex++)
            {
                Transform trans = SplinePoints[pointIndex];
                if (trans)
                {
                    nodes.Add(new SplineNode(trans.position, trans.rotation, step * pointIndex, new Vector2(0, 1)));
                }
            }

            if (Closed && nodes.Count > 0)
            {
                float joiningPointTime = (step * pointIndex);

                nodes.Add(new SplineNode(nodes[0]));
                nodes[nodes.Count - 1].T = joiningPointTime;

                Vector3 vInitDir = (nodes[1].Point - nodes[0].Point).normalized;

                Vector3 vEndDir = (nodes[nodes.Count - 2].Point - nodes[nodes.Count - 1].Point).normalized;
                float firstLength = (nodes[1].Point - nodes[0].Point).magnitude;
                float lastLength = (nodes[nodes.Count - 2].Point - nodes[nodes.Count - 1].Point).magnitude;

                SplineNode firstNode = new SplineNode(nodes[0]);
                firstNode.Point = nodes[0].Point + vEndDir * firstLength;

                SplineNode lastNode = new SplineNode(nodes[nodes.Count - 1]);
                lastNode.Point = nodes[0].Point + vInitDir * lastLength;

                nodes.Insert(0, firstNode);
                nodes.Add(lastNode);
            }

            int numNodes = nodes.Count;
            for (int c = 1; c < numNodes; c++)
            {
                SplineNode node = nodes[c];
                SplineNode prevNode = nodes[c - 1];

                // Always interpolate using the shortest path -> Selective negation
                if (Quaternion.Dot(node.Rot, prevNode.Rot) < 0)
                {
                    node.Rot.x = -node.Rot.x;
                    node.Rot.y = -node.Rot.y;
                    node.Rot.z = -node.Rot.z;
                    node.Rot.w = -node.Rot.w;
                }
            }

            if (numNodes > 0 && !Closed)
            {
                nodes.Insert(0, nodes[0]);
                nodes.Add(nodes[nodes.Count - 1]);
            }
        }
    }

    private Vector3 _GetClosestPointOnSpline(Vector3 point)
    {
        float bestDistanceSqr = float.MaxValue;
        float bestT = 0;

        int numNodes = nodes.Count;
        int startIndex = 1;
        int lastIndex = numNodes;

        //Skip the first and last "fake" node in a closed spline setup since this can lead to wrong T-value calculations on the roundtrip segment.
        if (Closed)
        {
            startIndex = 2;
            lastIndex = numNodes - 1;
        }
        for (int nodeIndex = startIndex; nodeIndex < lastIndex; ++nodeIndex)
        {
            Vector3 start = nodes[nodeIndex - 1].Point;
            Vector3 end = nodes[nodeIndex].Point;

            Vector3 line = (end - start);
            float len = line.magnitude;
            line /= len;

            Vector3 v = point - start;
            float d = Vector3.Dot(v, line);
            d = Mathf.Clamp(d, 0f, len);
            Vector3 projectedPoint = start + line * d;
            float distanceSqr = Vector3.SqrMagnitude(projectedPoint - point);

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestT = Mathf.Lerp(nodes[nodeIndex - 1].T, nodes[nodeIndex].T, d / len);
            }
        }

        return _GetHermiteAtT(bestT);
    }

    private Vector3 _GetHermiteAtT(float timeParam)
    {
        int numNodes = nodes.Count;
        if (timeParam >= nodes[numNodes - 2].T)
        {
            return nodes[numNodes - 2].Point;
        }

        int c;
        for (c = 1; c < numNodes - 2; c++)
        {
            if (nodes[c].T > timeParam)
            {
                break;
            }
        }

        int idx = c - 1;
        float t = (timeParam - nodes[idx].T) / (nodes[idx + 1].T - nodes[idx].T);
        t = _Ease(t, nodes[idx].EaseIO.x, nodes[idx].EaseIO.y);

        float t2 = t * t;
        float t3 = t2 * t;

        Vector3 P0 = nodes[idx - 1].Point;
        Vector3 P1 = nodes[idx].Point;
        Vector3 P2 = nodes[idx + 1].Point;
        Vector3 P3 = nodes[idx + 2].Point;

        float tension = 0.5f;   // 0.5 equivale a catmull-rom

        Vector3 T1 = tension * (P2 - P0);
        Vector3 T2 = tension * (P3 - P1);

        float Blend1 = 2 * t3 - 3 * t2 + 1;
        float Blend2 = -2 * t3 + 3 * t2;
        float Blend3 = t3 - 2 * t2 + t;
        float Blend4 = t3 - t2;

        return Blend1 * P1 + Blend2 * P2 + Blend3 * T1 + Blend4 * T2;
    }

    private float _Ease(float t, float k1, float k2)
    {
        float f; float s;

        f = k1 * 2 / Mathf.PI + k2 - k1 + (1.0f - k2) * 2 / Mathf.PI;

        if (t < k1)
        {
            s = k1 * (2 / Mathf.PI) * (Mathf.Sin((t / k1) * Mathf.PI * 0.5f - Mathf.PI * 0.5f) + 1);
        }
        else if (t < k2)
        {
            s = (2 * k1 / Mathf.PI + t - k1);
        }
        else
        {
            s = 2 * k1 / Mathf.PI + k2 - k1 + ((1 - k2) * (2 / Mathf.PI)) * Mathf.Sin(((t - k2) / (1.0f - k2)) * Mathf.PI / 2);
        }

        return (s / f);
    }
    #endregion
}
