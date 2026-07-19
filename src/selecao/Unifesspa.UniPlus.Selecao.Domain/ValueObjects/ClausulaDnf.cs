namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Conjunção (E) de <see cref="CondicaoDnf"/> — uma cláusula da forma normal
/// disjuntiva de <see cref="PredicadoDnf"/> (ADR-0111, Story #847). Uma
/// cláusula vazia nunca é um objeto construível: a factory rejeita a lista
/// vazia ou nula.
/// </summary>
public sealed record ClausulaDnf
{
    private ClausulaDnf(IReadOnlyList<CondicaoDnf> condicoes)
    {
        Condicoes = condicoes;
    }

    public IReadOnlyList<CondicaoDnf> Condicoes { get; }

    public static Result<ClausulaDnf> Criar(IReadOnlyList<CondicaoDnf>? condicoes)
    {
        if (condicoes is not { Count: > 0 })
        {
            return Result<ClausulaDnf>.Failure(new DomainError(
                "ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
        }

        return Result<ClausulaDnf>.Success(new ClausulaDnf([.. condicoes]));
    }

    /// <summary>
    /// Avalia a cláusula (E lógico ternário, fail-closed — Story #916) contra os fatos já
    /// resolvidos de um candidato: <see cref="Ternario.Falso"/> se qualquer condição avaliar
    /// <see cref="Ternario.Falso"/> (vence sobre indeterminação); senão
    /// <see cref="Ternario.Indeterminado"/> se qualquer uma avaliar indeterminada; senão
    /// <see cref="Ternario.Verdadeiro"/>.
    /// </summary>
    public Ternario Avaliar(IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        bool algumaIndeterminada = false;
        foreach (CondicaoDnf condicao in Condicoes)
        {
            Ternario resultado = AvaliarCondicao(condicao, fatosResolvidos);
            if (resultado == Ternario.Falso)
            {
                return Ternario.Falso;
            }

            if (resultado == Ternario.Indeterminado)
            {
                algumaIndeterminada = true;
            }
        }

        return algumaIndeterminada ? Ternario.Indeterminado : Ternario.Verdadeiro;
    }

    /// <summary>
    /// Avalia uma condição isolada. Um fato citado que não está resolvido — chave ausente de
    /// <paramref name="fatosResolvidos"/>, ou presente com <see cref="JsonValueKind.Null"/>/
    /// <see cref="JsonValueKind.Undefined"/> — nunca vira <see cref="Ternario.Falso"/>: é
    /// sempre <see cref="Ternario.Indeterminado"/> (Story #916, fail-closed). O mesmo vale
    /// para um valor resolvido de tipo incoerente com o operador (ex.: comparação numérica
    /// sobre um valor que não é número) — nunca lança, sempre <see cref="Ternario.Indeterminado"/>.
    /// <see cref="Operador.Diferente"/>/<see cref="Operador.NaoEm"/> são a negação lógica de
    /// <see cref="Operador.Igual"/>/<see cref="Operador.Em"/> — a negação só se aplica quando o
    /// fato está efetivamente resolvido e coerente; nunca inverte ausência/indeterminação para
    /// <see cref="Ternario.Verdadeiro"/>.
    /// </summary>
    private static Ternario AvaliarCondicao(CondicaoDnf condicao, IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        if (!fatosResolvidos.TryGetValue(condicao.Fato, out JsonElement valorCandidato)
            || valorCandidato.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Ternario.Indeterminado;
        }

        // Diferente/NaoEm são avaliados como a negação de Igual/Em: o resultado base é
        // calculado sobre o operador positivo correspondente, e só invertido quando
        // efetivamente resolvido (Ternario.Verdadeiro/Falso) — Indeterminado nunca inverte.
        Operador operadorBase = condicao.Operador switch
        {
            Operador.Igual => Operador.Igual,
            Operador.Em => Operador.Em,
            Operador.MaiorIgual => Operador.MaiorIgual,
            Operador.MenorIgual => Operador.MenorIgual,
            Operador.Diferente => Operador.Igual,
            Operador.NaoEm => Operador.Em,
            Operador.Nenhuma => throw new ArgumentOutOfRangeException(
                nameof(condicao), condicao.Operador, "Operador.Nenhuma é sentinela e não é avaliável."),
            _ => throw new ArgumentOutOfRangeException(nameof(condicao), condicao.Operador, "Operador desconhecido."),
        };

        Ternario resultadoBase = AvaliarOperadorBase(operadorBase, valorCandidato, condicao.Valor);
        bool negar = condicao.Operador is Operador.Diferente or Operador.NaoEm;
        if (!negar || resultadoBase == Ternario.Indeterminado)
        {
            return resultadoBase;
        }

        return resultadoBase == Ternario.Verdadeiro ? Ternario.Falso : Ternario.Verdadeiro;
    }

    /// <summary>
    /// Avalia os operadores positivos (<see cref="Operador.Igual"/>/<see cref="Operador.Em"/>/
    /// <see cref="Operador.MaiorIgual"/>/<see cref="Operador.MenorIgual"/>) contra um valor
    /// candidato já sabido resolvido (não nulo/ausente). <see cref="Operador.Diferente"/>/
    /// <see cref="Operador.NaoEm"/> nunca chegam aqui — <see cref="AvaliarCondicao"/> já os
    /// reduziu ao operador base correspondente.
    /// </summary>
    private static Ternario AvaliarOperadorBase(Operador operadorBase, JsonElement valorCandidato, JsonElement valorConfigurado)
    {
        // Story #554 (PR #896): fato multivalorado — o candidato resolve para um CONJUNTO
        // (array JSON), não um escalar. A forma do valor resolvido decide a semântica, sem
        // precisar de metadado extra aqui: IGUAL passa a significar pertinência do valor
        // configurado no conjunto do candidato; EM passa a significar interseção não vazia
        // entre o conjunto do candidato e a lista configurada (ADR-0111). Fatos escalares
        // (a maioria) continuam pelo ramo original, sem NENHUMA mudança de comportamento.
        if (valorCandidato.ValueKind == JsonValueKind.Array)
        {
            return operadorBase switch
            {
                Operador.Igual => BoolParaTernario(valorCandidato.EnumerateArray().Any(item => ValoresIguais(item, valorConfigurado))),
                Operador.Em => BoolParaTernario(valorConfigurado.EnumerateArray()
                    .Any(itemConfigurado => valorCandidato.EnumerateArray().Any(item => ValoresIguais(item, itemConfigurado)))),
                _ => Ternario.Indeterminado,
            };
        }

        return operadorBase switch
        {
            Operador.Igual => CompararIgualdade(valorCandidato, valorConfigurado),
            Operador.Em => valorCandidato.ValueKind == JsonValueKind.String
                ? BoolParaTernario(valorConfigurado.EnumerateArray().Any(item => ValoresIguais(valorCandidato, item)))
                : Ternario.Indeterminado,
            Operador.MaiorIgual => CompararNumerico(valorCandidato, valorConfigurado, static comparacao => comparacao >= 0),
            Operador.MenorIgual => CompararNumerico(valorCandidato, valorConfigurado, static comparacao => comparacao <= 0),
            _ => Ternario.Indeterminado,
        };
    }

    /// <summary>
    /// Igualdade escalar ternária: tipo (<see cref="JsonValueKind"/>) incoerente entre o valor
    /// resolvido e o configurado é <see cref="Ternario.Indeterminado"/> (Story #916) — nunca
    /// <see cref="Ternario.Falso"/> silencioso, que confundiria "resolvido diferente" com
    /// "resolvido de tipo que este predicado não sabe comparar". <see cref="JsonValueKind.True"/>/
    /// <see cref="JsonValueKind.False"/> são tratados como o MESMO tipo lógico (booleano) — são
    /// kinds distintos no <c>System.Text.Json</c>, mas isso é a forma normal de um booleano
    /// divergir, não uma incoerência de tipo.
    /// </summary>
    private static Ternario CompararIgualdade(JsonElement candidato, JsonElement configurado)
    {
        bool candidatoBooleano = candidato.ValueKind is JsonValueKind.True or JsonValueKind.False;
        bool configuradoBooleano = configurado.ValueKind is JsonValueKind.True or JsonValueKind.False;
        if (candidatoBooleano && configuradoBooleano)
        {
            return BoolParaTernario(candidato.GetBoolean() == configurado.GetBoolean());
        }

        if (candidato.ValueKind != configurado.ValueKind)
        {
            return Ternario.Indeterminado;
        }

        return candidato.ValueKind switch
        {
            JsonValueKind.String => BoolParaTernario(string.Equals(candidato.GetString(), configurado.GetString(), StringComparison.Ordinal)),
            JsonValueKind.Number => BoolParaTernario(candidato.GetDecimal() == configurado.GetDecimal()),
            _ => Ternario.Indeterminado,
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

    /// <summary>
    /// Comparação numérica ternária: <see cref="Ternario.Indeterminado"/> (nunca lança) quando
    /// o valor resolvido — ou, defensivamente, o configurado — não é um número JSON
    /// representável como <see cref="decimal"/> (Story #916; antes, <c>GetDecimal()</c> direto
    /// lançava <see cref="InvalidOperationException"/> para um valor não numérico).
    /// </summary>
    private static Ternario CompararNumerico(JsonElement candidato, JsonElement configurado, Func<int, bool> satisfaz)
    {
        if (candidato.ValueKind != JsonValueKind.Number || !candidato.TryGetDecimal(out decimal candidatoDecimal))
        {
            return Ternario.Indeterminado;
        }

        if (configurado.ValueKind != JsonValueKind.Number || !configurado.TryGetDecimal(out decimal configuradoDecimal))
        {
            return Ternario.Indeterminado;
        }

        return BoolParaTernario(satisfaz(candidatoDecimal.CompareTo(configuradoDecimal)));
    }

    private static Ternario BoolParaTernario(bool valor) => valor ? Ternario.Verdadeiro : Ternario.Falso;
}
