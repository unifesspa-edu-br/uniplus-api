namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Todas as causas que impedem congelar a configuração, na ordem única das famílias de regra e, dentro de cada uma, na posição
/// de entrada (ADR-0125). Cada família devolve todas as suas causas: só deixa de avaliar a regra que lê o dado que outra
/// recusou, e isso vive dentro da regra que o lê. As regras falam o nome do corpo do pedido, e a conversão do campo no ponteiro do
/// documento da configuração (<see cref="PonteiroNoDocumento"/>) é de quem entrega as causas ao cliente. A família das
/// declarações traz também as partes que a leitura recusou (<see cref="OrigemNoDocumento.PartesRecusadas"/>), na ordem em que o
/// documento as traz.
/// </summary>
internal static class CausasQueImpedemCongelar
{
    /// <summary>As famílias de regra na ordem única da conferência: cada uma devolve todas as suas causas.</summary>
    internal static IReadOnlyList<FamiliaDeCausas> Familias { get; } =
    [
        new(NomeDaFamilia.Identificacao, static c => Identificacao(c.Configuracao, c.Cadastros.ContagemDePrazos)),
        new(NomeDaFamilia.Conformidade, static c => ChecklistDeConformidade.CausasDeConformidade(c.Configuracao)),
        new(NomeDaFamilia.Declaracoes, static c => Declaracoes(c.Configuracao.Origem, c.Cronograma, c.Etapas)),
        new(NomeDaFamilia.Conjunto, static c => Conjunto(c.Cronograma, c.Etapas)),
        new(NomeDaFamilia.Completude, static c => Completude(c.Configuracao, c.Cronograma, c.Etapas, c.Cadastros.ContagemDePrazos)),
        new(NomeDaFamilia.Recursos, static c => Recursos(c.Configuracao)),
        new(NomeDaFamilia.Bancas, static c => Bancas(c.Cronograma, c.Etapas, c.Cadastros.Corretores)),
        new(NomeDaFamilia.CorrecaoIndependente, static c => CorrecaoIndependente(c.Configuracao, c.Cadastros.Corretores)),
        new(NomeDaFamilia.TiposDeAto, static c => TiposDeAto(c.Cronograma, c.Etapas, c.Cadastros.TiposDeAtoUnicos)),
        new(NomeDaFamilia.Cascata, static c => CausasDaRecusa.NoCampo(CoberturaDaCascata.CausasDaCascata(c.Configuracao), "cascataRemanejamento")),
        new(NomeDaFamilia.CondicoesDaCanonicalizacao, static c => CondicoesDaCanonicalizacao.Causas(c.Configuracao, c.Cadastros)),
        new(NomeDaFamilia.MotivosDeDecisao, static c => MotivosDasDecisoes(c.Configuracao, c.Cadastros.MotivosEscolhidos)),
    ];

    internal static IReadOnlyList<FieldError> De(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        De(ContextoDaConferencia.De(configuracao, cadastros));

    internal static IReadOnlyList<FieldError> De(ContextoDaConferencia contexto) =>
        [.. Familias.SelectMany(familia => familia.Causas(contexto))];

    private static IEnumerable<FieldError> Identificacao(ConfiguracaoDeclarada configuracao, ContextoDeContagemDePrazos contexto) =>
    [
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoIdentificadorLegivel(configuracao), "identificacao.identificadorLegivel"),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDaLocalidade(configuracao), "identificacao.localidadeCodigoIbge"),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(configuracao), "identificacao.algoritmoContagemPrazo"),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoCalendarioVigente(configuracao, contexto), "identificacao"),
    ];

    // A parte que a leitura recusou já traz a causa como ponteiro do documento; vem na frente das recusas das declarações que ficaram.
    private static IEnumerable<FieldError> Declaracoes(OrigemNoDocumento origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. origem.PartesRecusadas.Select(static parte => parte.Causa),
        .. cronograma.RecusasDasDeclaracoes(),
        .. etapas.RecusasDasDeclaracoes(),
    ];

    private static IEnumerable<FieldError> Conjunto(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. cronograma.ConferirEstrutura(),
        .. etapas.Conferir(),
        .. EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas),
        .. EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas),
        .. CicloRecursalDoProcesso.Conferir(cronograma, etapas),
    ];

    private static IEnumerable<FieldError> Completude(
        ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, ContextoDeContagemDePrazos contexto) =>
    [
        .. EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas),
        .. etapas.ConferirCompletude(),
        .. cronograma.ConferirCronogramaExigidoPeloProcesso(configuracao.Identidade.OrigemCandidatos, configuracao.Vagas.HaVagasOfertadas),
        .. cronograma.ConferirCompletude(contexto.FusoInstitucional),
    ];

    private static IEnumerable<FieldError> Recursos(ConfiguracaoDeclarada configuracao) =>
        PendenciasDosProcedimentosDeRecurso.CausasDosProcedimentosDeRecurso(configuracao);

    // O recorte de competência é recusa da declaração da banca e já saiu junto das declarações.
    private static IEnumerable<FieldError> Bancas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
    [
        .. ConferenciaDasBancas.ConferirRecursosSemBanca(cronograma, etapas),
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
