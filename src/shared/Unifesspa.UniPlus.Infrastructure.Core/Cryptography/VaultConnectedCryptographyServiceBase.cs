namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.AuthMethods.Token;

/// <summary>
/// Conexão com o Vault e retry de autenticação compartilhados entre os provedores Vault da
/// cifra em repouso e do índice cego — o ciclo de vida do cliente e o retry em 403 (JWT
/// rotacionado) não dependem de qual operação do transit engine é chamada por cima.
/// </summary>
internal abstract class VaultConnectedCryptographyServiceBase : IDisposable
{
    private readonly string _vaultAddress;
    private readonly string _jwtPath;
    private readonly string? _role;
    private readonly string? _vaultToken;
    private readonly bool _useKubernetesAuth;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile VaultClient _vault;

    protected VaultConnectedCryptographyServiceBase(EncryptionOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        if (string.IsNullOrWhiteSpace(opts.VaultAddress))
        {
            throw new InvalidOperationException(
                "UniPlus:Encryption:VaultAddress é obrigatório quando Provider = 'vault'.");
        }

        bool hasRole = !string.IsNullOrWhiteSpace(opts.KubernetesRole);
        bool hasToken = !string.IsNullOrWhiteSpace(opts.VaultToken);

        // EncryptionOptionsValidator já enforça exatamente um dos dois quando Provider=vault.
        // As guardas defensivas abaixo cobrem o fluxo de testes que instancia o serviço
        // diretamente (sem passar pelo validator), tornando a violação explícita em vez
        // de um NullReferenceException mais adiante em CreateVaultClient.
        if (hasRole && hasToken)
        {
            throw new InvalidOperationException(
                "UniPlus:Encryption: KubernetesRole e VaultToken são mutuamente exclusivos quando Provider = 'vault'. " +
                "Em produção use KubernetesRole; em testes/dev use VaultToken. " +
                "Ver EncryptionOptionsValidator e docs/guia-config-cifragem.md.");
        }

        if (!hasRole && !hasToken)
        {
            throw new InvalidOperationException(
                "UniPlus:Encryption: nem KubernetesRole nem VaultToken estão definidos quando Provider = 'vault'. " +
                "Configure UNIPLUS__ENCRYPTION__KUBERNETESROLE (produção) ou UNIPLUS__ENCRYPTION__VAULTTOKEN (testes/dev). " +
                "Ver EncryptionOptionsValidator e docs/guia-config-cifragem.md.");
        }

        _vaultAddress = opts.VaultAddress;
        _jwtPath = opts.KubernetesJwtPath;
        _role = opts.KubernetesRole;
        _vaultToken = opts.VaultToken;
        _useKubernetesAuth = hasRole;

        _vault = CreateVaultClient();
    }

    public void Dispose() => _refreshLock.Dispose();

    /// <summary>
    /// Executa <paramref name="operation"/> com retry automático quando o Vault retorna 403.
    /// O retry relê o JWT do disco, garantindo que tokens K8s rotacionados sejam absorvidos
    /// sem restart. <paramref name="onAuthRefresh"/> loga o evento com o contexto (nome da
    /// chave) de quem chamou — este tipo não conhece qual operação está sendo repetida.
    /// </summary>
    protected async Task<T> ExecuteWithAuthRetryAsync<T>(Func<VaultClient, Task<T>> operation, Action onAuthRefresh)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onAuthRefresh);

        try
        {
            return await operation(_vault).ConfigureAwait(false);
        }
        catch (VaultApiException vex) when (vex.StatusCode == 403)
        {
            onAuthRefresh();
            await RefreshVaultClientAsync().ConfigureAwait(false);
            return await operation(_vault).ConfigureAwait(false);
        }
    }

    private VaultClient CreateVaultClient()
    {
        // O auth method é determinado pela configuração validada (EncryptionOptionsValidator
        // garante exatamente um entre KubernetesRole e VaultToken). Sem heurística de
        // File.Exists: se a config disser "K8s" mas o JWT não estiver disponível em disco,
        // falha-se com mensagem específica em vez de cair silenciosamente para token estático.
        IAuthMethodInfo authMethod = _useKubernetesAuth
            ? new KubernetesAuthMethodInfo(_role, ReadJwtOrThrow())
            : new TokenAuthMethodInfo(_vaultToken!);

        return new VaultClient(new VaultClientSettings(_vaultAddress, authMethod));
    }

    private string ReadJwtOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_jwtPath))
        {
            throw new InvalidOperationException(
                "UniPlus:Encryption:KubernetesJwtPath está vazio. Configure o path do JWT do " +
                "ServiceAccount (default: /var/run/secrets/kubernetes.io/serviceaccount/token).");
        }

        if (!File.Exists(_jwtPath))
        {
            throw new InvalidOperationException(
                $"JWT do ServiceAccount não encontrado em '{_jwtPath}'. " +
                "Verifique se o ServiceAccount está montado no Pod (automountServiceAccountToken=true) " +
                "e se o caminho UniPlus:Encryption:KubernetesJwtPath corresponde ao volume do token.");
        }

        string content = File.ReadAllText(_jwtPath);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                $"JWT do ServiceAccount em '{_jwtPath}' está vazio. " +
                "O kubelet costuma re-popular o token automaticamente; verificar logs do pod e " +
                "o estado do volume projetado do ServiceAccount.");
        }

        return content;
    }

    private async Task RefreshVaultClientAsync()
    {
        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _vault = CreateVaultClient();
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
