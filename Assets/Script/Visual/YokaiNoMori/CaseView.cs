using UnityEngine;
using YokaiNoMori.Interface;

public class CaseView : MonoBehaviour
{
    private IBoardCase model;
    private Renderer caseRenderer;

    [SerializeField] private Color hoverColor = Color.yellow;
    private Color originalColor;

    private void Awake()
    {
        caseRenderer = GetComponentInChildren<Renderer>();
        if (caseRenderer != null) originalColor = caseRenderer.material.color;
    }

    public void Setup(IBoardCase boardCase)
    {
        this.model = boardCase;
    }

    public IBoardCase GetModel() => model;

    public void Highlight(bool value)
    {
        if (caseRenderer == null) return;

        caseRenderer.material.color = value ? hoverColor : originalColor;
    }
}