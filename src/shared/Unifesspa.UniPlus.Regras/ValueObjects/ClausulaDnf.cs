namespace Unifesspa.UniPlus.Regras.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Conjunção (E) de <see cref="CondicaoDnf"/> — uma cláusula da forma normal
/// disjuntiva de <see cref="PredicadoDnf"/> (ADR-0111). Uma
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
    /// Avalia a cláusula (E lógico ternário, fail-closed) contra os fatos já
    /// resolvidos de um candidato: <see cref="Ternario.Falso"/> se qualquer condição avaliar
    /// <see cref="Ternario.Falso"/> (vence sobre indeterminação); senão
    /// <see cref="Ternario.Indeterminado"/> se qualquer uma avaliar indeterminada; senão
    /// <see cref="Ternario.Verdadeiro"/>.
    /// </summary>
    /// <remarks>
    /// O átomo tem quatro estados, mas a cláusula continua ternária:
    /// <see cref="EstadoAtomo.NaoAplicavel"/> <b>colapsa como <see cref="Ternario.Falso"/>
    /// resolvido</b> aqui. É o colapso que dá a resposta correta ao consumidor: um gatilho
    /// que cita um fato inaplicável resolve falso — a exigência não se aplica, em definitivo —
    /// em vez de ficar pendente esperando um valor que nunca virá.
    /// </remarks>
    public Ternario Avaliar(IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        bool algumaIndeterminada = false;
        foreach (CondicaoDnf condicao in Condicoes)
        {
            EstadoAtomo resultado = AvaliarCondicao(condicao, fatosResolvidos);
            if (resultado is EstadoAtomo.Falso or EstadoAtomo.NaoAplicavel)
            {
                return Ternario.Falso;
            }

            algumaIndeterminada |= resultado == EstadoAtomo.Indeterminado;
        }
        return algumaIndeterminada ? Ternario.Indeterminado : Ternario.Verdadeiro;
    }

    /// <summary>
    /// Avalia um átomo isolado, propagando o estado do fato para <b>qualquer</b> operador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fato <see cref="EstadoFato.NaoAplicavel"/> dá átomo <see cref="EstadoAtomo.NaoAplicavel"/>,
    /// e fato ausente do dicionário ou <see cref="EstadoFato.Indeterminado"/> dá átomo
    /// <see cref="EstadoAtomo.Indeterminado"/> — em ambos os casos para todo operador, inclusive
    /// <see cref="Operador.Diferente"/>/<see cref="Operador.NenhumDe"/>. A negação inverte um
    /// <b>valor</b>; sobre a inaplicabilidade ou sobre a ausência de informação não há o que
    /// inverter, e inverter mesmo assim faria o predicado ser satisfeito justamente pelos
    /// candidatos sobre os quais nada se sabe.
    /// </para>
    /// <para>
    /// Um valor resolvido de tipo incoerente com o operador (ex.: comparação numérica sobre um
    /// valor que não é número) também é <see cref="EstadoAtomo.Indeterminado"/> — nunca lança, e
    /// nunca vira falso silencioso, que confundiria "resolvido diferente" com "resolvido em
    /// forma que este predicado não sabe comparar".
    /// </para>
    /// </remarks>
    private static EstadoAtomo AvaliarCondicao(CondicaoDnf condicao, IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        // O nulo é tratado como ausência em vez de estourar: um dicionário montado por um
        // adaptador descuidado não deve derrubar a avaliação inteira, e "não sei" é a leitura
        // conservadora de uma entrada sem conteúdo — nunca "não se aplica", que dispensaria a
        // exigência por um defeito de montagem.
        FatoResolvido? fato = fatosResolvidos.GetValueOrDefault(condicao.Fato);

        return fato is { Estado: EstadoFato.Resolvido, Valor: { } valorCandidato }
            ? AvaliarValor(condicao, valorCandidato)
            : EstadoSemValorParaComparar(fato);
    }

    private static EstadoAtomo EstadoSemValorParaComparar(FatoResolvido? fato) => fato?.Estado switch
    {
        EstadoFato.NaoAplicavel => EstadoAtomo.NaoAplicavel,

        // Opcional em branco com a etapa concluída (UNI-REQ-0074): não há valor a comparar nem a
        // negar, então a condição é falsa para todo operador, inclusive DIFERENTE e NENHUM_DE — quem
        // não respondeu não satisfaz "diferente de X" por omissão.
        EstadoFato.NaoInformado => EstadoAtomo.Falso,
        _ => EstadoAtomo.Indeterminado,
    };

    // Diferente/NenhumDe são avaliados como a negação de Igual/AlgumDe: o resultado base é
    // calculado sobre o operador positivo correspondente, e só invertido quando
    // efetivamente resolvido (Ternario.Verdadeiro/Falso) — Indeterminado nunca inverte.
    private static EstadoAtomo AvaliarValor(CondicaoDnf condicao, JsonElement valorCandidato)
    {
        Ternario resultadoBase = AvaliarOperadorBase(OperadorPositivo(condicao.Operador), valorCandidato, condicao.Valor);

        return condicao.Operador is Operador.Diferente or Operador.NenhumDe
            ? NegacaoDoAtomo(resultadoBase)
            : TernarioParaAtomo(resultadoBase);
    }

    private static Operador OperadorPositivo(Operador operador) => operador switch
    {
        Operador.Diferente => Operador.Igual,
        Operador.NenhumDe => Operador.AlgumDe,
        Operador.Igual or Operador.AlgumDe or Operador.MaiorIgual or Operador.MenorIgual => operador,
        _ => throw new ArgumentOutOfRangeException(nameof(operador), operador, "Operador desconhecido."),
    };

    private static EstadoAtomo NegacaoDoAtomo(Ternario resultadoBase) => resultadoBase switch
    {
        Ternario.Verdadeiro => EstadoAtomo.Falso,
        Ternario.Falso => EstadoAtomo.Verdadeiro,
        _ => EstadoAtomo.Indeterminado,
    };

    /// <summary>
    /// Converte o resultado da comparação de valores — que é ternária, porque
    /// <see cref="EstadoAtomo.NaoAplicavel"/> já foi decidido antes de comparar valor algum —
    /// para o estado do átomo.
    /// </summary>
    private static EstadoAtomo TernarioParaAtomo(Ternario resultado) => resultado switch
    {
        Ternario.Verdadeiro => EstadoAtomo.Verdadeiro,
        Ternario.Falso => EstadoAtomo.Falso,
        Ternario.Indeterminado => EstadoAtomo.Indeterminado,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "Resultado ternário desconhecido."),
    };

    /// <summary>
    /// Avalia os operadores positivos (<see cref="Operador.Igual"/>/<see cref="Operador.AlgumDe"/>/
    /// <see cref="Operador.MaiorIgual"/>/<see cref="Operador.MenorIgual"/>) contra um valor
    /// candidato já sabido resolvido (não nulo/ausente). <see cref="Operador.Diferente"/>/
    /// <see cref="Operador.NenhumDe"/> nunca chegam aqui — <see cref="AvaliarCondicao"/> já os
    /// reduziu ao operador base correspondente.
    /// </summary>
    private static Ternario AvaliarOperadorBase(Operador operadorBase, JsonElement valorCandidato, JsonElement valorConfigurado) =>
        valorCandidato.ValueKind == JsonValueKind.Array
            ? AvaliarSobreConjunto(operadorBase, valorCandidato, valorConfigurado)
            : AvaliarSobreEscalar(operadorBase, valorCandidato, valorConfigurado);

    /// <summary>
    /// Fato multivalorado: o candidato resolve para um CONJUNTO (array JSON), não um escalar. A forma do valor
    /// resolvido decide a semântica, sem metadado extra: IGUAL passa a significar pertinência do valor
    /// configurado no conjunto do candidato; ALGUM_DE, interseção não vazia entre o conjunto do candidato e a lista
    /// configurada (ADR-0111).
    /// </summary>
    private static Ternario AvaliarSobreConjunto(Operador operadorBase, JsonElement conjuntoCandidato, JsonElement valorConfigurado) =>
        operadorBase switch
        {
            Operador.Igual => BoolParaTernario(conjuntoCandidato.EnumerateArray().Any(item => ValoresIguais(item, valorConfigurado))),
            Operador.AlgumDe => BoolParaTernario(valorConfigurado.EnumerateArray()
                .Any(itemConfigurado => conjuntoCandidato.EnumerateArray().Any(item => ValoresIguais(item, itemConfigurado)))),
            _ => Ternario.Indeterminado,
        };

    private static Ternario AvaliarSobreEscalar(Operador operadorBase, JsonElement valorCandidato, JsonElement valorConfigurado) =>
        operadorBase switch
        {
            Operador.Igual => CompararIgualdade(valorCandidato, valorConfigurado),
            Operador.AlgumDe => valorCandidato.ValueKind == JsonValueKind.String
                ? BoolParaTernario(valorConfigurado.EnumerateArray().Any(item => ValoresIguais(valorCandidato, item)))
                : Ternario.Indeterminado,
            Operador.MaiorIgual => CompararNumerico(valorCandidato, valorConfigurado, static comparacao => comparacao >= 0),
            Operador.MenorIgual => CompararNumerico(valorCandidato, valorConfigurado, static comparacao => comparacao <= 0),
            _ => Ternario.Indeterminado,
        };

    /// <summary>
    /// Igualdade escalar ternária: tipo (<see cref="JsonValueKind"/>) incoerente entre o valor
    /// resolvido e o configurado é <see cref="Ternario.Indeterminado"/> — nunca
    /// <see cref="Ternario.Falso"/> silencioso, que confundiria "resolvido diferente" com
    /// "resolvido de tipo que este predicado não sabe comparar". <see cref="JsonValueKind.True"/>/
    /// <see cref="JsonValueKind.False"/> são tratados como o MESMO tipo lógico (booleano) — são
    /// kinds distintos no <c>System.Text.Json</c>, mas isso é a forma normal de um booleano
    /// divergir, não uma incoerência de tipo.
    /// </summary>
    public static Ternario CompararIgualdade(JsonElement candidato, JsonElement configurado)
    {
        if (EhBooleano(candidato) && EhBooleano(configurado))
        {
            return BoolParaTernario(candidato.GetBoolean() == configurado.GetBoolean());
        }

        return candidato.ValueKind != configurado.ValueKind
            ? Ternario.Indeterminado
            : IgualdadeDoMesmoTipo(candidato, configurado);
    }

    private static bool EhBooleano(JsonElement valor) => valor.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static Ternario IgualdadeDoMesmoTipo(JsonElement candidato, JsonElement configurado) => candidato.ValueKind switch
    {
        JsonValueKind.String => BoolParaTernario(string.Equals(candidato.GetString(), configurado.GetString(), StringComparison.Ordinal)),
        JsonValueKind.Number => BoolParaTernario(candidato.GetDecimal() == configurado.GetDecimal()),
        _ => Ternario.Indeterminado,
    };

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
    /// representável como <see cref="decimal"/> (um <c>GetDecimal()</c> direto
    /// lançaria <see cref="InvalidOperationException"/> para um valor não numérico).
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
