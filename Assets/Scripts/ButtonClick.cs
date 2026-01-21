using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class ButtonClick : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private Selectable selectable;

    void Awake() => selectable = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        AudioManager.I.PlaySfx(SfxId.ButtonHover);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        AudioManager.I.PlaySfx(SfxId.ButtonClick);
    }

    private bool IsInteractable() => selectable != null && selectable.IsInteractable();
}
