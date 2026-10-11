namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// Avalia o que depende dos fatos do candidato (ADR-0143): o formulário inteiro, a árvore de
/// exigências sobre essa mesma avaliação e as modalidades em que ele concorre (UNI-REQ-0076,
/// UNI-REQ-0064, UNI-REQ-0069). É a única composição do avaliador do formulário com o resolvedor da
/// árvore: a consulta do candidato, o rascunho e a pré-visualização do processo leem a mesma conta.
/// </summary>
/// <remarks>Função pura: não lê nem grava nada, e a mesma entrada dá sempre o mesmo resultado.</remarks>
public static class AvaliacaoDaInscricao
{
    /// <summary>O fato que diz em quais modalidades o candidato concorre.</summary>
    public const string FatoDaModalidade = "MODALIDADE";

    /// <summary>Como a modalidade derivada é gravada: os códigos das modalidades, um por linha, na ordem das ofertadas.</summary>
    public static DefinicaoDaResposta DefinicaoDaModalidade { get; } = new(TipoDoValorDeResposta.Codigo, Multivalorado: true, Identificador: false);

    public static Result<ResultadoDaAvaliacaoDaInscricao> Avaliar(ConfiguracaoAvaliavel configuracao, EntradaDaAvaliacaoDaInscricao entrada)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(entrada);

        AvaliacaoFormulario formulario = AvaliadorFormulario.Avaliar(
            configuracao.Definicao,
            new EntradaAvaliacaoFormulario(
                RespostaDeCampo.DentroDaOferta(entrada.Respostas, configuracao.Ofertas),
                entrada.EtapasConcluidas,
                entrada.FatosConhecidos,
                entrada.Envio,
                entrada.Grupos.ToDictionary(
                    static g => g.Key,
                    g => (IReadOnlyList<OcorrenciaRespondida>)[.. g.Value.Select(o => o with { Respostas = RespostaDeCampo.DentroDaOferta(o.Respostas, configuracao.Ofertas) })],
                    StringComparer.Ordinal)));

        // As ocorrências de cada grupo visível são as instâncias que a árvore repete; o grupo oculto
        // não tem instância, e a subárvore dele não se aplica.
        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = formulario.Grupos
            .Where(static g => g.Visivel == Ternario.Verdadeiro)
            .ToDictionary(
                static g => g.Codigo,
                static g => (IReadOnlyList<InstanciaEntidade>)[.. g.Ocorrencias.Select(static o => new InstanciaEntidade(o.Id, o.Fatos))],
                StringComparer.Ordinal);
        IReadOnlySet<Guid> deFasePosterior = entrada.NaFaseDaInscricao
            ? ExigenciasDeFasePosterior.Na(configuracao, entrada.FatosConhecidos)
            : new HashSet<Guid>();
        Result<ResultadoResolucaoArvore> arvore = ResolvedorArvoreSatisfacao.Resolver(
            configuracao.Arvore,
            formulario.Fatos,
            new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>(),
            instancias,
            deFasePosterior);
        if (arvore.IsFailure)
        {
            return Result<ResultadoDaAvaliacaoDaInscricao>.Failure(arvore.Error!);
        }

        List<DocumentoAvaliado> documentos = Documentos(
            configuracao.Arvore, arvore.Value!, formulario.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal), deFasePosterior);
        return Result<ResultadoDaAvaliacaoDaInscricao>.Success(new ResultadoDaAvaliacaoDaInscricao(
            formulario,
            Modalidades(configuracao, formulario),
            new DocumentosDaInscricao(documentos, PendenciasDosDocumentos(configuracao, formulario, documentos))));
    }

    /// <summary>
    /// O que o formulário da inscrição pergunta e de que a avaliação depende, para quem consulta saber
    /// quando a resposta muda a avaliação: os campos alcançados pelas derivações e pelos gatilhos das
    /// exigências — o derivado do sistema, pelos campos de que ele é calculado —, os grupos que os
    /// agregados alcançados somam, os grupos pelos quais a árvore de exigências se repete, porque incluir
    /// ou tirar um membro muda o que se exige, e o que decide se esses grupos aparecem.
    /// </summary>
    public static DependenciasDaAvaliacao Dependencias(ConfiguracaoAvaliavel configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        DependenciasDaAvaliacao daModalidade = DependenciasDaModalidade(configuracao.Definicao);
        DependenciasDaAvaliacao dosDocumentos = DependenciasDe(configuracao.Definicao, CitacoesDe(configuracao.Definicao, configuracao.Arvore.Raizes));
        return new DependenciasDaAvaliacao(
            [.. daModalidade.Fatos.Union(dosDocumentos.Fatos, StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. daModalidade.Grupos.Union(dosDocumentos.Grupos, StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Os derivados do sistema de que a avaliação depende — os únicos que vale apurar antes dela,
    /// porque apurar o resto não muda a conta e pode depender de consulta externa.
    /// </summary>
    public static IReadOnlySet<string> DerivadosDoSistemaCitados(ConfiguracaoAvaliavel configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        IEnumerable<string> alvos = AlvosDoFechamento(configuracao.Definicao, CitacoesDe(configuracao.Definicao, configuracao.Arvore.Raizes));
        if (DerivaAModalidade(configuracao.Definicao))
        {
            alvos = alvos.Append(FatoDaModalidade);
        }

        return Fechamento(configuracao.Definicao, alvos).Pressupostos.Where(DerivadosDoSistema.Dependencias.ContainsKey).ToHashSet(StringComparer.Ordinal);
    }

    private static bool DerivaAModalidade(DefinicaoFormulario definicao) =>
        definicao.Derivacoes.Any(static d => d.CodigoFato == FatoDaModalidade);

    /// <summary>Os campos e os grupos da inscrição de que a modalidade depende.</summary>
    private static DependenciasDaAvaliacao DependenciasDaModalidade(DefinicaoFormulario definicao) =>
        DerivaAModalidade(definicao)
            ? DependenciasDe(definicao, new CitacoesDosGatilhos([FatoDaModalidade], [], []))
            : new DependenciasDaAvaliacao([], []);

    /// <summary>
    /// Os campos e os grupos da inscrição de que dependem os fatos citados: os do candidato pelo
    /// fechamento das derivações e das condições de exibição, os de membro pelo grupo que os repete, e o
    /// grupo pelo qual a árvore se repete, com o que decide se ele aparece e é exigido.
    /// </summary>
    private static DependenciasDaAvaliacao DependenciasDe(DefinicaoFormulario definicao, CitacoesDosGatilhos citacoes)
    {
        FechamentoDasDependencias fechamento = Fechamento(definicao, AlvosDoFechamento(definicao, citacoes));
        HashSet<string> camposDaInscricao = new(
            EtapasDaInscricao(definicao).SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        HashSet<string> gruposDaInscricao = new(
            EtapasDaInscricao(definicao).SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);
        DefinicaoAgregado[] agregados = [.. definicao.Agregados
            .Where(a => fechamento.Agregados.Contains(a.Codigo) && gruposDaInscricao.Contains(a.GrupoCodigo))];
        (string Grupo, string Fato)[] deMembro = [.. citacoes.DeMembro.Where(m => gruposDaInscricao.Contains(m.Grupo))];
        string[] repetidos = [.. citacoes.Repetidos.Where(gruposDaInscricao.Contains)];

        return new DependenciasDaAvaliacao(
            [.. CamposAlcancados(fechamento)
                .Where(camposDaInscricao.Contains)
                .Concat(agregados.Select(static a => $"{a.GrupoCodigo}.{a.FatoDeMembro}"))
                .Concat(deMembro.Select(static m => $"{m.Grupo}.{m.Fato}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            [.. agregados.Select(static a => a.GrupoCodigo)
                .Concat(deMembro.Select(static m => m.Grupo))
                .Concat(repetidos)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// De onde parte o fechamento do que as folhas dependem: os fatos do candidato que os gatilhos citam
    /// e o que decide se aparecem os grupos pelos quais a árvore se repete.
    /// </summary>
    private static IEnumerable<string> AlvosDoFechamento(DefinicaoFormulario definicao, CitacoesDosGatilhos citacoes)
    {
        Dictionary<string, IReadOnlyCollection<string>> condicoesDoGrupo = CondicoesDosGrupos(definicao);
        return citacoes.DoCandidato.Concat(citacoes.Repetidos.SelectMany(g => condicoesDoGrupo.GetValueOrDefault(g) ?? []));
    }

    /// <summary>
    /// Do que as folhas dependem: os fatos do candidato que os gatilhos citam; os de membro — os subitens
    /// do grupo pelo qual a subárvore se repete, que o gatilho lê de cada ocorrência —; e os grupos pelos
    /// quais as subárvores dessas folhas se repetem, cujas ocorrências dizem a quem se exige.
    /// </summary>
    private sealed record CitacoesDosGatilhos(
        IReadOnlyCollection<string> DoCandidato,
        IReadOnlyCollection<(string Grupo, string Fato)> DeMembro,
        IReadOnlyCollection<string> Repetidos);

    /// <param name="incluir">As exigências cujos gatilhos contam; todas, quando nulo.</param>
    private static CitacoesDosGatilhos CitacoesDe(
        DefinicaoFormulario definicao, IEnumerable<NoExigencia> raizes, Func<Guid, bool>? incluir = null)
    {
        Dictionary<string, HashSet<string>> subitensPorGrupo = definicao.Etapas
            .SelectMany(static e => e.Grupos)
            .ToDictionary(
                static g => g.Codigo,
                static g => g.Subitens.Select(static s => s.FatoCodigo).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        HashSet<string> doCandidato = new(StringComparer.Ordinal);
        HashSet<(string Grupo, string Fato)> deMembro = [];
        HashSet<string> repetidos = new(StringComparer.Ordinal);
        foreach (NoExigencia raiz in raizes)
        {
            Citar(raiz, grupoRepetido: null);
        }

        return new CitacoesDosGatilhos(doCandidato, deMembro, repetidos);

        // Se a subárvore do nó tem folha que conta.
        bool Citar(NoExigencia no, string? grupoRepetido)
        {
            string? repetido = no.RepetePorEntidade ?? grupoRepetido;
            bool temFolha = false;
            foreach (NoExigencia filho in no.Filhos)
            {
                temFolha |= Citar(filho, repetido);
            }

            if (no.DocumentoExigido is { } documento && incluir?.Invoke(documento.Id) != false)
            {
                temFolha = true;
                CitarGatilho(documento, repetido);
            }

            if (temFolha && no.RepetePorEntidade is { } grupo)
            {
                repetidos.Add(grupo);
            }

            return temFolha;
        }

        void CitarGatilho(DocumentoExigido documento, string? repetido)
        {
            foreach (string fato in documento.Condicoes.Select(static c => c.Fato))
            {
                if (repetido is not null && subitensPorGrupo.TryGetValue(repetido, out HashSet<string>? subitens) && subitens.Contains(fato))
                {
                    deMembro.Add((repetido, fato));
                }
                else
                {
                    doCandidato.Add(fato);
                }
            }
        }
    }

    /// <summary>
    /// O que os fatos alcançam: as dependências das derivações e, de cada campo e agregado alcançado,
    /// o que decide se ele aparece e é exigido — a resposta que esconde o campo também muda a
    /// avaliação, porque a resposta escondida deixa de valer.
    /// </summary>
    private static FechamentoDasDependencias Fechamento(DefinicaoFormulario definicao, IEnumerable<string> citados)
    {
        Dictionary<string, IReadOnlyCollection<string>> condicoes = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyCollection<string>> condicoesDoGrupo = CondicoesDosGrupos(definicao);
        foreach (DefinicaoEtapa etapa in definicao.Etapas)
        {
            IReadOnlyCollection<string> daSecao = etapa.Exibicao?.FatosCitados ?? [];
            foreach (DefinicaoItem item in etapa.Itens)
            {
                condicoes[item.FatoCodigo] = [.. daSecao, .. item.FatosCitados];
            }
        }

        foreach (DefinicaoAgregado agregado in definicao.Agregados)
        {
            condicoes[agregado.Codigo] = condicoesDoGrupo.GetValueOrDefault(agregado.GrupoCodigo) ?? [];
        }

        return FechamentoDasDependencias.De(
            citados,
            definicao.Etapas.SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo).ToHashSet(StringComparer.Ordinal),
            definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal),
            definicao.Agregados.ToDictionary(static a => a.Codigo, StringComparer.Ordinal),
            condicoes);
    }

    /// <summary>
    /// Do que cada grupo depende para aparecer e ser exigido: a exibição da seção dele e as condições de
    /// exibição e de obrigatoriedade do próprio grupo.
    /// </summary>
    private static Dictionary<string, IReadOnlyCollection<string>> CondicoesDosGrupos(DefinicaoFormulario definicao) =>
        definicao.Etapas
            .SelectMany(static e => e.Grupos.Select(g => (Grupo: g, DaSecao: e.Exibicao?.FatosCitados ?? [])))
            .ToDictionary(
                static g => g.Grupo.Codigo,
                static g => (IReadOnlyCollection<string>)[.. g.DaSecao, .. g.Grupo.FatosDoCandidatoCitados],
                StringComparer.Ordinal);

    /// <summary>Os campos perguntados que o fechamento alcança, direto ou pelo derivado do sistema que se calcula deles.</summary>
    private static IEnumerable<string> CamposAlcancados(FechamentoDasDependencias fechamento) =>
        fechamento.Perguntados.Concat(fechamento.Pressupostos
            .Where(DerivadosDoSistema.Dependencias.ContainsKey)
            .SelectMany(static p => DerivadosDoSistema.Dependencias[p]));

    /// <summary>Os códigos das etapas da finalidade Inscrição, que o envio dá como concluídas.</summary>
    public static IReadOnlySet<string> CodigosDasEtapasDaInscricao(DefinicaoFormulario definicao)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        return EtapasDaInscricao(definicao).Select(static e => e.Codigo).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A avaliação só com o que o formulário de inscrição pergunta: as etapas, os itens, os grupos e os
    /// termos da finalidade Inscrição. Os fatos ficam todos, porque a derivação pode citar um fato que
    /// outra finalidade coleta. É sobre ela que se decide o veredito da inscrição: o item obrigatório de
    /// outra finalidade, como a isenção, não é pendência da inscrição.
    /// </summary>
    public static AvaliacaoFormulario DaInscricao(AvaliacaoFormulario formulario)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return formulario with
        {
            Etapas = [.. formulario.Etapas.Where(static e => DaInscricao(e.Codigo))],
            Itens = [.. formulario.Itens.Where(static i => DaInscricao(i.EtapaCodigo))],
            Grupos = [.. formulario.Grupos.Where(static g => DaInscricao(g.EtapaCodigo))],
            Termos = [.. formulario.Termos.Where(static t => DaInscricao(t.Codigo))],
        };
    }

    private static bool DaInscricao(string codigo) => DefinicaoDoProcesso.EhDaFinalidade(codigo, FinalidadeFormulario.Inscricao);

    private static IEnumerable<DefinicaoEtapa> EtapasDaInscricao(DefinicaoFormulario definicao) =>
        definicao.Etapas.Where(static e => DaInscricao(e.Codigo));

    private static ModalidadesDeConcorrencia Modalidades(ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario)
    {
        if (!DerivaAModalidade(configuracao.Definicao))
        {
            return ModalidadesDeConcorrencia.SemDerivacao;
        }

        DependenciasDaAvaliacao dependencias = DependenciasDaModalidade(configuracao.Definicao);
        FatoResolvido modalidade = formulario.Fatos.GetValueOrDefault(FatoDaModalidade) ?? FatoResolvido.Indeterminado();
        if (modalidade is not { Estado: EstadoFato.Resolvido, Valor: { } valor })
        {
            return new ModalidadesDeConcorrencia(
                true, SituacaoDasModalidades.FaltaResponder, [], Pendencias(configuracao, formulario, dependencias), dependencias);
        }

        // A publicação confere cada contribuição da derivação contra as modalidades ofertadas: todo
        // código derivado tem nome na distribuição de vagas.
        Dictionary<string, ModalidadeDeConcorrencia> ofertadas = Ofertadas(configuracao.ModalidadesOfertadas);
        ModalidadeDeConcorrencia[] itens = [.. (RespostaDeCampo.Codigos(valor) ?? [])
            .Where(ofertadas.ContainsKey)
            .Select(c => ofertadas[c])
            .OrderBy(static m => m.Ordem)];

        return new ModalidadesDeConcorrencia(
            true, itens.Length > 0 ? SituacaoDasModalidades.Concorre : SituacaoDasModalidades.NaoConcorre, itens, [], dependencias);
    }

    /// <summary>
    /// As modalidades ofertadas, pelo código — a mesma modalidade se repete em cada oferta de curso —,
    /// com o nome congelado e a posição na ordem ordinal dos códigos, a mesma em que a publicação
    /// congela o domínio da modalidade.
    /// </summary>
    private static Dictionary<string, ModalidadeDeConcorrencia> Ofertadas(IEnumerable<ModalidadeSelecionada> modalidades) =>
        modalidades
            .GroupBy(static m => m.Codigo, StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static (g, ordem) => new ModalidadeDeConcorrencia(
                g.Key, g.Select(static m => m.Descricao).FirstOrDefault(static d => !string.IsNullOrWhiteSpace(d)) ?? g.Key, ordem))
            .ToDictionary(static m => m.Codigo, StringComparer.Ordinal);

    /// <summary>
    /// O que falta responder, na ordem do formulário: os campos e os grupos visíveis da inscrição de que
    /// a derivação depende e que continuam indeterminados. O campo cuja exibição ainda não se decide não
    /// entra: o que falta é a resposta que decide se ele aparece.
    /// </summary>
    private static List<PendenciaDaAvaliacao> Pendencias(
        ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario, DependenciasDaAvaliacao dependencias)
    {
        HashSet<string> campos = new(dependencias.Fatos, StringComparer.Ordinal);
        HashSet<string> grupos = new(dependencias.Grupos, StringComparer.Ordinal);
        Dictionary<string, AvaliacaoItem> itemPorFato = formulario.Itens.ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, AvaliacaoGrupo> grupoPorCodigo = formulario.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        Dictionary<string, FatoColetado> campoPorFato = configuracao.Campos
            .Where(static c => c.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static c => c.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, GrupoColetado> grupoColetado = configuracao.Grupos
            .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static g => g.Codigo, StringComparer.Ordinal);

        List<PendenciaDaAvaliacao> pendencias = [];
        foreach (DefinicaoEtapa etapa in EtapasDaInscricao(configuracao.Definicao))
        {
            foreach (DefinicaoItem item in etapa.Itens.Where(i => campos.Contains(i.FatoCodigo)))
            {
                if (itemPorFato.GetValueOrDefault(item.FatoCodigo) is { Visivel: Ternario.Verdadeiro }
                    && formulario.Fatos.GetValueOrDefault(item.FatoCodigo)?.Estado == EstadoFato.Indeterminado
                    && campoPorFato.TryGetValue(item.FatoCodigo, out FatoColetado? campo))
                {
                    pendencias.Add(new PendenciaDaAvaliacao(campo.FatoCodigo, campo.Rotulo, campo.EtapaCodigo));
                }
            }

            foreach (DefinicaoGrupo grupo in etapa.Grupos.Where(g => grupos.Contains(g.Codigo)))
            {
                if (grupoPorCodigo.GetValueOrDefault(grupo.Codigo) is { Visivel: Ternario.Verdadeiro, Estado: EstadoFato.Indeterminado }
                    && grupoColetado.TryGetValue(grupo.Codigo, out GrupoColetado? coletado))
                {
                    pendencias.Add(new PendenciaDaAvaliacao(coletado.Codigo, coletado.Rotulo, coletado.EtapaCodigo));
                }
            }
        }

        return pendencias;
    }

    /// <summary>
    /// Por fase, o que falta responder para saber se as exigências dela que podem ser exigidas se
    /// aplicam: os campos e os grupos visíveis da inscrição de que os gatilhos delas dependem e que
    /// continuam indeterminados. Cada fase tem as suas, para quem consulta uma fase só não receber o que
    /// não muda nada nela.
    /// </summary>
    private static Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>> PendenciasDosDocumentos(
        ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario, IReadOnlyList<DocumentoAvaliado> documentos) =>
        documentos
            .Where(static d => d.Situacao == SituacaoDoDocumento.PodeSerExigido)
            .GroupBy(static d => d.Documento.ExigidoNaFaseId)
            .Select(fase =>
            {
                HashSet<Guid> daFase = [.. fase.Select(static d => d.Documento.Id)];
                CitacoesDosGatilhos citacoes = CitacoesDe(configuracao.Definicao, configuracao.Arvore.Raizes, daFase.Contains);
                return (Fase: fase.Key, Pendencias: Pendencias(configuracao, formulario, DependenciasDe(configuracao.Definicao, citacoes)));
            })
            .Where(static f => f.Pendencias.Count > 0)
            .ToDictionary(static f => f.Fase, static f => (IReadOnlyList<PendenciaDaAvaliacao>)f.Pendencias);

    /// <summary>
    /// Cada folha da árvore com a situação dela diante do candidato: a de fora de repetição pelo status
    /// da exigência; a de dentro de um grupo repetível uma vez por ocorrência, uma vez sem ocorrência
    /// quando ainda não se sabe se o grupo aparece ou quais membros ele tem, e uma vez não exigida
    /// quando o grupo não aparece ou a lista dele vale sem ocorrência. A folha que depende de fato de
    /// fase posterior não é exigida nem dispensada.
    /// </summary>
    private static List<DocumentoAvaliado> Documentos(
        ArvoreExigenciasCongelada arvore,
        ResultadoResolucaoArvore resultado,
        IReadOnlyDictionary<string, AvaliacaoGrupo> grupos,
        IReadOnlySet<Guid> deFasePosterior)
    {
        List<DocumentoAvaliado> documentos = [];
        foreach (NoExigencia raiz in arvore.Raizes.OrderBy(static r => r.Ordem))
        {
            Coletar(raiz, ancestrais: [], grupoRepetido: null);
        }

        return documentos;

        void Coletar(NoExigencia no, IReadOnlyList<GrupoDaArvore> ancestrais, string? grupoRepetido)
        {
            string? repetido = no.RepetePorEntidade ?? grupoRepetido;
            if (no.Tipo != TipoNo.Folha)
            {
                IReadOnlyList<GrupoDaArvore> dosFilhos =
                [
                    .. ancestrais,
                    new GrupoDaArvore(
                        no.Id, no.Tipo, no.Tipo == TipoNo.GrupoOu ? no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao : null, no.RepetePorEntidade),
                ];
                foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
                {
                    Coletar(filho, dosFilhos, repetido);
                }

                return;
            }

            DocumentoExigido documento = no.DocumentoExigido!;
            DocumentoAvaliado Avaliado(StatusResolucaoExigencia status, string? ocorrenciaId) =>
                new(documento, Situacao(status, deFasePosterior.Contains(documento.Id)), repetido, ocorrenciaId, ancestrais, no.RepetePorEntidade);

            if (repetido is null)
            {
                documentos.Add(Avaliado(resultado.StatusPorExigencia.GetValueOrDefault(documento.Id), null));
                return;
            }

            AvaliacaoGrupo? grupo = grupos.GetValueOrDefault(repetido);
            if (grupo?.Visivel == Ternario.Indeterminado)
            {
                documentos.Add(Avaliado(StatusResolucaoExigencia.AplicabilidadeIndeterminada, null));
                return;
            }

            List<DocumentoAvaliado> porOcorrencia = [.. resultado.StatusPorEntidade
                .Where(s => s.DocumentoExigidoId == documento.Id)
                .Select(s => Avaliado(s.Status, s.EntidadeId))];
            if (porOcorrencia.Count > 0)
            {
                documentos.AddRange(porOcorrencia);
                return;
            }

            // Sem ocorrência, o grupo pendente — visível e sem resposta, ou com uma lista que não vale —
            // ainda não diz quais membros existem: a exigência por membro fica indeterminada, nunca
            // descartada (UNI-REQ-0064). Só o grupo oculto ou a lista vazia que vale a descartam.
            documentos.Add(Avaliado(
                grupo?.Estado == EstadoFato.Indeterminado ? StatusResolucaoExigencia.AplicabilidadeIndeterminada : StatusResolucaoExigencia.NaoAplicavel,
                null));
        }
    }

    /// <summary>
    /// A situação da folha pelo status que o resolvedor deu a ela: a que depende de fato de fase
    /// posterior chega bloqueada, e por isso indeterminada, e não se confunde com a que espera uma
    /// resposta do candidato.
    /// </summary>
    private static SituacaoDoDocumento Situacao(StatusResolucaoExigencia status, bool deFasePosterior) => status switch
    {
        StatusResolucaoExigencia.NaoAplicavel => SituacaoDoDocumento.NaoExigido,
        _ when deFasePosterior => SituacaoDoDocumento.DependeDeFasePosterior,
        StatusResolucaoExigencia.Pendente or StatusResolucaoExigencia.Satisfeita => SituacaoDoDocumento.Exigido,
        _ => SituacaoDoDocumento.PodeSerExigido,
    };
}
