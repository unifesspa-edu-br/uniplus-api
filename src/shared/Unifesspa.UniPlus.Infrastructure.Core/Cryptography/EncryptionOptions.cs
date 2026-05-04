namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using System.ComponentModel.DataAnnotations;

public sealed class EncryptionOptions
{
    public const string SectionName = "UniPlus:Encryption";

    /// <summary>Provedor ativo: "vault" (produção) ou "local" (dev/CI).</summary>
    [Required]
    public string Provider { get; set; } = "local";

    /// <summary>
    /// Chave AES-GCM 256 em Base64 (32 bytes). Lida de env var ou appsettings.
    /// Obrigatória quando Provider = "local". Nunca commitar valor real.
    /// </summary>
    public string? LocalKey { get; set; }

    /// <summary>Endereço do Vault (ex.: https://vault.unifesspa.edu.br). Obrigatório quando Provider = "vault".</summary>
    public string? VaultAddress { get; set; }

    /// <summary>Nome do mount do transit engine no Vault (padrão: "transit").</summary>
    public string VaultTransitMount { get; set; } = "transit";

    /// <summary>Path do service account JWT para autenticação K8s (padrão: /var/run/secrets/kubernetes.io/serviceaccount/token).</summary>
    public string KubernetesJwtPath { get; set; } = "/var/run/secrets/kubernetes.io/serviceaccount/token";

    /// <summary>Role do Vault para autenticação K8s.</summary>
    public string? KubernetesRole { get; set; }

    /// <summary>
    /// Token Vault estático para autenticação por token (testes de integração / dev sem K8s).
    /// Quando definido, substitui o fluxo de autenticação Kubernetes.
    /// <b>Nunca usar em produção</b> — produção sempre usa KubernetesJwtPath.
    /// </summary>
    public string? VaultToken { get; set; }
}
