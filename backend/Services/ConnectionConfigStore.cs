using System.Text.Json;
using AustralCreditAnalytics.Api.Models;
using Microsoft.AspNetCore.DataProtection;

namespace AustralCreditAnalytics.Api.Services;

/// <summary>
/// Persiste a <see cref="ConnectionConfig"/> cifrada em App_Data/connection.dat
/// usando ASP.NET Data Protection. A senha nunca e gravada nem retornada em texto plano
/// fora do uso interno (factory/tester).
/// </summary>
public class ConnectionConfigStore : IConnectionConfigStore
{
    private const string ProtectorPurpose = "AustralCreditAnalytics.ConnectionConfig.v1";

    private readonly IDataProtector _protector;
    private readonly string _filePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ConnectionConfigStore(IDataProtectionProvider provider, IWebHostEnvironment env)
    {
        _protector = provider.CreateProtector(ProtectorPurpose);

        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "connection.dat");
    }

    public bool IsConfigured => File.Exists(_filePath);

    public void Save(ConnectionConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOpts);
        var cipher = _protector.Protect(json);

        lock (_lock)
        {
            File.WriteAllText(_filePath, cipher);
        }
    }

    public ConnectionConfig? Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return null;

            try
            {
                var cipher = File.ReadAllText(_filePath);
                var json = _protector.Unprotect(cipher);
                return JsonSerializer.Deserialize<ConnectionConfig>(json, JsonOpts);
            }
            catch
            {
                // Arquivo corrompido ou chave de protecao trocada: trata como nao configurado.
                return null;
            }
        }
    }

    public ConnectionConfigView? LoadMasked()
    {
        var config = Load();
        if (config is null)
            return null;

        return new ConnectionConfigView
        {
            Server = config.Server,
            Port = config.Port,
            Database = config.Database,
            AuthMode = config.AuthMode,
            User = config.User,
            PasswordMask = string.IsNullOrEmpty(config.Password) ? null : new string('\u2022', 12),
            Encrypt = config.Encrypt,
            TrustServerCertificate = config.TrustServerCertificate,
            ConnectionTimeout = config.ConnectionTimeout,
            IsConfigured = true,
        };
    }
}
