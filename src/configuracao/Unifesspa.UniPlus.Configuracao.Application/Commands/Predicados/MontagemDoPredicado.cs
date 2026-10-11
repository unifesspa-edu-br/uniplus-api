namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
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
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> clausulas = quando ?? [];
        for (int clausula = 0; clausula < clausulas.Count; clausula++)
        {
            if (clausulas[clausula] is not { Count: > 0 } condicoes)
            {
                return Result<PredicadoDnf>.Failure(new DomainError(
                    "ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
            }

            foreach (CondicaoDoPredicadoInput? condicao in condicoes)
            {
                if (condicao is null)
                {
                    return Result<PredicadoDnf>.Failure(condicaoNula);
                }

                Result<CondicaoDnf> criada = CondicaoDnf.Criar(condicao.Fato, OperadorCodigo.FromCodigo(condicao.Operador), condicao.Valor);
                if (criada.IsFailure)
                {
                    return Result<PredicadoDnf>.Failure(criada.Error!);
                }

                linhas.Add((clausula, criada.Value!));
            }
        }

        return PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
    }
}
