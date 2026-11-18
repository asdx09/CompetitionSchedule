namespace ScheduleLogic.Server.Services.Security
{
    /// <summary>Jelszószabályok (regisztráció és jelszó-visszaállítás; a PHP backenddel azonos).</summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MaxLength = 1024;
    }
}
