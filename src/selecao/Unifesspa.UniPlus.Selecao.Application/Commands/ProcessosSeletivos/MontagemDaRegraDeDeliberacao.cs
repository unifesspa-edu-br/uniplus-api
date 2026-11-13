namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta a regra de deliberação declarada para uma banca, acumulando as violações campo a campo
/// (ADR-0125) sob o caminho da banca no payload.
/// </summary>
internal static class MontagemDaRegraDeDeliberacao
{
    /// <summary>Regra ausente é rascunho: a publicação a exige, e a montagem a aceita.</summary>
    public static Result<RegraDeDeliberacao?> Montar(RegraDeDeliberacaoInput? declarada, string campoDaBanca)
    {
        if (declarada is null)
        {
            return Result<RegraDeDeliberacao?>.Success(null);
        }

        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(declarada.ParaDeclaracao());
        return regra.IsSuccess
            ? Result<RegraDeDeliberacao?>.Success(regra.Value)
            : Result<RegraDeDeliberacao?>.ValidationFailure(
                [.. regra.Errors.Select(erro => erro with { Field = $"{campoDaBanca}.regraDeDeliberacao.{erro.Field}" })]);
    }
}
