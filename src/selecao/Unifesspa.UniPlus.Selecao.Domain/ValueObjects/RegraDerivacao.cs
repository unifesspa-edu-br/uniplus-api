namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma regra da derivação de um fato: quando o predicado <see cref="Quando"/> é verdadeiro, a regra
/// contribui o código <see cref="Contribui"/> para o conjunto derivado (Story #927).
/// </summary>
/// <remarks>
/// <para>
/// O <see cref="Quando"/> é um predicado em forma normal disjuntiva no vocabulário fechado de fatos —
/// sem referência a outra regra, sem construção fora do schema. A regra <b>incondicional</b> (âncora)
/// tem <see cref="Quando"/> com <b>DNF vazio</b>: sem cláusula alguma, resolve verdadeiro sempre.
/// </para>
/// <para>
/// Aqui está a diferença deliberada em relação a <see cref="PredicadoDnf.Avaliar"/>: para os
/// consumidores genéricos, um predicado vazio significa "nunca casa" e avalia falso. A regra-âncora
/// tem semântica própria — vazio é "sempre" —, então <see cref="AvaliarQuando"/> trata o caso vazio
/// aqui, sem alterar o avaliador genérico.
/// </para>
/// </remarks>
public sealed record RegraDerivacao
{
    private RegraDerivacao(PredicadoDnf quando, string contribui)
    {
        Quando = quando;
        Contribui = contribui;
    }

    /// <summary>O predicado que ativa a regra. DNF vazio = incondicional (âncora).</summary>
    public PredicadoDnf Quando { get; }

    /// <summary>O código de valor do domínio do fato que a regra contribui quando ativa.</summary>
    public string Contribui { get; }

    /// <summary>Indica se a regra é incondicional — o <see cref="Quando"/> não tem cláusula alguma.</summary>
    public bool EhAncora => Quando.Clausulas.Count == 0;

    public static Result<RegraDerivacao> Criar(PredicadoDnf quando, string contribui)
    {
        ArgumentNullException.ThrowIfNull(quando);

        if (string.IsNullOrWhiteSpace(contribui))
        {
            return Result<RegraDerivacao>.Failure(new DomainError(
                RegraDerivacaoErrorCodes.ContribuiObrigatorio,
                "Uma regra de derivação precisa contribuir um código de valor do domínio do fato."));
        }

        return Result<RegraDerivacao>.Success(new RegraDerivacao(quando, contribui.Trim()));
    }

    /// <summary>
    /// Avalia se a regra está ativa contra os fatos resolvidos. A âncora (DNF vazio) é sempre
    /// verdadeira; as demais delegam ao avaliador do predicado.
    /// </summary>
    public Ternario AvaliarQuando(IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        return EhAncora ? Ternario.Verdadeiro : Quando.Avaliar(fatosResolvidos);
    }

    /// <summary>Os códigos de fato citados no predicado da regra, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. Quando.Clausulas
            .SelectMany(static c => c.Condicoes)
            .Select(static cond => cond.Fato)
            .Distinct(StringComparer.Ordinal)];
}

/// <summary>Códigos de erro de <see cref="RegraDerivacao"/>.</summary>
public static class RegraDerivacaoErrorCodes
{
    public const string ContribuiObrigatorio = "RegraDerivacao.ContribuiObrigatorio";
}
