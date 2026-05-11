namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Cobre apenas o construtor de <see cref="VaultTransitEncryptionService"/> — escolha
/// determinística do auth method baseada em config validada, sem heurística de
/// File.Exists. Round-trip encrypt/decrypt fica em IntegrationTests com Testcontainers.
/// </summary>
public sealed class VaultTransitEncryptionServiceConstructorTests : IDisposable
{
    private readonly string _jwtFile;

    public VaultTransitEncryptionServiceConstructorTests()
    {
        _jwtFile = Path.Combine(Path.GetTempPath(), $"uniplus-vault-test-jwt-{Guid.NewGuid():N}");
        File.WriteAllText(_jwtFile, "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.dummy.signature");
    }

    public void Dispose()
    {
        if (File.Exists(_jwtFile))
        {
            File.Delete(_jwtFile);
        }
    }

    private static VaultTransitEncryptionService CriarServico(EncryptionOptions opts) =>
        new(Options.Create(opts), NullLogger<VaultTransitEncryptionService>.Instance);

    [Fact]
    public void Construtor_SemVaultAddress_DeveLancarInvalidOperationException()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = null,
            KubernetesRole = "uniplus-api",
        };

        Action ato = () => CriarServico(opts);

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*UniPlus:Encryption:VaultAddress*");
    }

    [Fact]
    public void Construtor_SemRoleNemToken_DeveLancarInvalidOperationException()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault:8200",
            KubernetesRole = null,
            VaultToken = null,
        };

        Action ato = () => CriarServico(opts);

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*exatamente um*");
    }

    [Fact]
    public void Construtor_ComRoleEToken_DeveLancarInvalidOperationException()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault:8200",
            KubernetesRole = "uniplus-api",
            VaultToken = "hvs.dev",
            KubernetesJwtPath = _jwtFile,
        };

        Action ato = () => CriarServico(opts);

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*exatamente um*");
    }

    [Fact]
    public void Construtor_RoleConfiguradaSemJwtNoDisco_DeveLancarInvalidOperationException()
    {
        string pathInexistente = Path.Combine(Path.GetTempPath(), $"path-que-nao-existe-{Guid.NewGuid():N}");
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault:8200",
            KubernetesRole = "uniplus-api",
            KubernetesJwtPath = pathInexistente,
        };

        Action ato = () => CriarServico(opts);

        ato.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains(pathInexistente)
                && e.Message.Contains("ServiceAccount"));
    }

    [Fact]
    public void Construtor_RoleConfiguradaComJwtPathVazio_DeveLancarInvalidOperationException()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault:8200",
            KubernetesRole = "uniplus-api",
            KubernetesJwtPath = "   ",
        };

        Action ato = () => CriarServico(opts);

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*KubernetesJwtPath*");
    }

    [Fact]
    public void Construtor_RoleConfiguradaComJwtVazio_DeveLancarInvalidOperationException()
    {
        string emptyJwt = Path.Combine(Path.GetTempPath(), $"uniplus-vault-test-empty-jwt-{Guid.NewGuid():N}");
        File.WriteAllText(emptyJwt, "   \n\t   ");
        try
        {
            EncryptionOptions opts = new()
            {
                Provider = "vault",
                VaultAddress = "http://vault:8200",
                KubernetesRole = "uniplus-api",
                KubernetesJwtPath = emptyJwt,
            };

            Action ato = () => CriarServico(opts);

            ato.Should().Throw<InvalidOperationException>()
                .Where(e => e.Message.Contains(emptyJwt)
                    && e.Message.Contains("vazio"));
        }
        finally
        {
            File.Delete(emptyJwt);
        }
    }

    [Fact]
    public void Construtor_RoleConfiguradaEJwtPresente_DeveCriarServicoSemLancar()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault:8200",
            KubernetesRole = "uniplus-api",
            KubernetesJwtPath = _jwtFile,
        };

        Action ato = () => CriarServico(opts);

        ato.Should().NotThrow();
    }

    [Fact]
    public void Construtor_TokenConfiguradoESemJwtNoDisco_DeveCriarServicoSemLancar()
    {
        // Cenário típico dev/CI: VaultToken estático, JWT do K8s ausente. Nenhuma
        // leitura de disco deve ser tentada quando o auth method é Token.
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault:8200",
            VaultToken = "hvs.dev",
            KubernetesJwtPath = "/path/inexistente",
        };

        Action ato = () => CriarServico(opts);

        ato.Should().NotThrow();
    }
}
