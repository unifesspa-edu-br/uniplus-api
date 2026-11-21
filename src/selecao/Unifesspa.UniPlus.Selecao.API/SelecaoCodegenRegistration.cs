namespace Unifesspa.UniPlus.Selecao.API;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Wolverine;

/// <summary>
/// Opt-ins de codegen Wolverine do módulo Seleção (ADR-0098): portas cujo concreto é interno, ou nasce de
/// fábrica, e que por isso o código gerado resolve do contêiner em vez de construir, sob
/// <c>ServiceLocationPolicy.NotAllowed</c>. A unidade de trabalho é declarada uma vez, em
/// <c>MonolitoCodegenRegistration</c>. Composto pelo host no <c>configureRouting</c> de
/// <c>UseWolverineOutboxCascading</c>, para que <c>WolverineOutboxConfiguration</c> permaneça agnóstico
/// dos tipos de cada módulo.
/// </summary>
public static class SelecaoCodegenRegistration
{
    /// <summary>
    /// Aplica os opt-ins de codegen do Seleção ao <paramref name="opts"/>.
    /// </summary>
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        OptInDasLeiturasDeAtendimentoEDeOferta(opts);
        OptInDasLeiturasDoCronograma(opts);
        OptInDasLeiturasDeClassificacaoEDoTipoDeProcesso(opts);
        OptInDasLeiturasDosCadastrosDaInscricao(opts);
        OptInDasPortasDePublicacoes(opts);
        OptInDasPortasInternasDaSelecao(opts);
        OptInDasPortasRegistradasPorFabrica(opts);
    }

    // Leituras de catálogos da Configuração consumidas pelos handlers do processo (ADR-0056): o contrato é
    // público, mas o concreto é internal à Configuracao.Infrastructure, e o codegen não emite `new` de tipo
    // não público de outro assembly. Atendimento especializado, vagas e modalidades.
    private static void OptInDasLeiturasDeAtendimentoEDeOferta(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ICondicaoAtendimentoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IRecursoAcessibilidadeReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITipoDeficienciaReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IOfertaCursoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IModalidadeReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IReferenciaReservaDemograficaReader>();
    }

    // Mesmo motivo das leituras de atendimento e oferta. Cronograma de fases: fase canônica, tipo de banca,
    // precedências entre fases, calendário de dias úteis vigente e tipos de etapa, congelados por snapshot-copy.
    private static void OptInDasLeiturasDoCronograma(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IFaseCanonicaReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITipoBancaReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IPrecedenciaFaseReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ICalendarioVigenteReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITipoEtapaReader>();
    }

    // Mesmo motivo das leituras de atendimento e oferta. Tipo de processo, pesos por área do ENEM, base legal do
    // bônus regional e vocabulário de fatos do candidato (validação do predicado de desempate).
    private static void OptInDasLeiturasDeClassificacaoEDoTipoDeProcesso(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITipoProcessoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IPesoAreaEnemReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IBaseLegalBonusRegionalReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IFatoCandidatoReader>();
    }

    // Mesmo motivo das leituras de atendimento e oferta. Cadastros que a inscrição congela: tipos e categorias
    // de documento, termos de consentimento e modelos de formulário.
    private static void OptInDasLeiturasDosCadastrosDaInscricao(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITipoDocumentoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ICategoriaDocumentoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITermoConsentimentoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IModeloFormularioReader>();
    }

    // Portas de Publicações (ADR-0056, ADR-0170): o catálogo de tipos de ato confere o tipo declarado antes de
    // publicar, e o registro de atos é escrito na transação do domínio que publica. Os concretos são internal ao
    // módulo de Publicações.
    private static void OptInDasPortasDePublicacoes(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Unifesspa.UniPlus.Publicacoes.Contracts.ITipoAtoPublicadoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Unifesspa.UniPlus.Publicacoes.Contracts.IRegistroDeAtoNormativo>();
    }

    // Portas cujo concreto é internal à Infrastructure do Seleção, e o codegen roda em Selecao.API. Leitores de
    // inscrição, índice cego e lotes do ENEM e do INEP também dependem de serviços de criptografia que o codegen
    // não monta em tempo de compilação.
    private static void OptInDasPortasInternasDaSelecao(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IRegraCatalogoReader>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ICertameDivulgadoRepository>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IResolvedorDaUnidadeResponsavel>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IConferenciaEnderecoGeo>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IInscritosDaEdicaoDoEnem>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IGeracaoDeEnvioAoInepRepository>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ILoteDeNotasDoEnemRepository>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IIndiceCegoDoCpf>();
    }

    // Portas registradas por fábrica, opacas ao codegen (ADR-0098): o acervo público adia a resolução do storage
    // até o uso, o armazenamento do candidato deriva credencial própria das opções, e as portas de impedimento e
    // de designação de banca nascem de uma implementação do host que o módulo dono substitui.
    private static void OptInDasPortasRegistradasPorFabrica(WolverineOptions opts)
    {
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IAcervoPublico>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ArmazenamentoDoCandidato>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IInscricaoDoSujeitoNoProcesso>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<IDesignacaoDeBanca>();
    }
}
