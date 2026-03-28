namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

/// <summary>
/// Stub para integração com Gov.br (Login Único).
/// A implementação completa será adicionada quando a integração com o Gov.br for configurada.
/// </summary>
public interface IGovBrAuthService
{
    /// <summary>
    /// Valida o token de autenticação do Gov.br e retorna o CPF do usuário autenticado.
    /// </summary>
    Task<string?> ValidarTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém os dados básicos do cidadão autenticado no Gov.br.
    /// </summary>
    Task<GovBrUserInfo?> ObterDadosUsuarioAsync(string accessToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dados do usuário retornados pelo Gov.br.
/// </summary>
public sealed record GovBrUserInfo(string Cpf, string Nome, string Email);

/// <summary>
/// Implementação stub do serviço de autenticação Gov.br.
/// </summary>
public sealed class GovBrAuthService : IGovBrAuthService
{
    public Task<string?> ValidarTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // Stub: retorna null indicando token inválido até a integração real ser implementada
        return Task.FromResult<string?>(null);
    }

    public Task<GovBrUserInfo?> ObterDadosUsuarioAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        // Stub: retorna null até a integração real ser implementada
        return Task.FromResult<GovBrUserInfo?>(null);
    }
}
