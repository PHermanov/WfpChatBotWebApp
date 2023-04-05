namespace WfpChatBotWebApp.Secrets;

public interface IKeyVaultManager
{
    public Task<string> GetSecret(string secretName);
}