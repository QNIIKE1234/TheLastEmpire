namespace TheLastEmpire
{
    public interface IDamageable
    {
        void TakeDamage(float damageAmount);
        bool IsDead { get; }
    }
}
