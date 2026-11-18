namespace Unifesspa.UniPlus.Selecao.API.Contracts.Responses;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>O rascunho depois de substituído: a revisão que ficou e as causas pelas quais o documento ainda não se publica.</summary>
/// <param name="Revisao">A revisão gravada, uma a mais que a substituída.</param>
/// <param name="Diagnosticos">Os itens do diagnóstico do documento, cada um no ponteiro JSON do campo.</param>
public sealed record RascunhoSubstituidoResponse(int Revisao, IReadOnlyList<ItemDeErroDeCampo> Diagnosticos)
{
    internal static RascunhoSubstituidoResponse De(RascunhoSalvoDto salvo, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(salvo);

        return new RascunhoSubstituidoResponse(salvo.Revisao, [.. salvo.Diagnosticos.Select(erro => ItemDeErroDeCampo.De(erro, mapper))]);
    }
}
