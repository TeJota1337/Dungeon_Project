using UnityEngine;

// Faz um joint (filho de uma malha com blend shape) acompanhar a superfície quando ela
// dobra. Na pose de repouso (peso 0), acha o vértice mais próximo do joint uma vez só e
// guarda o deslocamento fixo entre os dois, em espaço local do renderer. A cada
// UpdateFollow() usa BakeMesh() pra achar onde esse mesmo vértice foi parar já deformado
// e reaplica esse deslocamento a partir do transform ATUAL do renderer — por isso continua
// correto mesmo com o estilingue se movendo (seguindo a mão, o pêndulo do treco etc.).
public class BlendShapeAnchor
{
    readonly SkinnedMeshRenderer targetRenderer;
    readonly Transform joint;
    readonly Mesh bakedMesh;
    readonly Vector3 restVertexLocal;
    readonly Vector3 jointOffsetLocal;
    readonly Vector3 restJointLocalPosition;
    readonly int vertexIndex;

    public BlendShapeAnchor(SkinnedMeshRenderer renderer, Transform jointTransform)
    {
        targetRenderer = renderer;
        joint = jointTransform;
        bakedMesh = new Mesh();
        restJointLocalPosition = joint.localPosition;

        Vector3[] restVertices = renderer.sharedMesh.vertices;
        Vector3 jointLocalToRenderer = renderer.transform.InverseTransformPoint(joint.position);

        vertexIndex = 0;
        float bestSqrDist = float.MaxValue;
        for (int i = 0; i < restVertices.Length; i++)
        {
            float sqrDist = (restVertices[i] - jointLocalToRenderer).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                vertexIndex = i;
            }
        }

        restVertexLocal = restVertices[vertexIndex];
        jointOffsetLocal = jointLocalToRenderer - restVertexLocal;
    }

    public void UpdateFollow()
    {
        targetRenderer.BakeMesh(bakedMesh, false);
        Vector3 deformedVertexLocal = bakedMesh.vertices[vertexIndex];

        joint.position = targetRenderer.transform.TransformPoint(deformedVertexLocal + jointOffsetLocal);
    }

    public void ResetToRest()
    {
        joint.localPosition = restJointLocalPosition;
    }
}
