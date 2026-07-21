namespace TankDuel.Data
{
    /// <summary>
    /// Оси прокачки в матче.
    /// При добавлении оси: добавить значение сюда, поднять TankBuild.UpgradeTypesTotal,
    /// добавить трек в UpgradeConfig-ассет.
    /// </summary>
    public enum UpgradeType
    {
        Damage = 0,
        FireRate = 1,
        MoveSpeed = 2,
        Health = 3,
    }
}
