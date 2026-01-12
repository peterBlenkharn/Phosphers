//TestBitTarget.cs

using UnityEngine;
using Phosphers.Resources;

public class TestBitTarget : MonoBehaviour, IBitTarget
{
    public bool IsAvailable => true;
    public BitSpec Spec => BitSpec.Generic;
    public Transform Transform => transform;
    public Vector2 Position => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
