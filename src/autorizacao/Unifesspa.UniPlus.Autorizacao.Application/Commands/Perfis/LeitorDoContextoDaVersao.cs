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
    private readonly IRevisaoDasConcessoesRepository _revisoes;
    private readonly TimeProvider _clock;

    public LeitorDoContextoDaVersao(IPerfilRepository perfis, IRevisaoDasConcessoesRepository revisoes, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _revisoes = revisoes;
        _clock = clock;
    }

    /// <summary>Se o nome é de outro perfil, sem distinguir maiúsculas de minúsculas.</summary>
    public Task<bool> NomeEmUsoPorOutroAsync(string nome, Guid? perfilId, CancellationToken cancellationToken) =>
        _perfis.NomeExisteAsync(nome, perfilId, cancellationToken);

    public async Task<ContextoDaVersao> LerAsync(Guid perfilId, string nome, CancellationToken cancellationToken) =>
        new(
            await NomeEmUsoPorOutroAsync(nome, perfilId, cancellationToken).ConfigureAwait(false),
            await _revisoes.TemRevisaoAbertaAsync(perfilId, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false));
}
