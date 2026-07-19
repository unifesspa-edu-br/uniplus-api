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
/// não é erro — mas <see cref="Avaliar"/> retorna <see cref="Ternario.Falso"/>
/// para qualquer candidato nesse caso: um predicado sem cláusula nunca casa
/// com ninguém (estado estrutural, não ausência de dado — não se confunde com
/// <see cref="Ternario.Indeterminado"/>).
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
    /// Avalia o predicado contra os fatos já resolvidos de um candidato: OU ternário
    /// (Story #916) sobre as cláusulas, cada uma um E ternário sobre as suas condições
    /// (<see cref="ClausulaDnf.Avaliar"/>). <see cref="Ternario.Verdadeiro"/> se qualquer
    /// cláusula for verdadeira; senão <see cref="Ternario.Indeterminado"/> se qualquer uma
    /// for indeterminada; senão <see cref="Ternario.Falso"/>. Um fato citado numa condição
    /// que não está presente em <paramref name="fatosResolvidos"/> (o processo não o
    /// coletou, ou o motor ainda não o derivou) — ou está presente com valor nulo/de tipo
    /// incoerente — nunca faz a condição avaliar como <see cref="Ternario.Falso"/>: é
    /// <see cref="Ternario.Indeterminado"/>, fail-closed — nunca lança, e o restante do
    /// predicado continua avaliável normalmente.
    /// </summary>
    public Ternario Avaliar(IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        bool algumaIndeterminada = false;
        foreach (ClausulaDnf clausula in Clausulas)
        {
            Ternario resultado = clausula.Avaliar(fatosResolvidos);
            if (resultado == Ternario.Verdadeiro)
            {
                return Ternario.Verdadeiro;
            }

            if (resultado == Ternario.Indeterminado)
            {
                algumaIndeterminada = true;
            }
        }

        return algumaIndeterminada ? Ternario.Indeterminado : Ternario.Falso;
    }
}
