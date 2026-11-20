namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta o predicado da entrada: o OU de cláusulas, cada uma o E de condições. A lista nula ou vazia
/// é o predicado sem cláusula; a cláusula vazia e a condição nula são recusadas.
/// </summary>
internal static class MontagemDoPredicado
{
    public static Result<PredicadoDnf> Montar(
        IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? quando,
        DomainError condicaoNula)
    {
        Result<List<(int Clausula, CondicaoDnf Condicao)>> linhas = EntradaDeRegras.Condicoes(Entrada(quando));
        if (linhas.IsFailure)
        {
            return Result<PredicadoDnf>.Failure(linhas.Errors[0].Error.Code == PredicadoDnfErrorCodes.CondicaoNula ? condicaoNula : linhas.Error!);
        }

        return PredicadoDnf.CriarDeCondicoesAgrupadas(linhas.Value!);
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput?>?> Entrada(
        IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? quando) =>
        [.. (quando ?? []).Select(static clausula => (IReadOnlyList<CondicaoPrecondicaoInput?>?)
            [.. (clausula ?? []).Select(static condicao => condicao is null ? null : new CondicaoPrecondicaoInput(condicao.Fato, condicao.Operador, condicao.Valor))])];
}
