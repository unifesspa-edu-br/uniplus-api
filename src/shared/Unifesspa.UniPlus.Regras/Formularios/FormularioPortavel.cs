namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras de um formulário na forma que atravessa o fio: o que o avaliador usa para decidir o que
/// aparece, o que é obrigatório, quais opções valem, o que a resposta viola e o que impede a inscrição
/// — etapas, itens, grupos repetíveis, termos, derivações e agregados —, mais a oferta de valores de
/// cada campo. É a forma que a API publica e que o interpretador do front lê (ADR-0139).
/// </summary>
/// <remarks>
/// <para>
/// A conversão vai e volta sem perda: <see cref="De"/> leva uma <see cref="DefinicaoFormulario"/> ao
/// portável, e <see cref="ParaDefinicao"/> traz o portável de volta. Para as mesmas respostas, as duas
/// definições dão a mesma avaliação; é isso que prova que nada que o avaliador usa fica fora do fio.
/// </para>
/// <para>
/// Os predicados seguem a forma da pré-condição: a lista externa é o OU de cláusulas, e cada cláusula é
/// o E de condições. Nulo é ausência de condição; a lista vazia é o predicado sem cláusula, que avalia
/// falso — e, na derivação, a regra-âncora, sempre ativa.
/// </para>
/// </remarks>
public sealed record FormularioPortavel(
    IReadOnlyList<EtapaPortavel> Etapas,
    IReadOnlyList<TermoPortavel> Termos,
    IReadOnlyList<DerivacaoPortavel> Derivacoes,
    IReadOnlyList<AgregadoPortavel> Agregados)
{
    /// <summary>
    /// O formulário portável de uma definição. A oferta de valores não está na definição: quem monta a
    /// definição a conhece e a passa aqui, por fato.
    /// </summary>
    public static FormularioPortavel De(
        DefinicaoFormulario definicao, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas = null)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados = ofertas ?? new Dictionary<string, IReadOnlySet<string>>();

        return new(
            [.. definicao.Etapas.Select(e => Etapa(e, ofertados))],
            [.. definicao.Termos.Select(Termo)],
            [.. definicao.Derivacoes.Select(Derivacao)],
            [.. definicao.Agregados.Select(Agregado)]);
    }

    /// <summary>
    /// A definição avaliável do portável. A forma que não forma definição — obrigatoriedade sem o
    /// predicado que ela pede, derivação sem regra, código repetido — é recusada com a causa, para
    /// que o conteúdo vindo de fora (um arquivo importado) nunca chegue ao avaliador pela metade.
    /// </summary>
    public Result<DefinicaoFormulario> ParaDefinicao()
    {
        if (CaminhoDoPrimeiroNulo() is { } nulo)
        {
            return Result<DefinicaoFormulario>.Failure(new DomainError(
                FormularioPortavelErrorCodes.EstruturaInvalida, $"O elemento '{nulo}' das regras é nulo."));
        }

        return ConversaoDoPortavel.Converter(this);
    }

    /// <summary>
    /// O caminho do primeiro elemento nulo dentro das regras, como <c>etapas[0].itens[2]</c>; nulo
    /// quando não há. O arquivo importado chega pelo fio, e a lista com elemento nulo não forma
    /// definição.
    /// </summary>
    private string? CaminhoDoPrimeiroNulo() =>
        Lista(Etapas, "etapas", static (etapa, caminho) =>
            Predicado(etapa.Exibicao, $"{caminho}.exibicao")
            ?? Lista(etapa.Itens, $"{caminho}.itens", NuloNoItem)
            ?? Lista(etapa.Grupos, $"{caminho}.grupos", static (grupo, doGrupo) =>
                Predicado(grupo.Exibicao, $"{doGrupo}.exibicao")
                ?? Predicado(grupo.PredicadoObrigatoriedade, $"{doGrupo}.predicadoObrigatoriedade")
                ?? Lista(grupo.Subitens, $"{doGrupo}.subitens", NuloNoItem)))
        ?? Lista(Termos, "termos", static (termo, caminho) =>
            Predicado(termo.Exibicao, $"{caminho}.exibicao") ?? Predicado(termo.PredicadoObrigatoriedade, $"{caminho}.predicadoObrigatoriedade"))
        ?? Lista(Derivacoes, "derivacoes", static (derivacao, caminho) =>
            Lista(derivacao.Regras, $"{caminho}.regras", static (regra, daRegra) => Predicado(regra.Quando, $"{daRegra}.quando")))
        ?? Lista(Agregados, "agregados", static (_, _) => null);

    private static string? NuloNoItem(ItemPortavel item, string caminho) =>
        Predicado(item.Exibicao, $"{caminho}.exibicao")
        ?? Predicado(item.PredicadoObrigatoriedade, $"{caminho}.predicadoObrigatoriedade")
        ?? Lista(item.Restricoes, $"{caminho}.restricoes", static (restricao, daRestricao) =>
            Lista(restricao.Entradas, $"{daRestricao}.entradas", static (entrada, daEntrada) =>
                Predicado(entrada.Quando, $"{daEntrada}.quando") ?? Lista(entrada.Valores, $"{daEntrada}.valores", static (_, _) => null))
            ?? Lista(restricao.Fatos, $"{daRestricao}.fatos", static (_, _) => null))
        ?? (item.Impedimento is { } impedimento ? Predicado(impedimento.Quando, $"{caminho}.impedimento.quando") : null)
        ?? Lista(item.Oferta, $"{caminho}.oferta", static (_, _) => null);

    private static string? Predicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado, string caminho) =>
        Lista(predicado, caminho, static (clausula, daClausula) => Lista(clausula, daClausula, static (_, _) => null));

    private static string? Lista<T>(IReadOnlyList<T>? lista, string caminho, Func<T, string, string?> dentro)
        where T : class
    {
        for (int i = 0; i < (lista?.Count ?? 0); i++)
        {
            string doElemento = $"{caminho}[{i}]";
            if (lista![i] is not { } elemento)
            {
                return doElemento;
            }

            if (dentro(elemento, doElemento) is { } nulo)
            {
                return nulo;
            }
        }

        return null;
    }

    /// <summary>
    /// A oferta de valores de cada campo que tem oferta, por fato — a dos itens e a dos subitens. Um fato
    /// repetido não forma definição (<see cref="ParaDefinicao"/> o recusa); aqui fica a primeira oferta.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas()
    {
        Dictionary<string, IReadOnlySet<string>> ofertas = new(StringComparer.Ordinal);
        foreach (ItemPortavel item in (Etapas ?? [])
            .SelectMany(static e => (e.Itens ?? []).Concat((e.Grupos ?? []).SelectMany(static g => g.Subitens ?? [])))
            .Where(static i => i.Oferta is not null))
        {
            ofertas.TryAdd(item.FatoCodigo, new HashSet<string>(item.Oferta!, StringComparer.Ordinal));
        }

        return ofertas;
    }

    /// <summary>
    /// A avaliação do portável: a resposta fora da oferta é descartada antes, como na pré-visualização e
    /// na execução, e o resto é o mesmo avaliador.
    /// </summary>
    public Result<AvaliacaoFormulario> Avaliar(EntradaAvaliacaoFormulario entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        Result<DefinicaoFormulario> definicao = ParaDefinicao();
        if (definicao.IsFailure)
        {
            return Result<AvaliacaoFormulario>.Failure(definicao.Error!);
        }

        return Result<AvaliacaoFormulario>.Success(AvaliadorFormulario.Avaliar(definicao.Value!, DentroDaOferta(entrada)));
    }

    private EntradaAvaliacaoFormulario DentroDaOferta(EntradaAvaliacaoFormulario entrada)
    {
        IReadOnlyDictionary<string, IReadOnlySet<string>> ofertas = Ofertas();
        return entrada with
        {
            Respostas = RespostaDeCampo.DentroDaOferta(entrada.Respostas, ofertas),
            RespostasDosGrupos = entrada.RespostasDosGrupos?.ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaRespondida>)[.. g.Value.Select(o => o with { Respostas = RespostaDeCampo.DentroDaOferta(o.Respostas, ofertas) })],
                StringComparer.Ordinal),
        };
    }

    private static EtapaPortavel Etapa(DefinicaoEtapa etapa, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            etapa.Codigo,
            ParaFio(etapa.Exibicao),
            [.. etapa.Itens.Select(i => Item(i, ofertados))],
            [.. etapa.Grupos.Select(g => Grupo(g, ofertados))]);

    private static GrupoPortavel Grupo(DefinicaoGrupo grupo, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            grupo.Codigo,
            ParaFio(grupo.Exibicao),
            EntradaDeRegras.ParaEntrada(grupo.Obrigatoriedade),
            ParaFio(grupo.Obrigatoriedade.Predicado),
            grupo.Minimo,
            grupo.Maximo,
            grupo.IncluiCandidato,
            [.. grupo.Subitens.Select(s => Item(s, ofertados))]);

    private static TermoPortavel Termo(DefinicaoTermo termo) =>
        new(
            termo.Codigo,
            ParaFio(termo.Exibicao),
            EntradaDeRegras.ParaEntrada(termo.Obrigatoriedade),
            ParaFio(termo.Obrigatoriedade.Predicado));

    private static DerivacaoPortavel Derivacao(DerivacaoDoFato derivacao) =>
        new(
            derivacao.CodigoFato,
            derivacao.Booleano,
            [.. derivacao.Regras.Select(static r => new RegraDerivacaoPortavel(ParaFio(r.Quando)!, r.Contribui))]);

    private static AgregadoPortavel Agregado(DefinicaoAgregado agregado) =>
        new(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro, AgregadoDeGrupo.ParaToken(agregado.Operacao));

    private static ItemPortavel Item(DefinicaoItem item, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            item.FatoCodigo,
            ParaFio(item.Exibicao),
            EntradaDeRegras.ParaEntrada(item.Obrigatoriedade),
            ParaFio(item.Obrigatoriedade.Predicado),
            [.. item.Restricoes.Select(EntradaDeRegras.ParaEntrada)],
            EntradaDeRegras.ParaEntrada(item.Impedimento),
            ofertados.TryGetValue(item.FatoCodigo, out IReadOnlySet<string>? oferta) ? [.. oferta.Order(StringComparer.Ordinal)] : null,
            item.Formato);

    /// <summary>O predicado na forma do fio: nulo sem condição, a lista vazia sem cláusula.</summary>
    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? ParaFio(PredicadoDnf? predicado) =>
        EntradaDeRegras.ParaEntrada(predicado);
}
