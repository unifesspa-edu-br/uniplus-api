namespace Unifesspa.UniPlus.Selecao.Application.Registro;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Services;
using Services.AcessoPelaBanca;
using Services.Comprovantes;
using Services.DocumentoDaConfiguracao;
using Services.ExportacaoDaVersaoPublicada;
using Services.InscricaoAdministrativa;
using Services.Inscricoes;
using Services.ListagemDeInscricoes;
using Services.NotasDoEnem;
using Services.Rascunhos;
using Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// Registra os validadores e serviços da camada Application do módulo Seleção,
/// incluindo a porta de permissão compartilhada com os demais módulos.
/// </summary>
public static class SelecaoApplicationServiceRegistration
{
    public static IServiceCollection AddSelecaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(typeof(SelecaoApplicationServiceRegistration).Assembly);
        AddConfiguracaoDoProcesso(services);
        AddConferenciaDaObrigatoriedadeLegal(services);
        AddPublicacaoDoProcesso(services);
        AddInscricao(services);
        AddNotasDoEnem(services);
        AddRemocaoDeArquivosPendentesVencidos(services);
        AddPortaDePermissao(services);

        return services;
    }

    // A descrição dos rascunhos recebe só as portas por construtor, e a consulta dos indicadores a recebe pronta.
    private static void AddDescricaoDosRascunhos(IServiceCollection services) =>
        services.AddScoped<DescricaoDosRascunhos>();

    private static void AddRemocaoDeArquivosPendentesVencidos(IServiceCollection services) =>
        services.AddScoped<RemocaoDeArquivosPendentesVencidos>();

    private static void AddPortaDePermissao(IServiceCollection services) =>
        services.AddSingleton<IPortaDePermissaoPorPar, PortaDePermissaoPorPar<PermissaoSelecao>>();

    private static void AddConfiguracaoDoProcesso(IServiceCollection services)
    {
        AddCadastrosDosFormulariosEDosDocumentos(services);
        AddDefinicaoDoCronogramaDeFases(services);
        AddResolucaoDasDistribuicoesDeVagas(services);
        AddRascunhoDoProcessoSeletivo(services);
    }

    private static void AddPublicacaoDoProcesso(IServiceCollection services)
    {
        services.AddScoped<IProjecaoDaVersaoPublicada, ProjecaoDaVersaoPublicada>();
        services.AddScoped<LeituraDoInstanteDoComando>();
        services.AddScoped<ConferenciaDoProcessoDivulgado>();
        services.AddScoped<PublicacaoDoAtoDoProcesso>();
        AddPreparacaoDosAtos(services);
        services.AddScoped<EnderecosDosDocumentosDoAto>();
        AddCongelamentoDaVersao(services);
        AddCopiaAoAcervo(services);
        AddAvaliacaoDaConformidade(services);
    }

    private static void AddInscricao(IServiceCollection services)
    {
        AddTitularPeriodoEVersaoDaInscricao(services);
        AddInicioDescarteECancelamentoDaInscricao(services);
        AddEnvioDaInscricao(services);
        AddConsultaDaInscricaoPeloCandidato(services);
        AddDocumentosDaInscricao(services);
        AddComprovanteDaInscricao(services);
        AddSalvamentoDoRascunho(services);
        AddDescricaoDosRascunhos(services);
        AddAcessoAoCampoEAoDocumentoDaInscricao(services);
        AddConsultaAdministrativaDasInscricoes(services);
    }

    private static void AddTitularPeriodoEVersaoDaInscricao(IServiceCollection services)
    {
        services.AddScoped<InscricaoDoDono>();
        services.AddScoped<PeriodoDaInscricaoResolver>();
        services.AddScoped<VersaoDaInscricaoResolver>();
        services.AddScoped<ContextoDaVersaoFixadaResolver>();
    }

    private static void AddInicioDescarteECancelamentoDaInscricao(IServiceCollection services)
    {
        services.AddScoped<InicioDaInscricao>();
        services.AddScoped<DescarteDaInscricao>();
        services.AddScoped<CancelamentoDaInscricaoPeloTitular>();
    }

    private static void AddEnvioDaInscricao(IServiceCollection services)
    {
        services.AddScoped<ConferenciaDoEnderecoDaInscricao>();
        services.AddScoped<ConferenciaDosMunicipiosDaInscricao>();
        services.AddScoped<PreparacaoDoEnvioDaInscricao>();
        services.AddScoped<ConferenciaDoEnvioDaInscricao>();
        services.AddScoped<RegistroDoEnvioDaInscricao>();
        services.AddScoped<SubmissaoDaInscricao>();
    }

    private static void AddConsultaDaInscricaoPeloCandidato(IServiceCollection services)
    {
        services.AddScoped<LeituraDaInscricao>();
        services.AddScoped<ConsultaDaAvaliacaoDaInscricao>();
        services.AddScoped<PreparacaoDaAvaliacaoDaInscricao>();
        services.AddScoped<ProcessosDasInscricoes>();
    }

    private static void AddDocumentosDaInscricao(IServiceCollection services)
    {
        services.AddScoped<DocumentosEnviadosDaInscricao>();
        services.AddScoped<AcessoAoDocumentoDaInscricao>();
        services.AddScoped<InicioDoEnvioDeDocumento>();
        services.AddScoped<ConfirmacaoDoEnvioDeDocumento>();
        services.AddScoped<RemocaoDoArquivoDaInscricao>();
    }

    private static void AddComprovanteDaInscricao(IServiceCollection services)
    {
        services.AddScoped<PrazoDaCorrecaoDoComprovante>();
        services.AddScoped<CancelamentoDoComprovante>();
        services.AddScoped<MontadorDoComprovante>();
    }

    private static void AddConsultaAdministrativaDasInscricoes(IServiceCollection services)
    {
        services.AddScoped<ProjetorDaInscricaoAdministrativa>();
        services.AddScoped<LeituraDaInscricaoAdministrativa>();
        services.AddScoped<ProjetorDaListagemDeInscricoes>();
    }

    private static void AddNotasDoEnem(IServiceCollection services)
    {
        services.AddScoped<ResolvedorDoEscopoDeNotasDoEnem>();
        services.AddScoped<PreparacaoDaImportacaoDeNotas>();
        services.AddScoped<ImportacaoDasNotasDoEnem>();
        services.AddScoped<DetalheDoLoteDeNotasMontador>();
        services.AddScoped<GeracaoDoEnvioAoInep>();
    }

    // O resolvedor guarda só as portas dos cadastros por construtor; o comando que grava a distribuição e a query que a
    // simula o recebem prontos, sem service location.
    private static void AddResolucaoDasDistribuicoesDeVagas(IServiceCollection services) =>
        services.AddScoped<ConfiguracaoDistribuicaoVagasResolver>();

    // Os cadastros que a aplicação de modelo e a definição dos documentos exigidos consultam guardam só as portas por
    // construtor, e cada handler os recebe prontos, sem service location.
    private static void AddCadastrosDosFormulariosEDosDocumentos(IServiceCollection services)
    {
        services.AddScoped<CadastrosDosDocumentosExigidos>();
        services.AddScoped<CadastrosDaAplicacaoDoModelo>();
    }

    // O serviço de definição e a montagem das fases não guardam estado de uma edição para outra: as portas entram
    // por construtor, e o handler recebe os dois prontos. A retirada dos motivos dos lugares que saem é comum ao
    // cronograma e às etapas.
    private static void AddDefinicaoDoCronogramaDeFases(IServiceCollection services)
    {
        services.AddScoped<MontagemDasFases>();
        services.AddScoped<MotivosDosLugaresQueSaem>();
        services.AddScoped<DefinicaoDoCronogramaDeFases>();
        services.AddScoped<DefinicaoDasEtapas>();
    }

    // A decisão e a leitura da revelação de um campo e da abertura de um documento, pela consulta administrativa, guardam
    // só as portas por construtor, e cada handler os recebe prontos, sem service location.
    private static void AddAcessoAoCampoEAoDocumentoDaInscricao(IServiceCollection services)
    {
        services.AddScoped<AcessoARevelacaoDoCampo>();
        services.AddScoped<LeituraDoIdentificador>();
        services.AddScoped<ClassificacaoDaExigenciaDoArquivo>();
        services.AddScoped<DecisaoDoAcessoAoDocumento>();
        services.AddScoped<EmissaoDoAcessoAoArquivo>();
        services.AddScoped<AcessoARevelacaoPelaBanca>();
    }

    // Salvar o rascunho: as portas entram por construtor de cada serviço, e o handler os recebe prontos, sem service location.
    private static void AddSalvamentoDoRascunho(IServiceCollection services)
    {
        services.AddScoped<InscricaoAtivaDoCandidato>();
        services.AddScoped<RascunhoDoCandidato>();
        services.AddScoped<SalvamentoDoRascunho>();
        services.AddScoped<RespostaDoRascunho>();
    }

    // A leitura do documento só guarda o esquema, que é singleton; os serviços que decidem acesso ou leem cadastro e repositório são do escopo,
    // e a leitura da identificação e do cronograma recebe as portas dos cadastros por construtor, sem service location.
    private static void AddRascunhoDoProcessoSeletivo(IServiceCollection services)
    {
        services.AddScoped<LeituraDosCadastrosDaConfiguracaoCitados>();
        services.AddScoped<LeituraDosCadastrosCitados>();
        services.AddScoped<LeituraDaIdentificacaoEDoCronograma>();
        services.AddSingleton<LeituraDoDocumentoDaConfiguracao>();
        services.AddSingleton<PreparacaoDoDocumentoDoRascunho>();
        services.AddScoped<UnidadeDoDocumentoDoRascunho>();
        services.AddScoped<BloqueioDoRascunhoDeProcessoSeletivo>();
        services.AddScoped<BloqueioCompartilhadoDoProcessoSeletivo>();
        services.AddScoped<ResolucaoDosModelosCitados>();
        services.AddScoped<DiagnosticoDoRascunho>();
        AddExportacaoDoRascunho(services);
    }

    private static void AddExportacaoDoRascunho(IServiceCollection services)
    {
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeUnidades>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeProcesso>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeDocumento>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeMotivosDeDecisao>();
        AddDescricoesDosCadastrosDaConfiguracao(services);
        services.AddScoped<IDescricaoDeCadastro, DescricaoDaParticipacaoDaReserva>();
        services.AddScoped<DescricaoDoDocumentoDoRascunho>();
        services.AddScoped<DescricaoDaLocalidade>();
        services.AddScoped<ExportacaoDoRascunho>();
    }

    private static void AddDescricoesDosCadastrosDaConfiguracao(IServiceCollection services)
    {
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeFase>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeAto>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeCategoriasDeDocumento>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeBanca>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeEtapa>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeOfertasDeCurso>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeModalidades>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeCondicoesDeAtendimento>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeRecursosDeAcessibilidade>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeTiposDeDeficiencia>();
        services.AddScoped<IDescricaoDeCadastro, DescricaoDeBasesLegaisDaFada>();
    }

    private static void AddPreparacaoDosAtos(IServiceCollection services)
    {
        services.AddScoped<ConferenciaDoTipoDeAto>();
        services.AddScoped<ConferenciaDoTipoDoAtoAvulso>();
        services.AddScoped<ConferenciaDoAtoCorrigido>();
        services.AddScoped<ConferenciaDosDocumentosDoAto>();
        services.AddScoped<PreparacaoDaPublicacaoDoAtoAvulso>();
        services.AddScoped<ConfiguracaoVigenteDoProcesso>();
        services.AddScoped<ConferenciaDoProdutoDoEdital>();
        services.AddScoped<PreparacaoDaPublicacaoDoProdutoDoEdital>();
    }

    // A consulta de conformidade recebe a avaliação pronta, sem service location: as portas entram pelo construtor.
    private static void AddAvaliacaoDaConformidade(IServiceCollection services)
    {
        services.AddScoped<LeituraDaVersaoPublicadaDoPainel>();
        services.AddScoped<LeituraDosCadastrosDoPainel>();
        services.AddScoped<AvaliacaoDaConformidade>();
    }

    // A conferência dos campos e das referências de uma obrigatoriedade legal guarda só as portas por construtor, e
    // os handlers de criar e atualizar a recebem pronta, sem service location.
    private static void AddConferenciaDaObrigatoriedadeLegal(IServiceCollection services)
    {
        services.AddScoped<ReferenciasDoPredicado>();
        services.AddScoped<ConferenciaDaObrigatoriedadeLegal>();
    }

    // Os serviços que congelam uma versão (publicação e retificação de ato único) guardam só as
    // portas por construtor, e cada handler os recebe prontos, sem service location.
    private static void AddCongelamentoDaVersao(IServiceCollection services)
    {
        AddConferenciasDoCongelamento(services);
        services.AddScoped<LeituraDoContextoDeContagemDePrazos>();
        services.AddScoped<LeituraDasRegrasCitadasNaConfiguracao>();
        services.AddScoped<LeituraDasPrecedenciasDosCadastros>();
        services.AddScoped<MontagemDoCongelamento>();
        services.AddScoped<MontagemDaDivulgacao>();
        services.AddScoped<GravacaoDaVersaoCongelada>();
        services.AddScoped<CongelamentoDaVersao>();
    }

    private static void AddConferenciasDoCongelamento(IServiceCollection services)
    {
        services.AddScoped<ConferenciaDoAtoEDoDocumento>();
        services.AddScoped<LeituraDaBaseDaRetificacao>();
        services.AddScoped<LeituraDasVersoesPublicadas>();
        services.AddScoped<ConferenciaDoCatalogoEDaProtecao>();
        services.AddScoped<RecopiaAoCongelar>();
        services.AddScoped<ResolucaoDosTiposDeAtoUnicos>();
        services.AddScoped<ConferenciaDosAnexosDoEdital>();
        services.AddScoped<ConferenciaDeConformidadeLegal>();
        services.AddScoped<LeituraDosCadastrosDaConfiguracao>();
        services.AddScoped<ConferenciaDosDocumentosDaOperacao>();
        services.AddScoped<ConferenciaDaOperacaoAoCongelar>();
        services.AddScoped<ConferenciaDaConfiguracaoAoCongelar>();
        services.AddScoped<ConferenciaDosMunicipiosNoGeo>();
    }

    // A cópia dos documentos do ato ao acervo é o efeito posterior da publicação: o handler a recebe pronta, sem service location.
    private static void AddCopiaAoAcervo(IServiceCollection services)
    {
        services.AddScoped<AcervoDaDivulgacao>();
        services.AddScoped<CopiaDosDocumentosDoAto>();
    }
}
