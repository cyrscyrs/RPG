using UnityEngine;

/// <summary>
/// 挂在「自己管理显示/隐藏」的 UI 面板根节点上（对话界面、装备强化界面这种）。
///
/// 为什么需要它：UI.cs 切换菜单时会把 UI 物体（你的场景里就是 Canvas）下面**所有**子物体
/// SetActive(false)，而这样会把面板根节点一起关掉——根节点一关，面板上的脚本就完全不再运行，
/// 于是「按W对话」的提示不显示、打开强化界面的动作也没人接。
///
/// 挂上本组件的面板会被 UI.cs 跳过，根节点始终保持激活；面板自己的显示/隐藏由它自己的脚本
/// 控制（一般是开关一个 Window 子节点），所以不会互相干扰。
/// </summary>
public class UI_SelfManagedPanel : MonoBehaviour
{
    /// <summary>切换到别的菜单时触发，自管理面板监听它来把自己收起来。</summary>
    public static event System.Action OnOtherMenuOpened;

    public static void NotifyOtherMenuOpened() => OnOtherMenuOpened?.Invoke();
}
