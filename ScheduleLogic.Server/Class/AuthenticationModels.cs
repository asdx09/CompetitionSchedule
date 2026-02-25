namespace ScheduleLogic.Server.Class
{
    public class AuthenticationModels
    {
        public class LoginModel
        {
            public string Username { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        public class RegisterModel
        {
            public string Username { get; set; } = null!;
            public string Password { get; set; } = null!;
            public string Email { get; set; } = null!;
        }
    }
}
