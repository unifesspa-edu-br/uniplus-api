namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Todas as causas que impedem congelar a configuração, na ordem única das famílias de regra e, dentro de cada uma, na posição
/// de entrada (ADR-0125). Cada família devolve todas as suas causas: só deixa de avaliar a regra que lê o dado que outra
/// recusou, e isso vive dentro da regra que o lê. As regras falam o nome do corpo do pedido, e o campo de cada causa que sai daqui
/// é o caminho no documento da configuração (<see cref="CamposDoDocumento"/>).
/// </summary>
internal static class CausasQueImpedemCongelar
{
    /// <summary>As famílias de regra na ordem única da conferência: cada uma devolve todas as suas causas.</summary>
    internal static IReadOnlyList<FamiliaDeCausas> Familias { get; } =
    [
        new(NomeDaFamilia.Identificacao, static c => Identificacao(c.Estado, c.Cadastros.ContagemDePrazos)),
        new(NomeDaFamilia.Conformidade, static c => ChecklistDeConformidade.CausasDeConformidade(c.Estado)),
        new(NomeDaFamilia.Declaracoes, static c => Declaracoes(c.Cronograma, c.Etapas)),
        new(NomeDaFamilia.Conjunto, static c => Conjunto(c.Cronograma, c.Etapas)),
        new(NomeDaFamilia.Completude, static c => Completude(c.Estado, c.Cronograma, c.Etapas, c.Cadastros.ContagemDePrazos)),
        new(NomeDaFamilia.Recursos, static c => Recursos(c.Estado)),
        new(NomeDaFamilia.Bancas, static c => Bancas(c.Cronograma, c.Etapas, c.Cadastros.Corretores)),
        new(NomeDaFamilia.CorrecaoIndependente, static c => CorrecaoIndependente(c.Estado, c.Cadastros.Corretores)),
        new(NomeDaFamilia.TiposDeAto, static c => TiposDeAto(c.Cronograma, c.Etapas, c.Cadastros.TiposDeAtoUnicos)),
        new(NomeDaFamilia.Cascata, static c => CausasDaRecusa.NoCampo(CoberturaDaCascata.CausasDaCascata(c.Estado), "cascataRemanejamento")),
        new(NomeDaFamilia.CondicoesDaCanonicalizacao, static c => CondicoesDaCanonicalizacao.Causas(c.Estado, c.Cadastros)),
    ];

    internal static IReadOnlyList<FieldError> De(EstadoDaConfiguracao estado, CadastrosDaConfiguracao cadastros) =>
        De(ContextoDaConferencia.De(estado, cadastros));

    internal static IReadOnlyList<FieldError> De(ContextoDaConferencia contexto) =>
        CamposDoDocumento.Traduzir(Familias.SelectMany(familia => familia.Causas(contexto)));

    private static IEnumerable<FieldError> Identificacao(EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto) =>
    [
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoIdentificadorLegivel(estado), "identificacao.identificadorLegivel"),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDaLocalidade(estado), "identificacao.localidadeCodigoIbge"),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(estado), "identificacao.algoritmoContagemPrazo"),
        .. CausasDaRecusa.Da(ContagemDePrazos.PendenciaDoCalendarioVigente(estado, contexto), "identificacao"),
    ];

    private static IEnumerable<FieldError> Declaracoes(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        [.. cronograma.RecusasDasDeclaracoes(), .. etapas.RecusasDasDeclaracoes()];

    private static IEnumerable<FieldError> Conjunto(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. cronograma.ConferirEstrutura(),
        .. etapas.Conferir(),
        .. EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas),
        .. EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas),
        .. CicloRecursalDoProcesso.Conferir(cronograma, etapas),
    ];

    private static IEnumerable<FieldError> Completude(
        EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, ContextoDeContagemDePrazos contexto) =>
    [
        .. EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas),
        .. etapas.ConferirCompletude(),
        .. cronograma.ConferirCronogramaExigidoPeloProcesso(estado.OrigemCandidatos, estado.HaVagasOfertadas),
        .. cronograma.ConferirCompletude(contexto.FusoInstitucional),
    ];

    private static IEnumerable<FieldError> Recursos(EstadoDaConfiguracao estado) =>
        PendenciasDosProcedimentosDeRecurso.CausasDosProcedimentosDeRecurso(estado);

    // O recorte de competência é recusa da declaração da banca e já saiu junto das declarações.
    private static IEnumerable<FieldError> Bancas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
    [
        .. ConferenciaDasBancas.ConferirRecursosSemBanca(cronograma, etapas),
        .. ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, etapas),
        .. ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, corretores),
    ];

    private static IEnumerable<FieldError> CorrecaoIndependente(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores) =>
        CausasDaRecusa.NoCampo(PendenciasDaCorrecaoIndependente.CausasDaCorrecaoIndependente(estado, corretores), "etapas");

    // A repetição que é só a vaga já ocupada pelo ato da operação está no ato pedido, não no documento, e fica sem campo.
    private static IEnumerable<FieldError> TiposDeAto(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TiposDeAtoUnicosPorObjeto tipos) =>
    [
        .. ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos(cronograma, etapas, tipos),
        .. ConferenciaDosTiposDeAto.ConferirResultadosSemPapel(cronograma, etapas, tipos),
        .. ConferenciaDosTiposDeAto.ConferirTiposQueCongelamComoResultado(cronograma, etapas, tipos),
    ];
}
