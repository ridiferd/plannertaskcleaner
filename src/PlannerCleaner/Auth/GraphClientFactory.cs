using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using PlannerCleaner.Options;

namespace PlannerCleaner.Auth;

public static class GraphClientFactory
{
    /// <summary>Membuat GraphServiceClient sesuai AuthMode (DeviceCode atau Entra).</summary>
    public static GraphServiceClient Create(CleanerOptions options)
    {
        TokenCredential credential = options.AuthMode switch
        {
            AuthMode.DeviceCode => CreateDeviceCode(options),
            AuthMode.Entra => CreateEntra(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options.AuthMode), options.AuthMode, "Mode login tidak dikenal.")
        };

        Console.WriteLine($"Mode login: {options.AuthMode}");
        return new GraphServiceClient(credential, CleanerOptions.Scopes);
    }

    /// <summary>Device code dengan Client ID publik Microsoft Graph Command Line Tools. Tanpa App Registration.</summary>
    private static TokenCredential CreateDeviceCode(CleanerOptions options) =>
        new DeviceCodeCredential(new DeviceCodeCredentialOptions
        {
            TenantId = options.TenantId,
            ClientId = CleanerOptions.GraphCliClientId,
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "PlannerCleaner.DeviceCode" },
            DeviceCodeCallback = (info, _) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.WriteLine($"Buka   : {info.VerificationUri}");
                Console.WriteLine($"Kode   : {info.UserCode}");
                Console.WriteLine($"Berlaku sampai {info.ExpiresOn.ToLocalTime():HH:mm}");
                Console.ResetColor();
                return Task.CompletedTask;
            }
        });

    /// <summary>Login browser interaktif dengan App Registration sendiri di Entra ID.</summary>
    private static TokenCredential CreateEntra(CleanerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EntraClientId))
            throw new InvalidOperationException(
                "Mode Entra membutuhkan Client ID App Registration. Isi Cleaner:EntraClientId atau pakai --client-id <GUID>.");

        return new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
        {
            TenantId = options.TenantId,
            ClientId = options.EntraClientId,
            RedirectUri = new Uri(options.RedirectUri),
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "PlannerCleaner.Entra" }
        });
    }
}
