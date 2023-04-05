using Azure.Security.KeyVault.Secrets;

namespace WfpChatBotWebApp.Secrets;

public class KeyVaultManager : IKeyVaultManager
{
    private readonly SecretClient _secretClient;
    
    public KeyVaultManager(SecretClient secretClient)
        => _secretClient = secretClient;

    public async Task<string> GetSecretAsync(string secretName)
    {
        KeyVaultSecret keyValueSecret = await _secretClient.GetSecretAsync(secretName);
        return keyValueSecret.Value;
    }
}