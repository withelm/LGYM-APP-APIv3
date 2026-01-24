namespace LgymApi.Domain.Enums;

public static class Message
{
    public const string Created = "Created";
    public const string FieldRequired = "All fields required";
    public const string ChooseDays = "You have to choose days number between 1-7";
    public const string Updated = "Updated";
    public const string TryAgain = "Error, try again";
    public const string Deleted = "Deleted!";
    public const string DidntFind = "Didnt find!";
    public const string InvalidToken = "Invalid JWT Token";
    public const string ExpiredToken = "Token expired";
    public const string InputsMustBeNumbers = "Inputs must be numbers";
    public const string NameIsRequired = "Name is required, and has to have minimum one character";
    public const string EmailInvalid = "This email is not valid!";
    public const string PasswordMin = "Passwword need to have minimum six characters";
    public const string SamePassword = "Passwords need to be same";
    public const string UserWithThatName = "We have user with that name";
    public const string UserWithThatEmail = "We have user with that email";
    public const string Unauthorized = "Unauthorized";
    public const string Forbidden = "Forbidden";
}
