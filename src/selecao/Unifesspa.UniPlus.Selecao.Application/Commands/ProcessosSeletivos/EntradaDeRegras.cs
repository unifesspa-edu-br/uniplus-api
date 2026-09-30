namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma das regras do formulário vindas da escrita — predicado e obrigatoriedade —, a mesma
/// para item e termo. A semântica (fatos citáveis, operador e valor do domínio) é conferida depois,
/// contra o catálogo.
/// </summary>
internal static class EntradaDeRegras
{
    /// <summary>O predicado da entrada; nulo ou sem cláusula é ausência de condição.</summary>
    public static Result<PredicadoDnf?> Predicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? clausulas)
    {
        if (clausulas is null || clausulas.Count == 0)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            if (clausulas[c] is not { Count: > 0 } condicoes)
            {
                return Result<PredicadoDnf?>.Failure(new DomainError("ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
            }

            foreach (CondicaoPrecondicaoInput? condicao in condicoes)
            {
                if (condicao is null)
                {
                    return Result<PredicadoDnf?>.Failure(new DomainError(
                        CondicaoPrecondicaoFatoErrorCodes.ClausulaInvalida, "O predicado contém uma condição nula."));
                }

                Result<CondicaoDnf> criada = CondicaoDnf.Criar(condicao.Fato, OperadorCodigo.FromCodigo(condicao.Operador), condicao.Valor);
                if (criada.IsFailure)
                {
                    return Result<PredicadoDnf?>.Failure(criada.Error!);
                }

                linhas.Add((c, criada.Value!));
            }
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
    }

    /// <summary>
    /// A obrigatoriedade do token e do predicado: <c>SEMPRE</c> e <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele; qualquer outra combinação é <see langword="null"/>, e quem chama
    /// recusa com o código da sua entidade.
    /// </summary>
    public static Obrigatoriedade? Obrigatoriedade(string? token, PredicadoDnf? predicado) =>
        (PredicadoDnfJson.TipoDoToken(token), predicado) switch
        {
            (TipoObrigatoriedade.Sempre, null) => Regras.Formularios.Obrigatoriedade.Sempre,
            (TipoObrigatoriedade.Nunca, null) => Regras.Formularios.Obrigatoriedade.Nunca,
            (TipoObrigatoriedade.Quando, { } quando) => Regras.Formularios.Obrigatoriedade.Quando(quando),
            _ => null,
        };
}
