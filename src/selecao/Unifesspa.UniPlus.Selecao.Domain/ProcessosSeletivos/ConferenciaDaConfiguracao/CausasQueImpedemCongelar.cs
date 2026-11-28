namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Todas as causas que impedem congelar a configuração, na ordem única dos assuntos de regra e, dentro de cada um, na posição
/// de entrada (ADR-0125). Cada assunto devolve todas as suas causas: só deixa de avaliar a regra que lê o dado que outra
/// recusou, e isso vive dentro da regra que o lê. As regras falam o nome do corpo do pedido, e a conversão do campo no ponteiro do
/// documento da configuração (<see cref="PonteiroNoDocumento"/>) é de quem entrega as causas ao cliente. O assunto das
/// declarações traz também as partes que a leitura recusou (<see cref="OrigemNoDocumento.PartesRecusadas"/>), na ordem em que o
/// documento as traz.
/// </summary>
internal static class CausasQueImpedemCongelar
{
    /// <summary>Os assuntos de regra na ordem única da conferência: cada um devolve todas as suas causas.</summary>
    internal static IReadOnlyList<AssuntoDaConferencia> Assuntos { get; } =
    [
        new(NomeDoAssunto.Identificacao, static c => Identificacao(c.Configuracao, c.Cadastros.ContagemDePrazos)),
        new(NomeDoAssunto.Conformidade, static c => ChecklistDeConformidade.CausasDeConformidade(c.Configuracao)),
        new(NomeDoAssunto.Declaracoes, static c => Declaracoes(c.Configuracao.Origem, c.Cronograma, c.Etapas)),
        new(NomeDoAssunto.ReferenciasDeRegra, static c => ConferenciaDasReferenciasDeRegra.Conferir(c.Configuracao, c.Cadastros)),
        new(NomeDoAssunto.Conjunto, static c => Conjunto(c.Configuracao, c.Cronograma, c.Etapas, c.Cadastros.Precedencias)),
        new(NomeDoAssunto.Completude, static c => Completude(c.Configuracao, c.Cronograma, c.Etapas, c.Cadastros.ContagemDePrazos, c.Cadastros.TiposDeAtoUnicos.Nomes)),
        new(NomeDoAssunto.Recursos, static c => Recursos(c.Configuracao)),
        new(NomeDoAssunto.Bancas, static c => Bancas(c.Configuracao.Identidade.OrigemCandidatos, c.Cronograma, c.Etapas, c.Cadastros.Corretores)),
        new(NomeDoAssunto.CorrecaoIndependente, static c => CorrecaoIndependente(c.Configuracao, c.Cadastros.Corretores)),
        new(NomeDoAssunto.TiposDeAto, static c => TiposDeAto(c.Cronograma, c.Etapas, c.Cadastros.TiposDeAtoUnicos)),
        new(NomeDoAssunto.Cascata, static c => CausasDaRecusa.NoCampo(CoberturaDaCascata.CausasDaCascata(c.Configuracao), "ordemDeRemanejamento")),
        new(NomeDoAssunto.ConsequenciasDoDescumprimento, static c => CoerenciaDaConsequenciaDoDescumprimento.Causas(c.Configuracao, c.Cadastros.FatosDeModalidade, c.Cronograma, c.Etapas)),
        new(NomeDoAssunto.CondicoesParaRegistrarConfiguracao, static c => CondicoesParaRegistrarConfiguracao.Causas(c.Configuracao, c.Cadastros)),
        new(NomeDoAssunto.MotivosDeDecisao, static c => MotivosDasDecisoes(c.Configuracao, c.Cadastros.MotivosEscolhidos)),
        new(NomeDoAssunto.ProvaPresencial, static c =>
        [
            .. ConferenciaDaProvaPresencial.Conferir(c.Configuracao.ProvaPresencial, c.Cadastros.MunicipiosDoGeo, c.Cadastros.LimiteDeMunicipiosDeProva),
            .. CidadeDeProvaNosFormularios.Conferir(c.Configuracao),
        ]),
        new(NomeDoAssunto.ImportacaoDoResultado, static c => ConferenciaDaImportacaoDoResultado.Conferir(
            ProcessoDosFormularios.De(c.Configuracao), c.Configuracao.ImportacaoDoResultado, c.Configuracao.Cronograma.CronogramaFases)),
    ];

    internal static IReadOnlyList<FieldError> De(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        De(ContextoDaConferencia.De(configuracao, cadastros));

    internal static IReadOnlyList<FieldError> De(ContextoDaConferencia contexto) =>
        [.. Assuntos.SelectMany(assunto => assunto.Causas(contexto))];

    private static IEnumerable<FieldError> Identificacao(ConfiguracaoDeclarada configuracao, ContextoDeContagemDePrazos contexto) =>
    [
        .. CompletudeDaIdentificacao.Recusas(configuracao),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoIdentificadorLegivel(configuracao), IdentidadeDoProcessoDeclarada.CampoDoIdentificadorLegivel),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDaLocalidade(configuracao), IdentidadeDoProcessoDeclarada.CampoDaLocalidade),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(configuracao), IdentidadeDoProcessoDeclarada.CampoDaConvencaoDeContagem),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoCalendarioVigente(configuracao, contexto), "identificacao"),
    ];

    // A parte que a leitura recusou já traz a causa como ponteiro do documento; vem na frente das recusas das declarações que ficaram.
    private static IEnumerable<FieldError> Declaracoes(OrigemNoDocumento origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. origem.PartesRecusadas.Select(static parte => parte.Causa),
        .. cronograma.RecusasDasDeclaracoes(),
        .. etapas.RecusasDasDeclaracoes(),
    ];

    // A precedência do cadastro só é julgada quando a operação a leu: a conferência da publicação a repete porque o cadastro muda entre gravar e publicar.
    private static IEnumerable<FieldError> Conjunto(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, PrecedenciasDosCadastros precedencias) =>
    [
        .. DatasDeReferenciaNoCronograma.ConferirConjunto(configuracao.Cronograma.CronogramaFases, configuracao.Exigencias.DocumentosExigidos, CaminhosDasExigencias.NaLista("exigencias", configuracao.Exigencias.DocumentosExigidos.Count)),
        .. cronograma.ConferirEstrutura(),
        .. precedencias.FoiLida ? cronograma.ConferirPrecedencia(precedencias.Arestas) : [],
        .. etapas.Conferir(),
        .. EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas),
        .. EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas),
    ];

    private static IEnumerable<FieldError> Completude(
        ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, ContextoDeContagemDePrazos contexto,
        NomesDosTiposDeAto nomes) =>
    [
        .. DatasDeReferenciaNoCronograma.ConferirCompletude(configuracao.Cronograma.CronogramaFases, configuracao.Exigencias.DocumentosExigidos, CaminhosDasExigencias.NaLista("exigencias", configuracao.Exigencias.DocumentosExigidos.Count)),
        .. EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas),
        .. etapas.ConferirCompletude(),
        .. cronograma.ConferirCronogramaExigidoPeloProcesso(configuracao.Identidade.OrigemCandidatos, configuracao.Vagas.HaVagasOfertadas),
        .. ExigenciaDaHomologacaoPorOrigem.Causas(configuracao.Identidade.OrigemCandidatos, cronograma, etapas),
        .. EdicoesDoExameDaHomologacao.Causas(cronograma, configuracao.Coleta.OpcoesDeclaradas),
        .. cronograma.ConferirCompletude(contexto.FusoInstitucional),
        .. CicloRecursalDoProcesso.Conferir(cronograma, etapas, nomes),
    ];

    private static IEnumerable<FieldError> Recursos(ConfiguracaoDeclarada configuracao) =>
        PendenciasDosProcedimentosDeRecurso.CausasDosProcedimentosDeRecurso(configuracao);

    // Os candidatos atendidos são recusa da declaração da banca e já saem junto das declarações.
    private static IEnumerable<FieldError> Bancas(
        OrigemCandidatos? origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
    [
        .. QuemJulgaORecurso.SemBanca(cronograma, etapas),
        .. QuemJulgaORecurso.InstanciaSuperiorNaoAdmitida(cronograma, etapas),
        .. ConferenciaDaSegundaInstancia.Conferir(cronograma, etapas),
        .. ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, etapas),
        .. ExigenciaDasBancasDaHomologacao.Causas(origem, cronograma, etapas),
        .. ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, etapas),
        .. ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, corretores),
    ];

    private static IEnumerable<FieldError> CorrecaoIndependente(ConfiguracaoDeclarada configuracao, CorretoresDasEtapas corretores) =>
        CausasDaRecusa.NoCampo(PendenciasDaCorrecaoIndependente.CausasDaCorrecaoIndependente(configuracao, corretores), "etapas");

    // A repetição que é só a vaga já ocupada pelo ato da operação está no ato pedido, não no documento, e fica sem campo.
    private static IEnumerable<FieldError> TiposDeAto(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
    [
        .. ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(cronograma, etapas, tipos),
        .. ConferenciaDosTiposDeAto.ConferirResultadosSemPapel(cronograma, etapas, tipos),
        .. ConferenciaDosTiposDeAto.ConferirTiposQueCongelamComoResultado(cronograma, etapas, tipos),
    ];

    // O motivo recusado por estar desativado ou por ter o tipo não declarado não conta para a cobertura. A avaliação que já
    // começou depende do instante da operação e fica na conferência da retificação.
    private static IEnumerable<FieldError> MotivosDasDecisoes(ConfiguracaoDeclarada configuracao, MotivosEscolhidos motivos)
    {
        LugaresDaDecisao lugares = LugaresDaDecisao.Da(configuracao);

        return
        [
            .. motivos.Desativados(lugares),
            .. motivos.DeTipoNaoDeclarado(lugares),
            .. CoberturaDosMotivos.Faltas(configuracao, lugares, motivos.QueCompoemAVersao(lugares)),
        ];
    }
}
