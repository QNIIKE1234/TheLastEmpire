namespace TheLastEmpire
{
    public interface IDamageable
    {
        void TakeDamage(int damageAmount);
        bool IsDead { get; }
    }
}
