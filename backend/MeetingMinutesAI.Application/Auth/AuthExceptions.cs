namespace MeetingMinutesAI.Application.Auth;

public sealed class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException(string email)
        : base($"A user with email '{email}' already exists.")
    {
        Email = email;
    }

    public string Email { get; }
}

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }
}

public sealed class UserNotFoundException : Exception
{
    public UserNotFoundException(Guid userId)
        : base($"User '{userId}' was not found.")
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}
