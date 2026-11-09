namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Lê do banco o que o agregado do perfil não vê sozinho para decidir sobre uma versão. Quem chama já tem o perfil
/// travado, então o que lê aqui é o que as transações anteriores confirmaram.
/// </summary>
public sealed class LeitorDoContextoDaVersao
{
    private readonly IPerfilRepository _perfis;
    private readonly IConcessaoRepository _concessoes;

    public LeitorDoContextoDaVersao(IPerfilRepository perfis, IConcessaoRepository concessoes)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(concessoes);

        _perfis = perfis;
        _concessoes = concessoes;
    }

    /// <summary>Se o nome é de outro perfil, sem distinguir maiúsculas de minúsculas.</summary>
    public Task<bool> NomeEmUsoPorOutroAsync(string nome, Guid? perfilId, CancellationToken cancellationToken) =>
        _perfis.NomeExisteAsync(nome, perfilId, cancellationToken);

    public async Task<ContextoDaVersao> LerAsync(Guid perfilId, string nome, CancellationToken cancellationToken) =>
        new(
            await NomeEmUsoPorOutroAsync(nome, perfilId, cancellationToken).ConfigureAwait(false),
            await _concessoes.ExisteNaoRevogadaDoPerfilAsync(perfilId, cancellationToken).ConfigureAwait(false));
}
