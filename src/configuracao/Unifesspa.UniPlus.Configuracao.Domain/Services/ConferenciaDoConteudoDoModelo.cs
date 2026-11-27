namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As regras do conteúdo do modelo de formulário, as mesmas do formulário do processo, aplicadas por
/// <c>Unifesspa.UniPlus.Regras</c>. As recusas acumulam (ADR-0125) na ordem das regras: o cabeçalho, a forma
/// de cada página, item, grupo e termo, o teto de itens e os pressupostos; a estrutura pela declaração e o
/// grafo de coleta só são conferidos sobre partes bem formadas e sobre o conteúdo inteiro, porque dependem
/// de todas as partes.
/// </summary>
public static class ConferenciaDoConteudoDoModelo
{
    /// <summary>O caminho do conteúdo no corpo da escrita, onde as recusas dele se apontam.</summary>
    public const string CaminhoDoConteudo = "conteudo";

    /// <summary>As recusas do conteúdo apontam o campo dentro dele, no caminho da escrita.</summary>
    public static IEnumerable<FieldError> NoConteudo(IEnumerable<FieldError> erros) =>
        erros.Select(static e => e.NoCaminhoDe(CaminhoDoConteudo));

    /// <summary>
    /// Confere o conteúdo na forma gravada (<see cref="ConteudoDoModelo.NaFormaGravada"/>), com as recusas
    /// no caminho do conteúdo. As <paramref name="derivacoes"/> são as dependências de cada derivado por
    /// regra do catálogo, para o grafo. Com <paramref name="conteudoPorInteiro"/> falso, parte do que foi enviado
    /// não está em <paramref name="conteudo"/>: a estrutura e o grafo não são conferidos, mas as demais regras sim.
    /// </summary>
    public static List<FieldError> Conferir(
        DeclaracaoDoFormulario? declaracao,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes,
        bool conteudoPorInteiro)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        return [.. NoConteudo(ConferirNormalizado(declaracao, conteudo, derivacoes, conteudoPorInteiro))];
    }

    private static List<FieldError> ConferirNormalizado(
        DeclaracaoDoFormulario? declaracao,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes,
        bool conteudoPorInteiro)
    {
        List<FieldError> erros = ConferirPartesDoConteudo(declaracao, conteudo);

        // A estrutura depende da declaração, recusada no cadastro quando incompleta.
        return erros.Count > 0 || !conteudoPorInteiro || declaracao is not { } declarada
            ? erros
            : ConferirEstruturaEGrafo(declarada, conteudo, derivacoes);
    }

    private static List<FieldError> ConferirPartesDoConteudo(DeclaracaoDoFormulario? declaracao, ConteudoDoModelo conteudo) =>
    [
        .. conteudo.TextosNaoGravaveis(),
        .. FormaDoCabecalho.ValidarTitulo(conteudo.Titulo),
        .. ConferirFormaDasPaginas(conteudo),
        .. ConferirCamposEGrupos(declaracao, conteudo),
        .. ConferirFormaDosTermos(conteudo),
        .. ConferirPressupostos(declaracao, conteudo),
        .. FormatoDosCodigosDoConteudo.Conferir(conteudo),
    ];

    private static List<FieldError> ConferirEstruturaEGrafo(
        DeclaracaoDoFormulario declaracao, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        List<FieldError> erros = ConferirEstruturaPelaDeclaracao(declaracao, conteudo);
        if (erros.Count == 0 && ConferirGrafo(conteudo, derivacoes) is { } recusa)
        {
            erros.Add(recusa);
        }

        return erros;
    }

    private static IEnumerable<FieldError> ConferirFormaDasPaginas(ConteudoDoModelo conteudo) =>
        conteudo.Paginas.SelectMany((pagina, i) => NoCaminho(
            FormaDaPagina.Conferir(pagina.Codigo, pagina.Ordem, pagina.Tipo, pagina.Titulo, pagina.Descricao, pagina.Aviso, pagina.Exibicao is not null),
            $"paginas[{i}]"));

    private static List<FieldError> ConferirCamposEGrupos(DeclaracaoDoFormulario? declaracao, ConteudoDoModelo conteudo)
    {
        List<FieldError> excesso = FormaDoItem.ValidarQuantidade(
            FormaDoItem.QuantidadeNoTeto(conteudo.Itens.Count, conteudo.Grupos.Select(static g => g.Subitens.Count)));
        return excesso.Count > 0
            ? excesso
            :
            [
                .. conteudo.Itens.SelectMany((item, i) => ConferirCampo(item, $"itens[{i}]")),
                .. Impedimento.ConferirDeclaracao(declaracao, conteudo.Itens.Select(static i => i.Regras.Impedimento)),
                .. conteudo.Grupos.SelectMany((grupo, i) => ConferirGrupo(grupo, $"grupos[{i}]")),
            ];
    }

    private static IEnumerable<FieldError> ConferirFormaDosTermos(ConteudoDoModelo conteudo) =>
    [
        .. conteudo.Termos.SelectMany((termo, i) => NoCaminho(FormaDoTermo.ValidarFormaBasica(termo.Codigo, termo.Ordem), $"termos[{i}]")),
        .. FormaDoTermo.ConferirUnicidade([.. conteudo.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]),
    ];

    /// <summary>A forma de um campo do modelo, item ou campo de grupo, no caminho dele.</summary>
    private static IEnumerable<FieldError> ConferirCampo(ItemDoModelo campo, string caminho) =>
        NoCaminho(
            FormaDoItem.Conferir(campo.FatoCodigo, campo.Ordem, campo.Apresentacao, campo.Formato, campo.Regras),
            caminho);

    /// <summary>A forma do grupo, a do candidato como membro quando ele o inclui e a de cada campo do grupo.</summary>
    private static IEnumerable<FieldError> ConferirGrupo(GrupoDoModelo grupo, string caminho)
    {
        return
        [
            .. NoCaminho(
                FormaDoGrupo.Conferir(
                    grupo.Codigo, grupo.Ordem, grupo.Rotulo, grupo.Ocorrencias,
                    [.. grupo.Subitens.Select(static s => ((string?)s.FatoCodigo, s.PaginaCodigo, s.Regras.Impedimento is not null))],
                    grupo.Regras),
                caminho),
            .. ConferirCandidatoComoMembro(grupo.Ocorrencias, grupo.Subitens, caminho),
            .. grupo.Subitens.SelectMany((campo, j) => ConferirCampo(campo, $"{caminho}.subitens[{j}]")),
        ];
    }

    private static IEnumerable<FieldError> ConferirCandidatoComoMembro(
        OcorrenciasDoGrupo ocorrencias, IReadOnlyList<ItemDoModelo> campos, string caminho) =>
        NoCaminho(
            ocorrencias.ConferirCandidatoComoMembro(
                [.. campos.Select(static s => (s.FatoCodigo, s.Regras.Exibicao is not null, s.Regras.Obrigatoriedade, s.Regras.Restricoes))]),
            caminho);

    private static IEnumerable<FieldError> NoCaminho(IEnumerable<FieldError> erros, string caminho) =>
        erros.Select(e => e.NoCaminhoDoItem(caminho));

    /// <summary>
    /// Pressuposto é fato coletado pela inscrição (UNI-REQ-0144): o modelo que serve à fase de inscrição não
    /// tem, e o fato não é pressuposto e item do mesmo modelo.
    /// </summary>
    private static List<FieldError> ConferirPressupostos(DeclaracaoDoFormulario? declaracao, ConteudoDoModelo conteudo) =>
        declaracao is { ServeAFaseDeInscricao: true } && conteudo.Pressupostos.Count > 0
            ? [new("pressupostos", new DomainError(
                ModeloFormularioErrorCodes.PressupostoNaInscricao,
                "O modelo de inscrição não tem pressupostos: nenhum formulário é respondido antes dele."))]
            : ConferirFatosDosPressupostos(conteudo);

    private static List<FieldError> ConferirFatosDosPressupostos(ConteudoDoModelo conteudo)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        HashSet<string> itens = new(conteudo.Campos().Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        List<FieldError> erros = [];
        for (int i = 0; i < conteudo.Pressupostos.Count; i++)
        {
            string fato = conteudo.Pressupostos[i];
            erros.AddRange(ConferirPressuposto(fato, $"pressupostos[{i}]", vistos, itens));
            vistos.Add(fato);
        }

        return erros;
    }

    private static IEnumerable<FieldError> ConferirPressuposto(string fato, string campo, IReadOnlySet<string> vistos, IReadOnlySet<string> itens) =>
        string.IsNullOrWhiteSpace(fato)
            ? [new(campo, new DomainError(ModeloFormularioErrorCodes.PressupostoEmBranco, "O pressuposto nomeia um fato."))]
            : ConferirFatoDoPressuposto(fato, campo, vistos, itens);

    private static IEnumerable<FieldError> ConferirFatoDoPressuposto(string fato, string campo, IReadOnlySet<string> vistos, IReadOnlySet<string> itens)
    {
        if (vistos.Contains(fato))
        {
            yield return PressupostoRepetido(campo);
        }

        if (itens.Contains(fato))
        {
            yield return PressupostoTambemItem(campo);
        }
    }

    private static FieldError PressupostoRepetido(string campo) =>
        new(campo, new DomainError(
            ModeloFormularioErrorCodes.PressupostoRepetido, "O fato aparece mais de uma vez entre os pressupostos."));

    private static FieldError PressupostoTambemItem(string campo) =>
        new(campo, new DomainError(
            ModeloFormularioErrorCodes.PressupostoTambemItem,
            "O fato é item do modelo e não pode ser também pressuposto: ou o formulário o coleta, ou o recebe da inscrição."));

    private static List<FieldError> ConferirEstruturaPelaDeclaracao(DeclaracaoDoFormulario declaracao, ConteudoDoModelo conteudo)
    {
        PaginaEstrutura[] paginas = [.. conteudo.Paginas.Select(static e => new PaginaEstrutura(e.Codigo, e.Ordem, e.Tipo, e.Bloco))];
        List<FieldError> erros = [.. EstruturaFormulario.ValidarPaginas(declaracao, paginas)];
        if (erros.Count == 0)
        {
            erros.AddRange(EstruturaFormulario.ValidarItens(
                paginas,
                [
                    .. conteudo.Itens.Select(static i => new ItemEstrutura(i.FatoCodigo, i.Ordem, i.PaginaCodigo)),
                    .. conteudo.Grupos.Select(static g => new ItemEstrutura(g.Codigo, g.Ordem, g.PaginaCodigo)),
                ],
                secaoObrigatoria: true));
        }

        return erros;
    }

    /// <summary>
    /// O grafo de coleta pelas regras do formulário: os pressupostos são os fatos conhecidos antes dele,
    /// e as derivações, as do catálogo. Depois, as citações dos termos e dos documentos exigidos, que só
    /// podem citar o que o modelo coleta, e o campo opcional que alimenta derivação ou negação.
    /// </summary>
    private static FieldError? ConferirGrafo(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ItemDoGrafo[] itens = [.. conteudo.Itens.Select(ParaGrafo)];
        PaginaDoGrafo[] paginas = [.. conteudo.Paginas.Select(static e => new PaginaDoGrafo(e.Codigo, e.Ordem, e.FatosCitados))];
        GrupoDoGrafo[] grupos = [.. conteudo.Grupos.Select(static g => new GrupoDoGrafo(
            g.Codigo, g.Ordem, g.PaginaCodigo, g.FatosCitados, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ParaGrafo)]))];
        HashSet<string> pressupostos = new(conteudo.Pressupostos, StringComparer.Ordinal);

        return NoConteudoDoGrafo(GrafoDoFormulario.ValidarColeta(itens, paginas, pressupostos, derivacoes, grupos))
            ?? ConferirCitacoesDosTermosEDosDocumentos(conteudo, GrafoDoFormulario.Dependencias(itens, pressupostos, derivacoes))
            ?? NoConteudoDoGrafo(ConferirCampoOpcionalQueAlimentaRegra(conteudo, derivacoes));
    }

    private static FieldError? NoConteudoDoGrafo(DomainError? erro) => erro is null ? null : new(string.Empty, erro);

    private static ItemDoGrafo ParaGrafo(ItemDoModelo campo) => new(campo.FatoCodigo, campo.Ordem, campo.PaginaCodigo, campo.FatosCitados);

    private static FieldError? ConferirCitacoesDosTermosEDosDocumentos(ConteudoDoModelo conteudo, DependenciasDoFormulario dependencias) =>
        NoConteudoDoGrafo(conteudo.Termos.Select(t => GrafoDoFormulario.CitacaoInvalidaDoTermo(new TermoDoGrafo(t.Codigo, t.FatosCitados), dependencias))
            .FirstOrDefault(static e => e is not null))
        ?? ConferirCitacoesDosDocumentos(conteudo, dependencias);

    /// <summary>O documento do conjunto padrão cita só o que o modelo coleta, como o termo.</summary>
    private static FieldError? ConferirCitacoesDosDocumentos(ConteudoDoModelo conteudo, DependenciasDoFormulario dependencias)
    {
        foreach ((string caminho, DocumentoExigidoDoModelo documento) in conteudo.Documentos.ComCaminho())
        {
            if (documento.FatosCitados.FirstOrDefault(f => dependencias.Conferir(f, DependenciasDoFormulario.PosicaoDosTermos) is not null) is { } citado)
            {
                return new FieldError($"{caminho}.{CaminhoDaCitacao(documento, citado)}", new DomainError(
                    GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                    "O documento exigido usa um dado que o modelo não coleta, não calcula e não recebe de outro formulário."));
            }
        }

        return null;
    }

    private static string CaminhoDaCitacao(DocumentoExigidoDoModelo documento, string citado)
    {
        if (documento.Quando is not { } quando || !quando.FatosCitados.Contains(citado))
        {
            return "porOpcao.fatoCodigo";
        }

        (string Fato, int Clausula, int Condicao) posicao = quando.Clausulas
            .SelectMany((clausula, i) => clausula.Condicoes.Select((condicao, j) => (condicao.Fato, Clausula: i, Condicao: j)))
            .First(p => string.Equals(p.Fato, citado, StringComparison.Ordinal));
        return $"quando[{posicao.Clausula}][{posicao.Condicao}].fato";
    }

    private static DomainError? ConferirCampoOpcionalQueAlimentaRegra(
        ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        CampoQueAlimentaRegra.PrimeiroOpcional(
            conteudo.Itens.OrderBy(static i => i.Ordem)
                .Concat(conteudo.Grupos.OrderBy(static g => g.Ordem).SelectMany(static g => g.Subitens.OrderBy(static s => s.Ordem)))
                .Select(static i => (i.FatoCodigo, i.Regras.Obrigatoriedade.Tipo)),
            CampoQueAlimentaRegra.Fatos(
                DependenciasCitadas(conteudo, derivacoes), conteudo.CondicoesDasRegras().Select(static c => (c.Fato, c.Operador)),
                conteudo.Itens.Where(static i => i.Regras.Impedimento is not null).Select(static i => i.FatoCodigo)));

    /// <summary>
    /// As dependências dos derivados que as regras do modelo citam, direta ou transitivamente — só elas
    /// alimentam regra no formulário, e não todas as derivações do catálogo.
    /// </summary>
    private static List<string> DependenciasCitadas(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        HashSet<string> expandidos = new(StringComparer.Ordinal);
        Stack<string> pendentes = new(conteudo.FatosCitados());
        List<string> dependencias = [];
        while (pendentes.TryPop(out string? fato))
        {
            foreach (string dependencia in expandidos.Add(fato) ? DependenciasDoDerivado(fato, derivacoes) : [])
            {
                dependencias.Add(dependencia);
                pendentes.Push(dependencia);
            }
        }

        return dependencias;
    }

    private static IReadOnlyCollection<string> DependenciasDoDerivado(string fato, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        derivacoes.TryGetValue(fato, out IReadOnlyCollection<string>? dependencias) ? dependencias : [];
}
