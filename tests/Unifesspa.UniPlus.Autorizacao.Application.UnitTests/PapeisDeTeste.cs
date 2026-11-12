namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A trava e a conferência dos papéis incompatíveis nos testes: anota cada pessoa travada, na ordem recebida, e responde
/// que só as pessoas dadas têm papel incompatível.
/// </summary>
internal sealed class PapeisDeTeste(List<string> travas, params Identidade[] comPapelIncompativel) : IIncompatibilidadeDosPapeis
{
    public PapeisDeTeste()
        : this([])
    {
    }

    public List<string> Travas => travas;

    public Task TravarAsync(IEnumerable<Identidade> pessoas, CancellationToken cancellationToken = default)
    {
        travas.AddRange(pessoas.Select(static p => "pessoa:" + p.Sub));
        return Task.CompletedTask;
    }

    public Task<bool> TemPapelIncompativelAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        Task.FromResult(comPapelIncompativel.Contains(pessoa));
}
