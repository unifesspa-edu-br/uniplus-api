namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Canonicalization;

using Domain.Interfaces;

using ExternalServices;

using Geo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Persistence.Cryptography;
using Persistence.Interceptors;
using Persistence.Readers;
using Persistence.Repositories;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Infrastructure.Time;

public static class SelecaoInfrastructureRegistration
{
    private static void AddProcessoSeletivoEAsLeiturasDaVersaoPublicada(this IServiceCollection services)
    {
        services.AddScoped<IProcessoSeletivoRepository, ProcessoSeletivoRepository>();
        services.AddScoped<IUnidadeDaFaseNaVersaoVigente, UnidadeDaFaseNaVersaoVigente>();
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
        // Singleton: a zona é constante e o resultado da busca na base de fusos do sistema não
        // muda durante a execução — inclusive quando a base falta, que é o caso que o erro nomeado
        // cobre.
        services.AddSingleton<IResolvedorFusoInstitucional, ResolvedorFusoInstitucional>();
        services.AddScoped<IRegistroCodecsEnvelope, RegistroCodecsEnvelope>();
        services.AddScoped<IRestauradorDeConfiguracao, RestauradorDeConfiguracao>();
    }

    private static void AddRepositoriosDoModulo(this IServiceCollection services)
    {
        services.AddScoped<IObrigatoriedadeLegalRepository, ObrigatoriedadeLegalRepository>();
        services.AddScoped<IMotivoDeDecisaoRepository, MotivoDeDecisaoRepository>().AddScoped<IMotivoDoProcessoRepository, MotivoDoProcessoRepository>();
        services.AddScoped<IDocumentoAPublicarRepository, DocumentoAPublicarRepository>();
        services.AddScoped<IAtoDoProcessoRepository, AtoDoProcessoRepository>();
        services.AddScoped<IModeloDeDocumentoRepository, ModeloDeDocumentoRepository>();
        services.AddScoped<IDocumentoDaInscricaoRepository, DocumentoDaInscricaoRepository>();
        services.AddScoped<IRascunhoDePublicacaoRepository, RascunhoDePublicacaoRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<ILoteDeNotasDoEnemRepository, LoteDeNotasDoEnemRepository>();
        services.AddScoped<IGeracaoDeEnvioAoInepRepository, GeracaoDeEnvioAoInepRepository>();
    }

    private static void AddServicosDeAplicacaoDaInscricao(this IServiceCollection services)
    {
        services.AddScoped<PrazoDaCorrecaoDoComprovante>();
        services.AddScoped<CancelamentoDoComprovante>();
        services.AddScoped<MontadorDoComprovante>();
        services.AddScoped<ProjetorDaInscricaoAdministrativa>();
        services.AddScoped<LeituraDaInscricaoAdministrativa>();
        services.AddScoped<ProcessosDasInscricoes>();
        services.AddScoped<IGeradorDeNumeroInscricao, GeradorDeNumeroInscricao>();
        services.AddScoped<IResolvedorDaUnidadeResponsavel, ResolvedorDaUnidadeResponsavel>();
        services.AddScoped<IInscritosDaEdicaoDoEnem, InscritosDaEdicaoDoEnem>();
        services.AddScoped<IIndiceCegoDoCpf, CalculadorDoIndiceCegoDoCpf>();
    }

    private static void AddPortasELeitoresDoModulo(this IServiceCollection services)
    {
        // Substitui a porta nula do host: a Seleção guarda as inscrições e responde ao impedimento do servidor inscrito.
        // Por fábrica, e não por tipo, porque o concreto é interno e o ponto de decisão é injetado por handlers de todos
        // os módulos (ADR-0098).
        services.Replace(ServiceDescriptor.Scoped<IInscricaoDoSujeitoNoProcesso>(
            static sp => new InscricaoDoSujeitoNoProcesso(sp.GetRequiredService<UniPlusDbContext>())));
        services.AddScoped<ILancamentosDeNotaReader, LancamentosDeNotaReader>();
        services.AddScoped<IAvaliacoesDaEtapaReader, AvaliacoesDaEtapaReader>();
        services.AddScoped<IRegraCatalogoReader, RegraCatalogoReader>();
        services.AddScoped<IRetificacaoEmCursoReader, RetificacaoEmCursoReader>();
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

    private static void AddOpcoesDosArquivosDoCandidato(this IServiceCollection services) =>
        // Os nomes dos buckets são obrigatórios: sem eles o pod falha no startup, e não no primeiro envio.
        services.AddOptions<ArquivosDoCandidatoOptions>()
            .BindConfiguration(ArquivosDoCandidatoOptions.SectionName)
            .Validate(
                static o => !string.IsNullOrWhiteSpace(o.BucketDeEnvio) && !string.IsNullOrWhiteSpace(o.BucketDefinitivo)
                    && !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey),
                "ArquivosDoCandidato:BucketDeEnvio, BucketDefinitivo, AccessKey e SecretKey são obrigatórios.")
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
        services.AddProcessoSeletivoEAsLeiturasDaVersaoPublicada();
        services.AddCertameDivulgadoESnapshotDaVersao();
        services.AddRepositoriosDoModulo();
        services.AddServicosDeAplicacaoDaInscricao();
        services.AddPortasELeitoresDoModulo();
        services.AddArmazenamentoDosArquivos();
        services.AddOpcoesDosArquivosDoCandidato();
        services.AddIntegracoesExternasEAcervoPublico();

        return services;
    }
}
