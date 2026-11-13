namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Canonicalization;

using DocumentoDaConfiguracao;

using Domain.Interfaces;

using ExternalServices;

using Geo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Persistence;
using Persistence.Cryptography;
using Persistence.Interceptors;
using Persistence.Readers;
using Persistence.Repositories;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services.ListagemDeInscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;

public static class SelecaoInfrastructureRegistration
{
    private static void AddProcessoSeletivoEAsLeiturasDaVersaoPublicada(this IServiceCollection services)
    {
        services.AddScoped<IProcessoSeletivoRepository, ProcessoSeletivoRepository>();
        services.AddScoped<IUnidadeDaFaseNaVersaoVigente, UnidadeDaFaseNaVersaoVigente>();
        services.AddScoped<IUnidadeDonaDoProcesso, UnidadeDonaDoProcesso>();
        services.AddScoped<IFasesIniciadasDoProcesso, FasesIniciadasDoProcesso>();
    }

    // Scoped por simetria com SoftDelete/Auditable: depende do IUserContext scoped para preencher snapshot_by. A
    // SelecaoContribuicao o acrescenta às opções do contexto.
    private static void AddInterceptorDoHistoricoDasObrigatoriedades(this IServiceCollection services) =>
        services.AddScoped<ObrigatoriedadeLegalHistoricoInterceptor>();

    private static void AddCertameDivulgadoESnapshotDaVersao(this IServiceCollection services)
    {
        // Projeção pública do certame: a tabela cuja existência de linha é a publicidade.
        services.AddScoped<ICertameDivulgadoRepository, CertameDivulgadoRepository>();
        services.AddScoped<ISnapshotPublicacaoCanonicalizer, SnapshotPublicacaoCanonicalizer>();
        services.AddScoped<IRegistroCodecsEnvelope, EnvelopeCodec>();
    }

    private static void AddRepositoriosDoModulo(this IServiceCollection services)
    {
        services.AddScoped<IObrigatoriedadeLegalRepository, ObrigatoriedadeLegalRepository>();
        services.AddScoped<IMotivoDeDecisaoRepository, MotivoDeDecisaoRepository>().AddScoped<IMotivoDeDecisaoDoProcessoRepository, MotivoDeDecisaoDoProcessoRepository>();
        services.AddScoped<IDocumentoAPublicarRepository, DocumentoAPublicarRepository>();
        services.AddScoped<IAtoDoProcessoRepository, AtoDoProcessoRepository>();
        services.AddScoped<IModeloDeDocumentoRepository, ModeloDeDocumentoRepository>();
        services.AddScoped<IDocumentoDaInscricaoRepository, DocumentoDaInscricaoRepository>();
        services.AddScoped<IRascunhoDeProcessoSeletivoRepository, RascunhoDeProcessoSeletivoRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<IComprovanteDaInscricaoRepository, ComprovanteDaInscricaoRepository>();
        services.AddScoped<ILoteDeNotasDoEnemRepository, LoteDeNotasDoEnemRepository>();
        services.AddScoped<IGeracaoDeEnvioAoInepRepository, GeracaoDeEnvioAoInepRepository>();
    }

    private static void AddServicosDeAplicacaoDaInscricao(this IServiceCollection services)
    {
        services.AddEtapasDoEnvioDaInscricao();
        services.AddLeituraEAvaliacaoDaInscricao();
        services.AddScoped<PrazoDaCorrecaoDoComprovante>();
        services.AddScoped<CancelamentoDoComprovante>();
        services.AddScoped<MontadorDoComprovante>();
        services.AddScoped<ProjetorDaInscricaoAdministrativa>();
        services.AddScoped<ProjetorDaListagemDeInscricoes>();
        services.AddScoped<LeituraDaInscricaoAdministrativa>();
        services.AddScoped<ProcessosDasInscricoes>();
        services.AddScoped<IGeradorDeNumeroInscricao, GeradorDeNumeroInscricao>();
        services.AddScoped<IResolvedorDaUnidadeResponsavel, ResolvedorDaUnidadeResponsavel>();
        services.AddScoped<IInscritosDaEdicaoDoEnem, InscritosDaEdicaoDoEnem>();
        services.AddScoped<IIndiceCegoDoCpf, CalculadorDoIndiceCegoDoCpf>();
    }

    private static void AddLeituraEAvaliacaoDaInscricao(this IServiceCollection services)
    {
        services.AddOperacoesDoEnem();
        services.AddScoped<InicioDaInscricao>();
        services.AddScoped<CancelamentoDaInscricaoPeloTitular>();
        services.AddScoped<ConsultaDaAvaliacaoDaInscricao>();
        services.AddScoped<LeituraDaInscricao>();
        services.AddScoped<PreparacaoDaAvaliacaoDaInscricao>();
        services.AddOperacoesDosDocumentosDaInscricao();
    }

    private static void AddOperacoesDoEnem(this IServiceCollection services)
    {
        services.AddScoped<ResolvedorDoEscopoDeNotasDoEnem>();
        services.AddScoped<PreparacaoDaImportacaoDeNotas>();
        services.AddScoped<ImportacaoDasNotasDoEnem>();
        services.AddScoped<GeracaoDoEnvioAoInep>();
        services.AddScoped<DetalheDoLoteDeNotasMontador>();
    }

    private static void AddOperacoesDosDocumentosDaInscricao(this IServiceCollection services)
    {
        services.AddScoped<AcessoAoDocumentoDaInscricao>();
        services.AddScoped<InicioDoEnvioDeDocumento>();
        services.AddScoped<ConfirmacaoDoEnvioDeDocumento>();
        services.AddScoped<RemocaoDoArquivoDaInscricao>();
    }

    private static void AddEtapasDoEnvioDaInscricao(this IServiceCollection services)
    {
        services.AddScoped<ConferenciaDoEnderecoDaInscricao>();
        services.AddScoped<ConferenciaDosMunicipiosDaInscricao>();
        services.AddScoped<DocumentosEnviadosDaInscricao>();
        services.AddScoped<InscricaoDoDono>();
        services.AddScoped<PeriodoDaInscricaoResolver>();
        services.AddScoped<VersaoDaInscricaoResolver>();
        services.AddScoped<ContextoDaVersaoFixadaResolver>();
        services.AddScoped<PreparacaoDoEnvioDaInscricao>();
        services.AddScoped<ConferenciaDoEnvioDaInscricao>();
        services.AddScoped<RegistroDoEnvioDaInscricao>();
        services.AddScoped<SubmissaoDaInscricao>();
        services.AddScoped<DescarteDaInscricao>();
    }

    private static void AddPortasELeitoresDoModulo(this IServiceCollection services)
    {
        // Substitui a porta nula do host: a Seleção guarda as inscrições e responde ao impedimento do servidor inscrito.
        // Por fábrica, e não por tipo, porque o concreto é interno e o ponto de decisão é injetado por handlers de todos
        // os módulos (ADR-0098).
        services.Replace(ServiceDescriptor.Scoped<IInscricaoDoSujeitoNoProcesso>(
            static sp => new InscricaoDoSujeitoNoProcesso(sp.GetRequiredService<UniPlusDbContext>())));
        services.AddScoped<ILancamentosDeNotaReader, LancamentosDeNotaReader>();
        services.AddScoped<IIndicadoresDoProcessoReader, IndicadoresDoProcessoReader>();
        services.AddScoped<IAvaliacoesDaEtapaReader, AvaliacoesDaEtapaReader>();
        services.AddScoped<IRegraCatalogoReader, RegraCatalogoReader>();

        // O esquema do documento é montado uma vez, dos arquivos embutidos no assembly, e é compartilhado por todas as requisições.
        services.AddSingleton<IEsquemaDoDocumentoDaConfiguracao, EsquemaDoDocumentoDaConfiguracao>();
    }

    private static void AddArmazenamentoDosArquivos(this IServiceCollection services)
    {
        // Storage dos arquivos enviados ao processo — envolve o
        // IStorageService compartilhado (registrado uma vez no host via
        // AddUniPlusStorage). Não registra AddUniPlusStorage aqui: é
        // cross-cutting compartilhado entre módulos, já ligado no host.
        services.AddScoped<IArquivoArmazenadoStorage, ArquivoArmazenadoStorageService>();
        services.AddSingleton(static sp =>
        {
            ArquivosDoCandidatoOptions candidato = sp.GetRequiredService<IOptions<ArquivosDoCandidatoOptions>>().Value;
            return new ArmazenamentoDoCandidato(
                StorageServiceCollectionExtensions.CriarStorageComCredencialPropria(sp, candidato.AccessKey, candidato.SecretKey));
        });
        services.AddScoped<IArquivoDoCandidatoStorage, ArquivoDoCandidatoStorageService>();
    }

    private static void AddOpcoesDoModulo(this IServiceCollection services)
    {
        services.AddOpcoesDosArquivosDoCandidato();
        services.AddOpcoesDaTravaDoProcesso();
    }

    private static void AddOpcoesDosArquivosDoCandidato(this IServiceCollection services) =>
        // Os nomes dos buckets são obrigatórios: sem eles o pod falha no startup, e não no primeiro envio.
        services.AddOptions<ArquivosDoCandidatoOptions>()
            .BindConfiguration(ArquivosDoCandidatoOptions.SectionName)
            .Validate(
                static o => !string.IsNullOrWhiteSpace(o.BucketDeEnvio) && !string.IsNullOrWhiteSpace(o.BucketDefinitivo)
                    && !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey),
                "ArquivosDoCandidato:BucketDeEnvio, BucketDefinitivo, AccessKey e SecretKey são obrigatórios.")
            .ValidateOnStart();

    private static void AddOpcoesDaTravaDoProcesso(this IServiceCollection services) =>
        // Limite zero desligaria o lock_timeout do banco e a espera voltaria a ser sem fim: o pod falha no startup.
        services.AddOptions<TravaDoProcessoOptions>()
            .BindConfiguration(TravaDoProcessoOptions.SectionName)
            .Validate(
                TravaDoProcessoOptions.EhValida,
                "TravaDoProcesso:EsperaMaxima deve ser maior que zero e caber em milissegundos de 32 bits.")
            .ValidateOnStart();

    private static void AddIntegracoesExternasEAcervoPublico(this IServiceCollection services)
    {
        services.AddGeoClient();
        services.AddScoped<IGovBrAuthService, GovBrAuthService>();

        // Acervo público (ADR-0132): a escrita, pela materialização da divulgação, e o endereço que
        // os contratos públicos divulgam. As opções vêm de AddUniPlusAcervoPublico, ligado no host
        // junto do storage.
        services.AddScoped<IAcervoPublico>(sp => new AcervoPublicoService(
            () => sp.GetRequiredService<IStorageService>(),
            sp.GetRequiredService<IOptions<StorageOptions>>(),
            sp.GetRequiredService<IOptions<AcervoPublicoOptions>>()));
        services.AddSingleton<IEnderecoNoAcervoPublico, EnderecoNoAcervoPublico>();
    }

    /// <summary>
    /// Registra a infraestrutura do módulo Seleção: o interceptador do histórico das obrigatoriedades legais, os
    /// repositórios, os leitores e os serviços externos. O contexto e a unidade de trabalho vêm da persistência única do
    /// monólito.
    /// </summary>
    public static IServiceCollection AddSelecaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddInterceptorDoHistoricoDasObrigatoriedades();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConferenciaDoDtoAdministrativoNaSubida>());
        services.AddProcessoSeletivoEAsLeiturasDaVersaoPublicada();
        services.AddCertameDivulgadoESnapshotDaVersao();
        services.AddRepositoriosDoModulo();
        services.AddServicosDeAplicacaoDaInscricao();
        services.AddPortasELeitoresDoModulo();
        services.AddArmazenamentoDosArquivos();
        services.AddOpcoesDoModulo();
        services.AddIntegracoesExternasEAcervoPublico();

        return services;
    }
}
