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

        public class RegisterResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public bool EmailSent { get; set; } = true;
        }

        public class ResendConfirmationModel
        {
            public string UsernameOrEmail { get; set; } = "";
        }

        public class ForgotPasswordModel
        {
            public string UsernameOrEmail { get; set; } = "";
        }

        public class ResetPasswordModel
        {
            public string Token { get; set; } = "";
            public string NewPassword { get; set; } = "";
        }
    }
}
