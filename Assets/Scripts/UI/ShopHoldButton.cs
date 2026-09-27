using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在商店的「－」「＋」按钮上：按住不动就会持续增减数量。
/// 单击由 Button.onClick 处理（±1），按住则由这里每隔一小段再变一次。
/// </summary>
public class ShopHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("+1 或 -1")]
    [SerializeField] private int direction = 1;

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (ShopUI.Instance != null)
            ShopUI.Instance.BeginHold(direction);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        if (ShopUI.Instance != null)
            ShopUI.Instance.EndHold();
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (ShopUI.Instance != null)
            ShopUI.Instance.EndHold();
    }

    private void OnDisable()
    {
        if (ShopUI.Instance != null)
            ShopUI.Instance.EndHold();
    }
}
