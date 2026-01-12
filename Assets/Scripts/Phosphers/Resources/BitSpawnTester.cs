using UnityEngine;
using Phosphers.Core;
using Phosphers.Resources;

public class BitSpawnTester : MonoBehaviour
{
    public int count = 5;
    public float radius = 2.5f;
    public KeyCode hotkey = KeyCode.B;

    private void Update()
    {
        if (Input.GetKeyDown(hotkey))
        {
            var rs = ResourceSystem.Instance;
            if (rs == null) return;

            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.value * 0.25f) * Mathf.PI * 2f / count;
                Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                rs.SpawnBit(BitSpec.Generic, pos, Random.Range(0f, 360f));
            }
        }
    }
}
