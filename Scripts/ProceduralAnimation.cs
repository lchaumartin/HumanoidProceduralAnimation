using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimation : MonoBehaviour
{

    /* Some useful functions we may need */

    static Vector3[] CastOnSurface(Vector3 point, float halfRange, Vector3 up)
    {
        Vector3[] res = new Vector3[2];
        RaycastHit hit;
        Ray ray = new Ray(new Vector3(point.x, point.y + halfRange, point.z), -up);

        if (Physics.Raycast(ray, out hit, 2f * halfRange))
        {
            res[0] = hit.point;
            res[1] = hit.normal;
        }
        else
        {
            res[0] = point;
        }
        return res;
    }

    public static float GetAngle2D(Vector2 a, Vector2 b)
    {
        return Mathf.Acos(Vector2.Dot(a, b));
    }

    public static Vector2 Rotate2D(Vector2 v, float delta)
    {
        return new Vector2(
            v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
            v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
        );
    }

    static bool IsInEllipse(Vector2 point, Vector2 center, float minorRadius, float majorRadius)
    {
        return Mathf.Pow((point.x - center.x) / majorRadius, 2f) + Mathf.Pow((point.y - center.y) / minorRadius, 2f) <= 1f;
    }

    /*************************************/


    public Transform leftFootTarget;
    public Transform rightFootTarget;

    public Transform ellipse;
    public float balancingMinorRadius = 0.25f;
    public float balancingMajorRadius = 0.75f;

    public float movingStep = 0.05f;

    public float smoothness = 5f;

    public float stepHeight = 0.1f;

    private Vector3 initLeftFootPos;
    private Vector3 initRightFootPos;

    private Vector3 lastLeftFootPos;
    private Vector3 lastRightFootPos;

    private Vector3 lastBodyPos;

    private Vector3 velocity;

    private bool leftFootMoving = false;
    private bool rightFootMoving = false;

    // Start is called before the first frame update
    void Start()
    {
        initLeftFootPos = leftFootTarget.localPosition;
        initRightFootPos = rightFootTarget.localPosition;

        lastLeftFootPos = leftFootTarget.position;
        lastRightFootPos = rightFootTarget.position;

        lastBodyPos = transform.position;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!IsBalanced())
            MakeStep();
        else
            StickToTheGround();
        lastBodyPos = (transform.position + smoothness * lastBodyPos) / (1f + smoothness);
    }

    void MakeStep()
    {
        // We could use the rigidbody's velocity property but we don't wan't to depend on any rigidbody, since some controllers don't use any
        velocity = transform.position - lastBodyPos;
        if (velocity.magnitude == 0f)
            velocity = transform.forward/1000f;
        Vector3 centerOfMass = transform.position + velocity * 3f;
        float leftFootDistance = Vector3.Distance(leftFootTarget.position, centerOfMass);
        float rightFootDistance = Vector3.Distance(rightFootTarget.position, centerOfMass);

        // If the left foot is further
        if (leftFootDistance >= rightFootDistance)
        {
            if (!(leftFootMoving || rightFootMoving))
            {
                leftFootMoving = true;
                Vector3 nextPosition = NextStep(rightFootTarget.position, centerOfMass);
                Vector3[] posAndNormal = CastOnSurface(nextPosition, 2f, transform.up);
                StartCoroutine(MakeLeftStep(posAndNormal[0]));
                leftFootTarget.rotation = Quaternion.LookRotation(velocity.normalized, posAndNormal[1]);
            }
            else
                StickToTheGround();

        }
        else
        {
            if (!(leftFootMoving || rightFootMoving))
            {
                rightFootMoving = true;
                Vector3 nextPosition = NextStep(leftFootTarget.position, centerOfMass);
                Vector3[] posAndNormal = CastOnSurface(nextPosition, 2f, transform.up);
                StartCoroutine(MakeRightStep(posAndNormal[0]));
                rightFootTarget.rotation = Quaternion.LookRotation(velocity.normalized, posAndNormal[1]);
            }
            else
                StickToTheGround();
        }
    }

    IEnumerator MakeLeftStep(Vector3 nextPos)
    {
        Vector3 startPos = leftFootTarget.position;
        for (float l = 0f; l <= 1f; l += movingStep * Time.fixedDeltaTime)
        {
            leftFootTarget.position = Vector3.Lerp(startPos, nextPos, l);
            leftFootTarget.position += new Vector3(0f, 0.5f - Mathf.Abs(l - 0.5f), 0f) * 2f * stepHeight;
            yield return new WaitForFixedUpdate();
        }
        leftFootTarget.position = nextPos;
        lastLeftFootPos = leftFootTarget.position;
        leftFootMoving = false;
    }

    IEnumerator MakeRightStep(Vector3 nextPos)
    {
        Vector3 startPos = rightFootTarget.position;
        for (float l = 0f; l <= 1f; l += movingStep * Time.fixedDeltaTime)
        {
            rightFootTarget.position = Vector3.Lerp(startPos, nextPos, l);
            rightFootTarget.position += new Vector3(0f, 0.5f - Mathf.Abs(l - 0.5f), 0f) * 2f * stepHeight;
            yield return new WaitForFixedUpdate();
        }
        rightFootTarget.position = nextPos;
        lastRightFootPos = rightFootTarget.position;
        rightFootMoving = false;
    }

    void StickToTheGround()
    {
        leftFootTarget.position = lastLeftFootPos;
        rightFootTarget.position = lastRightFootPos;
    }

    bool IsBalanced()
    {
        float feetDistance = (leftFootTarget.position - rightFootTarget.position).magnitude;
        if (feetDistance > 0.5f && velocity.magnitude < 0.001)
            return false;
        Vector3 ellipseCenter = (leftFootTarget.position + rightFootTarget.position) / 2f + velocity;
        ellipseCenter = Vector3.ProjectOnPlane(ellipseCenter, transform.up);
        Vector2 ellipseCenter2D = new Vector2(ellipseCenter.x, ellipseCenter.z);
        Vector3 point = Vector3.ProjectOnPlane(transform.position, transform.up);
        Vector2 point2D = new Vector2(point.x, point.z);
        Vector3 feetAxis = (rightFootTarget.position - leftFootTarget.position).normalized;
        float feetAngle = Vector3.Angle(feetAxis, Vector3.right);
        if (Vector3.Dot(feetAxis, Vector3.forward) > 0)
            feetAngle = -feetAngle;
        Vector3 centerOfRot = ellipseCenter;
        Vector2 centerOfRot2D = new Vector2(centerOfRot.x, centerOfRot.z);
        Vector2 rotatedPoint2D = Rotate2D(point2D - centerOfRot2D, feetAngle) + centerOfRot2D;
        return IsInEllipse(rotatedPoint2D, ellipseCenter2D, balancingMinorRadius * Mathf.Clamp(velocity.magnitude * 2f, 0.5f, 1f), feetDistance/2f + balancingMajorRadius);
    }

    Vector3 NextStep(Vector3 otherFoot, Vector3 c)
    {
        Vector3 velocity = transform.position - lastBodyPos;
        return c + (c - otherFoot);
    }

    private void OnDrawGizmosSelected()
    {
        //Draw The Ellipse
        Vector3 ellipseCenter = (leftFootTarget.position + rightFootTarget.position) / 2f;
        ellipse.position = ellipseCenter;
        float feetDistance = (leftFootTarget.position - rightFootTarget.position).magnitude;
        ellipse.localScale = new Vector3(feetDistance + 2f * balancingMajorRadius, 0.01f, 2f * balancingMinorRadius);
        Vector3 feetAxis = (rightFootTarget.position - leftFootTarget.position).normalized;
        float feetAngle = Vector3.Angle(feetAxis, Vector3.right);
        if (Vector3.Dot(feetAxis, Vector3.forward) > 0)
            feetAngle = -feetAngle;
        ellipse.rotation = Quaternion.Euler(0f, feetAngle, 0f);

        Gizmos.color = IsBalanced() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(leftFootTarget.position, 0.2f);
        Gizmos.DrawWireSphere(rightFootTarget.position, 0.2f);
        Debug.DrawLine(transform.position, transform.position + transform.up * 2f, Color.green);
    }
}
