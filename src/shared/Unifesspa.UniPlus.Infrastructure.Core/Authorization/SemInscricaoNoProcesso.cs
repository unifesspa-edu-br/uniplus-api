namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Porta das inscrições para o host que não guarda inscrições: ninguém está inscrito, então ninguém fica impedido.
/// </summary>
internal sealed class SemInscricaoNoProcesso : IInscricaoDoSujeitoNoProcesso
{
    public Task<bool> TemInscricaoEnviadaAsync(string emissor, string subject, Guid processoId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
