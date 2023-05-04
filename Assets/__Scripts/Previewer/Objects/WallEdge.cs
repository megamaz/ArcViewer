using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class WallEdge : MonoBehaviour
{
    [SerializeField] private WallHandler scaleHandler;
    [SerializeField] private float thickness;
    [SerializeField] private ScaleDirection scaleDirection;
    [SerializeField] private YAnchor yAnchor;
    [SerializeField] private XAnchor xAnchor;
    [SerializeField] private ZAnchor zAnchor;

    private MeshRenderer meshRenderer;


    public void UpdateScale(Vector3 newScale)
    {
        Vector3 scale = transform.localScale;

        switch(scaleDirection)
        {
            case ScaleDirection.x:
                scale.x = newScale.x + thickness;
                break;
            case ScaleDirection.y:
                scale.y = newScale.y + thickness;
                break;
            case ScaleDirection.z:
                scale.z = newScale.z + thickness;
                break;
        }

        transform.localScale = scale;

        Vector3 position = transform.localPosition;

        switch(yAnchor)
        {
            case YAnchor.top:
                position.y = newScale.y / 2;
                break;
            case YAnchor.mid:
                position.y = 0;
                break;
            case YAnchor.bottom:
                position.y = (newScale.y / 2) * -1;
                break;
        }

        switch(xAnchor)
        {
            case XAnchor.left:
                position.x = newScale.x / 2;
                break;
            case XAnchor.mid:
                position.x = 0;
                break;
            case XAnchor.right:
                position.x = (newScale.x / 2) * -1;
                break;
        }

        switch(zAnchor)
        {
            case ZAnchor.front:
                position.z = newScale.z / 2;
                break;
            case ZAnchor.mid:
                position.z = 0;
                break;
            case ZAnchor.back:
                position.z = (newScale.z / 2) * -1;
                break;
        }

        transform.localPosition = position;
    }


    public void UpdateProperties(MaterialPropertyBlock properties)
    {
        meshRenderer.SetPropertyBlock(properties);
    }


    private void OnEnable()
    {
        if(!meshRenderer)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        scaleHandler.OnScaleUpdated += UpdateScale;
        scaleHandler.OnPropertiesUpdated += UpdateProperties;
    }


    private void OnDestroy()
    {
        scaleHandler.OnScaleUpdated -= UpdateScale;
        scaleHandler.OnPropertiesUpdated -= UpdateProperties;
    }


    private enum ScaleDirection
    {
        x,
        y,
        z
    }

    private enum YAnchor
    {
        top,
        mid,
        bottom
    }

    private enum XAnchor
    {
        left,
        mid,
        right
    }

    private enum ZAnchor
    {
        front,
        mid,
        back
    }
}