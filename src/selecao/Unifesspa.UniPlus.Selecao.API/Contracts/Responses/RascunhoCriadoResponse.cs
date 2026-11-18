namespace Unifesspa.UniPlus.Selecao.API.Contracts.Responses;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>O rascunho criado: o identificador (que será o do processo), a revisão e as causas pelas quais o documento ainda não se publica.</summary>
/// <param name="Id">O identificador do rascunho.</param>
/// <param name="Revisao">A revisão gravada, sempre 1 na criação.</param>
/// <param name="Diagnosticos">Os itens do diagnóstico do documento, cada um no ponteiro JSON do campo.</param>
public sealed record RascunhoCriadoResponse(Guid Id, int Revisao, IReadOnlyList<ItemDeErroDeCampo> Diagnosticos)
{
    internal static RascunhoCriadoResponse De(RascunhoSalvoDto salvo, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(salvo);

        return new RascunhoCriadoResponse(salvo.Id, salvo.Revisao, [.. salvo.Diagnosticos.Select(erro => ItemDeErroDeCampo.De(erro, mapper))]);
    }
}
