using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/SlantedImage")]
public class SlantedImage : BaseMeshEffect
{
    public float slantAmount = 50f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        int count = vh.currentVertCount;
        UIVertex vertex = new UIVertex();

        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            // If the vertex is at the top of the image, shift it right
            if (vertex.position.y > 0)
            {
                vertex.position.x += slantAmount;
            }
            // If the vertex is at the bottom, shift it left (optional, for center slant)
            else
            {
                vertex.position.x -= slantAmount;
            }

            vh.SetUIVertex(vertex, i);
        }
    }
}