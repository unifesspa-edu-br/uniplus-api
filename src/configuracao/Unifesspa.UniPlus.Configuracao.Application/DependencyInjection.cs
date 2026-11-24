namespace Unifesspa.UniPlus.Configuracao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

/// <summary>
/// Registra recursos da camada Application do módulo Configuracao: os validators FluentValidation do assembly, os
/// serviços de gravação e de conferência que os handlers de escrita recebem prontos (tipos de documento, fatos do
/// candidato, ofertas de curso e opções do cadastro institucional da simulação do modelo de formulário) e os do circuito
/// de aprovação da proteção do catálogo de tipos de documento e de fatos do candidato.
/// </summary>
public static class ConfiguracaoApplicationServiceRegistration
{
    public static IServiceCollection AddConfiguracaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(typeof(ConfiguracaoApplicationServiceRegistration).Assembly);

        return services
            .AddServicosDeEscrita()
            .AddServicosDoCircuitoDaProtecao();
    }

    private static IServiceCollection AddServicosDeEscrita(this IServiceCollection services) =>
        services
            .AddScoped<AlteracaoConferidaDoFato>()
            .AddScoped<ConferenciaDasReferenciasDaOferta>()
            .AddScoped<GravacaoDoTipoDocumento>()
            .AddScoped<OpcoesDoCadastroInstitucional>();

    private static IServiceCollection AddServicosDoCircuitoDaProtecao(this IServiceCollection services) =>
        services
            .AddScoped<ItensSobAprovacao>()
            .AddScoped<SolicitacaoDaProtecaoDoCatalogo>()
            .AddScoped<DecisaoNaProtecaoDoCatalogo>()
            .AddScoped<ManifestacaoNaProtecaoDoCatalogo>();
}
