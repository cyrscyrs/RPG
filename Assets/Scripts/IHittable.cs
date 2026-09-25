/// <summary>
/// 可被玩家攻击命中的目标（可破坏墙体、木箱、机关等）。
/// 玩家的攻击判定命中后会调用 TakeHit()，需要命中多少次才摧毁由实现类自己决定；
/// 这样攻击方不需要认识具体是什么东西，以后加新的可破坏物也不用改攻击代码。
/// </summary>
public interface IHittable
{
    void TakeHit();
}
