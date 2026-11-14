namespace Unifesspa.UniPlus.Configuracao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

/// <summary>
/// Registra a infraestrutura do módulo Configuracao: os repositórios e os leitores cross-módulo (ADR-0056). O contexto e
/// a unidade de trabalho vêm da persistência única do monólito.
/// </summary>
public static class ConfiguracaoInfrastructureRegistration
{
    public static IServiceCollection AddConfiguracaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AdicionarRepositoriosDaOferta(services);
        AdicionarRepositoriosDosDocumentosEDoAtendimento(services);
        AdicionarRepositoriosDoProcesso(services);
        AdicionarRepositoriosDoCatalogoDeFatos(services);
        AdicionarLeitoresDosCadastros(services);
        AdicionarLeitoresDoProcesso(services);
        return services;
    }

    // Os cadastros da oferta de vagas: campi, locais, cursos, ofertas e modalidades, e as bases da reserva e do bônus.
    private static void AdicionarRepositoriosDaOferta(IServiceCollection services)
    {
        services.AddScoped<ICampusRepository, CampusRepository>();
        services.AddScoped<ILocalOfertaRepository, LocalOfertaRepository>();
        services.AddScoped<ICursoRepository, CursoRepository>();
        services.AddScoped<IOfertaCursoRepository, OfertaCursoRepository>();
        services.AddScoped<IModalidadeRepository, ModalidadeRepository>();
        services.AddScoped<IReferenciaReservaDemograficaRepository, ReferenciaReservaDemograficaRepository>();
        services.AddScoped<IPesoAreaEnemRepository, PesoAreaEnemRepository>();
        services.AddScoped<IBaseLegalBonusRegionalRepository, BaseLegalBonusRegionalRepository>();
    }

    private static void AdicionarRepositoriosDosDocumentosEDoAtendimento(IServiceCollection services)
    {
        services.AddScoped<ITipoDocumentoRepository, TipoDocumentoRepository>();
        services.AddScoped<ICategoriaDocumentoRepository, CategoriaDocumentoRepository>();
        services.AddScoped<ICondicaoAtendimentoRepository, CondicaoAtendimentoRepository>();
        services.AddScoped<IRecursoAcessibilidadeRepository, RecursoAcessibilidadeRepository>();
        services.AddScoped<ITipoDeficienciaRepository, TipoDeficienciaRepository>();
    }

    // O desenho do processo seletivo: fases, bancas, tipos de processo e de etapa, calendário, termos e formulários.
    private static void AdicionarRepositoriosDoProcesso(IServiceCollection services)
    {
        services.AddScoped<IFaseCanonicaRepository, FaseCanonicaRepository>();
        services.AddScoped<ITipoBancaRepository, TipoBancaRepository>();
        services.AddScoped<ITipoProcessoRepository, TipoProcessoRepository>();
        services.AddScoped<ITipoEtapaRepository, TipoEtapaRepository>();
        services.AddScoped<IPrecedenciaFaseRepository, PrecedenciaFaseRepository>();
        services.AddScoped<ICalendarioDiasUteisRepository, CalendarioDiasUteisRepository>();
        services.AddScoped<ITermoConsentimentoRepository, TermoConsentimentoRepository>();
        services.AddScoped<IModeloFormularioRepository, ModeloFormularioRepository>();
    }

    // Os fatos do candidato e os registros só acrescentados da aprovação da proteção do catálogo de tipos de documento
    // e de fatos do candidato.
    private static void AdicionarRepositoriosDoCatalogoDeFatos(IServiceCollection services)
    {
        services.AddScoped<IFatoCandidatoRepository, FatoCandidatoRepository>();
        services.AddScoped<IAtoDaProtecaoDoCatalogoRepository, AtoDaProtecaoDoCatalogoRepository>();
        services.AddScoped<IManifestacaoDaProtecaoRepository, ManifestacaoDaProtecaoRepository>();
    }

    // Leitores cross-módulo (ADR-0056) dos cadastros da oferta, dos documentos e do atendimento.
    private static void AdicionarLeitoresDosCadastros(IServiceCollection services)
    {
        services.AddScoped<IReferenciaReservaDemograficaReader, ReferenciaReservaDemograficaReader>();
        services.AddScoped<IPesoAreaEnemReader, PesoAreaEnemReader>();
        services.AddScoped<IBaseLegalBonusRegionalReader, BaseLegalBonusRegionalReader>();
        services.AddScoped<IModalidadeReader, ModalidadeReader>();
        services.AddScoped<IOfertaCursoReader, OfertaCursoReader>();
        services.AddScoped<ITipoDocumentoReader, TipoDocumentoReader>();
        services.AddScoped<ICategoriaDocumentoReader, CategoriaDocumentoReader>();
        services.AddScoped<ICondicaoAtendimentoReader, CondicaoAtendimentoReader>();
        services.AddScoped<IRecursoAcessibilidadeReader, RecursoAcessibilidadeReader>();
        services.AddScoped<ITipoDeficienciaReader, TipoDeficienciaReader>();
    }

    // Leitores cross-módulo (ADR-0056) do desenho do processo seletivo e do catálogo de fatos do candidato.
    private static void AdicionarLeitoresDoProcesso(IServiceCollection services)
    {
        services.AddScoped<IFaseCanonicaReader, FaseCanonicaReader>();
        services.AddScoped<ITipoBancaReader, TipoBancaReader>();
        services.AddScoped<ITipoProcessoReader, TipoProcessoReader>();
        services.AddScoped<ITipoEtapaReader, TipoEtapaReader>();
        services.AddScoped<IPrecedenciaFaseReader, PrecedenciaFaseReader>();
        services.AddScoped<ICalendarioVigenteReader, CalendarioVigenteReader>();
        services.AddScoped<ITermoConsentimentoReader, TermoConsentimentoReader>();
        services.AddScoped<IModeloFormularioReader, ModeloFormularioReader>();
        services.AddScoped<IFatoCandidatoReader, FatoCandidatoReader>();
    }
}
