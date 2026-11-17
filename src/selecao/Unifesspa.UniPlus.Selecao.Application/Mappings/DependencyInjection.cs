namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Services;
using Services.AcessoPelaBanca;
using Services.DocumentoDaConfiguracao;
using Services.InscricaoAdministrativa;
using Services.Rascunhos;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// Registra os recursos da camada Application do módulo Seleção. CQRS roda
/// integralmente sobre Wolverine (<c>ICommandBus</c>/<c>IQueryBus</c>) — esta
/// extensão registra os validators FluentValidation, consumidos pelo
/// middleware de validação FluentValidation do Wolverine (<c>UseFluentValidation</c>,
/// configurado em <c>Infrastructure.Core</c>), e os serviços que rodam fora do
/// barramento, como a remoção dos envios de arquivo pendentes vencidos.
/// </summary>
public static class SelecaoApplicationServiceRegistration
{
    public static IServiceCollection AddSelecaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(typeof(SelecaoApplicationServiceRegistration).Assembly);
        AddDefinicaoDoCronogramaDeFases(services);
        AddResolucaoDasDistribuicoesDeVagas(services);
        AddConferenciaDaObrigatoriedadeLegal(services);
        AddPublicacao(services);
        AddAvaliacaoDaConformidade(services);
        AddAcessoAoCampoEAoDocumentoDaInscricao(services);
        AddSalvamentoDoRascunho(services);
        AddRotinaPeriodicaEPortaDePermissao(services);
        AddRascunhoDoProcessoSeletivo(services);

        return services;
    }

    private static void AddRotinaPeriodicaEPortaDePermissao(IServiceCollection services)
    {
        // Escopo por execução da rotina periódica: os repositórios e o DbContext são do escopo.
        services.AddScoped<RemocaoDeArquivosPendentesVencidos>();

        // A porta lida do catálogo do módulo, para o projetor saber qual permissão atende a cada par.
        services.AddSingleton<IPortaDePermissaoPorPar, PortaDePermissaoPorPar<PermissaoSelecao>>();
    }

    // O resolvedor guarda só as portas dos cadastros por construtor; o comando que grava a distribuição e a query que a
    // simula o recebem prontos, sem service location.
    private static void AddResolucaoDasDistribuicoesDeVagas(IServiceCollection services) =>
        services.AddScoped<ConfiguracaoDistribuicaoVagasResolver>();

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
        services.AddScoped<BloqueioDoDonoDoDocumentoAPublicar>();
        services.AddScoped<ExportacaoDoRascunho>();
    }

    // Todo comando que decide num instante (publicação da versão, do ato avulso e do item do edital, leitura do documento) lê o relógio uma só vez por LeituraDoInstanteDoComando.
    private static void AddPublicacao(IServiceCollection services)
    {
        services.AddScoped<LeituraDoInstanteDoComando>();
        services.AddScoped<ConferenciaDoProcessoDivulgado>();
        services.AddScoped<PublicacaoDoAtoDoProcesso>();
        AddPreparacaoDosAtos(services);
        services.AddScoped<EnderecosDosDocumentosDoAto>();
        AddCongelamentoDaVersao(services);
        AddCopiaAoAcervo(services);
    }

    private static void AddPreparacaoDosAtos(IServiceCollection services)
    {
        services.AddScoped<ConferenciaDoTipoDeAto>();
        services.AddScoped<ConferenciaDoTipoDoAtoAvulso>();
        services.AddScoped<ConferenciaDoAtoCorrigido>();
        services.AddScoped<ConferenciaDosDocumentosDoAto>();
        services.AddScoped<PreparacaoDaPublicacaoDoAtoAvulso>();
        services.AddScoped<GrafoVigenteDoProcesso>();
        services.AddScoped<ConferenciaDoProdutoDoEdital>();
        services.AddScoped<PreparacaoDaPublicacaoDoProdutoDoEdital>();
    }

    // A consulta de conformidade recebe a avaliação pronta, sem service location: as portas entram pelo construtor.
    private static void AddAvaliacaoDaConformidade(IServiceCollection services) =>
        services.AddScoped<AvaliacaoDaConformidade>();

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
        services.AddScoped<LeituraDosCorretoresDasEtapas>();
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
    }

    // A cópia dos documentos do ato ao acervo é o efeito posterior da publicação: o handler a recebe pronta, sem service location.
    private static void AddCopiaAoAcervo(IServiceCollection services)
    {
        services.AddScoped<AcervoDaDivulgacao>();
        services.AddScoped<CopiaDosDocumentosDoAto>();
    }
}
