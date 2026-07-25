namespace TheLastEmpire
{
    public interface IDamageable
    {
        void TakeDamage(float damageAmount, UnityEngine.Vector3? hitPoint = null);
        bool IsDead { get; }
    }
}
