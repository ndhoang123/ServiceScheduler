namespace ServiceScheduler.Api.Services.Interface;

public interface IUserCredentialStore
{
    bool TryValidate(string username, string password, out string role);
}
