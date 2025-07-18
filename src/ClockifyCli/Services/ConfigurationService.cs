using System.Security.Cryptography;
using System.Text;
using ClockifyCli.Models;
using Newtonsoft.Json;

namespace ClockifyCli.Services;

public class ConfigurationService
{
    private const string ConfigFileName = "clockify-config.dat";
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClockifyCli",
        ConfigFileName
    );

    private static readonly byte[] AdditionalEntropy =
        Encoding.UTF8.GetBytes("ClockifyCli-SecureConfig-2024");

    public async Task<AppConfiguration> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return AppConfiguration.Empty;
            }

            var encryptedData = await File.ReadAllBytesAsync(ConfigFilePath);
            var decryptedData = DecryptData(encryptedData);

            var json = Encoding.UTF8.GetString(decryptedData);
            return JsonConvert.DeserializeObject<AppConfiguration>(json) ?? AppConfiguration.Empty;
        }
        catch (Exception)
        {
            // If decryption fails or file is corrupted, return empty config
            return AppConfiguration.Empty;
        }
    }

    public async Task SaveConfigurationAsync(AppConfiguration configuration)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(ConfigFilePath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            var plainData = Encoding.UTF8.GetBytes(json);
            var encryptedData = EncryptData(plainData);

            await File.WriteAllBytesAsync(ConfigFilePath, encryptedData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    public async Task<AppConfiguration> UpdateConfigurationAsync(
        string? clockifyApiKey = null,
        string? jiraUsername = null,
        string? jiraApiToken = null,
        string? tempoApiKey = null)
    {
        var currentConfig = await LoadConfigurationAsync();

        var updatedConfig = new AppConfiguration(
            clockifyApiKey ?? currentConfig.ClockifyApiKey,
            jiraUsername ?? currentConfig.JiraUsername,
            jiraApiToken ?? currentConfig.JiraApiToken,
            tempoApiKey ?? currentConfig.TempoApiKey
        );

        await SaveConfigurationAsync(updatedConfig);
        return updatedConfig;
    }

    public bool ConfigurationExists()
    {
        return File.Exists(ConfigFilePath);
    }

    public string GetConfigurationPath()
    {
        return ConfigFilePath;
    }

    private static byte[] EncryptData(byte[] plainData)
    {
        // Use AES encryption for cross-platform compatibility
        using var aes = Aes.Create();
        aes.Key = DeriveKey(AdditionalEntropy);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(plainData, 0, plainData.Length);
        }

        return ms.ToArray();
    }

    private static byte[] DecryptData(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(AdditionalEntropy);

        // Extract IV from the beginning of the encrypted data
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();

        cs.CopyTo(result);
        return result.ToArray();
    }

    private static byte[] DeriveKey(byte[] password)
    {
        // Use PBKDF2 to derive a key from the password
        using var pbkdf2 = new Rfc2898DeriveBytes(password, password, 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }
}