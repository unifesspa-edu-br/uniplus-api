namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using Microsoft.Extensions.Options;

/// <summary>
/// Valida <see cref="EncryptionOptions"/> no boot da aplicação para falhar com mensagem
/// específica quando a combinação de Provider + campos dependentes está inconsistente
/// (por exemplo, Provider=local sem LocalKey). Sem este validator a configuração inválida
/// só estouraria na primeira request que toca cifragem, retornando 500 ao consumer.
///
/// Detalhes em <c>docs/guia-config-cifragem.md</c>.
/// </summary>
internal sealed class EncryptionOptionsValidator : IValidateOptions<EncryptionOptions>
{
    private const int RequiredKeyBytes = 32;
    private const string GuideHint = "Detalhes em docs/guia-config-cifragem.md.";
    private static readonly string[] AllowedProviders = ["local", "vault"];

    public ValidateOptionsResult Validate(string? name, EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Só valida a instância default — projeto não usa named options para cifragem.
        if (!string.IsNullOrEmpty(name) && name != Options.DefaultName)
        {
            return ValidateOptionsResult.Skip;
        }

        List<string> failures = [];

        string provider = options.Provider?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(provider))
        {
            failures.Add(
                $"UniPlus:Encryption:Provider é obrigatório. Valores aceitos: 'local', 'vault'. {GuideHint}");
        }
        else if (!IsKnownProvider(provider))
        {
            failures.Add(
                $"UniPlus:Encryption:Provider inválido: '{options.Provider}'. " +
                $"Valores aceitos: 'local', 'vault'. {GuideHint}");
        }
        else if (provider.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            ValidateLocal(options, failures);
        }
        else if (provider.Equals("vault", StringComparison.OrdinalIgnoreCase))
        {
            ValidateVault(options, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsKnownProvider(string provider) =>
        AllowedProviders.Any(allowed => provider.Equals(allowed, StringComparison.OrdinalIgnoreCase));

    private static void ValidateLocal(EncryptionOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.LocalKey))
        {
            failures.Add(
                "UniPlus:Encryption:LocalKey é obrigatório quando Provider = 'local'. " +
                "Defina via env var UNIPLUS__ENCRYPTION__LOCALKEY (Base64, 32 bytes). " +
                "Gere uma chave dev/CI com `head -c 32 /dev/urandom | base64`. " +
                $"{GuideHint}");
            return;
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(options.LocalKey);
        }
        catch (FormatException)
        {
            failures.Add(
                "UniPlus:Encryption:LocalKey não é uma string Base64 válida. " +
                "Gere uma chave válida com `head -c 32 /dev/urandom | base64`. " +
                $"{GuideHint}");
            return;
        }

        if (keyBytes.Length != RequiredKeyBytes)
        {
            failures.Add(
                $"UniPlus:Encryption:LocalKey deve ter {RequiredKeyBytes} bytes (256 bits) após decode Base64. " +
                $"Recebido: {keyBytes.Length} bytes. " +
                $"{GuideHint}");
        }
    }

    private static void ValidateVault(EncryptionOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.VaultAddress))
        {
            failures.Add(
                "UniPlus:Encryption:VaultAddress é obrigatório quando Provider = 'vault'. " +
                "Exemplo: 'http://platform-vault-uniplus-standalone.vault.svc.cluster.local:8200'. " +
                $"{GuideHint}");
        }
        else if (!IsAbsoluteHttpUri(options.VaultAddress))
        {
            failures.Add(
                $"UniPlus:Encryption:VaultAddress inválido: '{options.VaultAddress}'. " +
                "Deve ser uma URL absoluta com scheme http ou https. " +
                $"{GuideHint}");
        }

        bool hasKubernetesRole = !string.IsNullOrWhiteSpace(options.KubernetesRole);
        bool hasVaultToken = !string.IsNullOrWhiteSpace(options.VaultToken);

        if (!hasKubernetesRole && !hasVaultToken)
        {
            failures.Add(
                "UniPlus:Encryption requer KubernetesRole (produção) ou VaultToken (testes/dev) quando Provider = 'vault'. " +
                "Configure UNIPLUS__ENCRYPTION__KUBERNETESROLE (role K8s auth registrada no Vault) " +
                "ou UNIPLUS__ENCRYPTION__VAULTTOKEN (apenas para testes de integração — nunca em produção). " +
                $"{GuideHint}");
        }
        else if (hasKubernetesRole && hasVaultToken)
        {
            // O auth method é determinado pela configuração validada: KubernetesRole ativa Kubernetes auth,
            // VaultToken ativa token auth. Aceitar ambos definidos forçaria uma escolha implícita no
            // VaultTransitEncryptionService (originalmente uma heurística "K8s se JWT existir, senão token",
            // retirada exatamente por este motivo). Operador escolhe um, explicitamente.
            failures.Add(
                "UniPlus:Encryption: KubernetesRole e VaultToken são mutuamente exclusivos. " +
                "Em produção use KubernetesRole; em testes/dev use VaultToken. " +
                $"{GuideHint}");
        }
    }

    private static bool IsAbsoluteHttpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
