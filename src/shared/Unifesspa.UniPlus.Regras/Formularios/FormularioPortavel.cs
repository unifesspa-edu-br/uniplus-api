namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras de um formulário na forma que atravessa o fio: o que o avaliador usa para decidir o que
/// aparece, o que é obrigatório, quais opções valem, o que a resposta viola e o que impede a inscrição
/// — páginas, itens, grupos repetíveis, termos, derivações e agregados —, mais a oferta de valores de
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
    IReadOnlyList<PaginaPortavel> Paginas,
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
            [.. definicao.Paginas.Select(e => Pagina(e, ofertados))],
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
        IReadOnlyList<FieldError> forma = FormaDoPortavel.Recusas(this);
        return forma.Count > 0 ? Result<DefinicaoFormulario>.ValidationFailure(forma) : ConversaoDoPortavel.Converter(this);
    }

    /// <summary>
    /// A oferta de valores de cada campo que tem oferta, por fato — a dos itens e a dos subitens. Um fato
    /// repetido não forma definição (<see cref="ParaDefinicao"/> o recusa); aqui fica a primeira oferta.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas()
    {
        Dictionary<string, IReadOnlySet<string>> ofertas = new(StringComparer.Ordinal);
        foreach (ItemPortavel item in (Paginas ?? [])
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
            return Result<AvaliacaoFormulario>.ValidationFailure(definicao.Errors);
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

    private static PaginaPortavel Pagina(DefinicaoPagina pagina, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            pagina.Codigo,
            pagina.Exibicao?.ToDto(),
            [.. pagina.Itens.Select(i => Item(i, ofertados))],
            [.. pagina.Grupos.Select(g => Grupo(g, ofertados))]);

    private static GrupoPortavel Grupo(DefinicaoGrupo grupo, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            grupo.Codigo,
            new OcorrenciasDoGrupo(grupo.Minimo, grupo.Maximo, grupo.IncluiCandidato).ToDto(),
            new ExibicaoEObrigatoriedade(grupo.Exibicao, grupo.Obrigatoriedade).ToDto(),
            [.. grupo.Subitens.Select(s => Item(s, ofertados))]);

    private static TermoPortavel Termo(DefinicaoTermo termo) =>
        new(termo.Codigo, new ExibicaoEObrigatoriedade(termo.Exibicao, termo.Obrigatoriedade).ToDto());

    private static DerivacaoPortavel Derivacao(DerivacaoDoFato derivacao) =>
        new(
            derivacao.CodigoFato,
            derivacao.Booleano,
            [.. derivacao.Regras.Select(static r => new RegraDerivacaoPortavel(r.Quando.ToDto(), r.Contribui))]);

    private static AgregadoPortavel Agregado(DefinicaoAgregado agregado) =>
        new(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro, agregado.Operacao.Codigo());

    private static ItemPortavel Item(DefinicaoItem item, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            item.FatoCodigo,
            new RegrasDoCampo(item.Exibicao, item.Obrigatoriedade, item.Restricoes, item.Impedimento).ToDto(),
            ofertados.TryGetValue(item.FatoCodigo, out IReadOnlySet<string>? oferta) ? [.. oferta.Order(StringComparer.Ordinal)] : null,
            item.Formato);
}
