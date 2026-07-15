namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Predicado sobre o candidato na forma normal disjuntiva — um OU de
/// <see cref="ClausulaDnf"/>, cada uma um E de <see cref="CondicaoDnf"/>
/// (ADR-0111, Story #847). Compartilhado por <c>DESEMPATE-PREDICADO-FATO</c>
/// (via <see cref="ArgsDesempatePredicadoFato"/>) e pelos futuros consumidores
/// #554 (<c>CondicaoGatilho</c>) e #559 (<c>CampoFormulario.CondicaoExibicao</c>).
/// </summary>
/// <remarks>
/// Zero cláusulas é um estado <b>estruturalmente válido</b> de construção —
/// não é erro — mas <see cref="Avaliar"/> retorna <see langword="false"/>
/// para qualquer candidato nesse caso: um predicado sem cláusula nunca casa
/// com ninguém.
/// </remarks>
public sealed record PredicadoDnf
{
    private PredicadoDnf(IReadOnlyList<ClausulaDnf> clausulas)
    {
        Clausulas = clausulas;
    }

    public IReadOnlyList<ClausulaDnf> Clausulas { get; }

    /// <summary>
    /// Agrupa condições por ordinal de cláusula (o formato bruto que
    /// <c>CondicaoGatilho</c> — linhas relacionais com coluna <c>clausula</c>
    /// — produz), ordenando as cláusulas resultantes por ordinal ascendente
    /// para determinismo. Ordinais ausentes não geram cláusulas vazias — a
    /// avaliação percorre só os ordinais efetivamente presentes. Uma
    /// sequência de entrada vazia produz um <see cref="PredicadoDnf"/> com
    /// zero cláusulas (sucesso, não falha).
    /// </summary>
    public static Result<PredicadoDnf> CriarDeCondicoesAgrupadas(
        IReadOnlyList<(int Clausula, CondicaoDnf Condicao)> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);

        List<ClausulaDnf> clausulas = [];
        foreach (IGrouping<int, (int Clausula, CondicaoDnf Condicao)> grupo in linhas
            .GroupBy(static linha => linha.Clausula)
            .OrderBy(static grupo => grupo.Key))
        {
            Result<ClausulaDnf> clausulaResult = ClausulaDnf.Criar([.. grupo.Select(static linha => linha.Condicao)]);
            if (clausulaResult.IsFailure)
            {
                return Result<PredicadoDnf>.Failure(clausulaResult.Error!);
            }

            clausulas.Add(clausulaResult.Value!);
        }

        return Result<PredicadoDnf>.Success(new PredicadoDnf(clausulas));
    }

    /// <summary>
    /// Avalia o predicado contra os fatos já resolvidos de um candidato:
    /// OU sobre as cláusulas, E sobre as condições de cada cláusula. Um fato
    /// citado numa condição que não está presente em
    /// <paramref name="fatosResolvidos"/> (o processo não o coletou, ou o
    /// motor ainda não o derivou) faz a condição avaliar como
    /// <see langword="false"/> — conservador, nunca lança e nunca trata como
    /// satisfeita. O restante do predicado continua avaliável normalmente.
    /// </summary>
    public bool Avaliar(IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        return Clausulas.Any(clausula => clausula.Condicoes.All(condicao => AvaliarCondicao(condicao, fatosResolvidos)));
    }

    private static bool AvaliarCondicao(CondicaoDnf condicao, IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        if (!fatosResolvidos.TryGetValue(condicao.Fato, out JsonElement valorCandidato))
        {
            return false;
        }

        return condicao.Operador switch
        {
            Operador.Igual => ValoresIguais(valorCandidato, condicao.Valor),
            Operador.Em => condicao.Valor.EnumerateArray().Any(item => ValoresIguais(valorCandidato, item)),
            Operador.MaiorIgual => CompararNumerico(valorCandidato, condicao.Valor) >= 0,
            Operador.MenorIgual => CompararNumerico(valorCandidato, condicao.Valor) <= 0,
            _ => false,
        };
    }

    private static bool ValoresIguais(JsonElement candidato, JsonElement configurado)
    {
        if (candidato.ValueKind != configurado.ValueKind)
        {
            return false;
        }

        return candidato.ValueKind switch
        {
            JsonValueKind.String => string.Equals(candidato.GetString(), configurado.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => candidato.GetDecimal() == configurado.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => candidato.GetBoolean() == configurado.GetBoolean(),
            _ => false,
        };
    }

    private static int CompararNumerico(JsonElement candidato, JsonElement configurado) =>
        candidato.GetDecimal().CompareTo(configurado.GetDecimal());
}
