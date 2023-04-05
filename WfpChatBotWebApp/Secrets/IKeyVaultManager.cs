namespace WfpChatBotWebApp.Secrets;

public interface IKeyVaultManager
{
    public Task<string> GetSecretAsync(string secretName);
}