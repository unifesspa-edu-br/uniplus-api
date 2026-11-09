namespace Unifesspa.UniPlus.Autorizacao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

/// <summary>
/// Registra a camada Application do módulo Autorização. O CQRS roda sobre Wolverine, então
/// só os validators FluentValidation, consumidos pelo middleware do Wolverine, são registrados.
/// </summary>
public static class AutorizacaoApplicationServiceRegistration
{
    public static IServiceCollection AddAutorizacaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(typeof(AutorizacaoApplicationServiceRegistration).Assembly);

        // As origens "instalação" e "pedido" acrescentam os eventos delas à trilha da concessão que gerou.
        services.AddScoped<IEventosDaOrigem, EventosDaInstalacao>();
        services.AddScoped<IEventosDaOrigem, EventosDoPedido>();

        services.AddScoped<AvisosDoPedido>();

        AdicionarVersoesDoPerfil(services);
        AdicionarReceptoresDoResultado(services);

        return services;
    }

    private static void AdicionarVersoesDoPerfil(IServiceCollection services)
    {
        services.AddScoped<AutoriaDaOperacao>();
        services.AddScoped<EnvioDoDocumento>();
        services.AddScoped<LeitorDoContextoDaVersao>();
        services.AddScoped<OperacaoSobreOPerfil>();
        services.AddScoped<VersoesDoPerfil>();
        services.AddScoped<PropostasDeVersao>();
        services.AddScoped<DocumentosDasVersoes>();
        services.AddScoped<EfeitosDaTrocaDeVigencia>();
        services.AddScoped<ResultadoDoDocumentoDaVersao>();

        AdicionarGovernancaDaVersao(services);
    }

    private static void AdicionarGovernancaDaVersao(IServiceCollection services)
    {
        services.AddScoped<FilaDeVersoesDaEtapa>();
        services.AddScoped<AprovacoesDaVersao>();
        services.AddScoped<CancelamentoDaVersao>();
        services.AddScoped<RemocaoDePermissoes>();
        services.AddScoped<EfeitosDoDesfechoDaVersao>();
        services.AddScoped<ExpiracaoDasVersoesPendentes>();
        services.AddScoped<AvisosDaVersao>();
        services.AddScoped<EmissorDeAvisos>();
    }

    private static void AdicionarReceptoresDoResultado(IServiceCollection services)
    {
        // A ordem é a da consulta do resultado: instalação, anexo de pedido e documento de versão.
        services.AddScoped<IReceptorDoResultadoDaVerificacao, ReceptorDoDocumentoDaInstalacao>();
        services.AddScoped<IReceptorDoResultadoDaVerificacao, ReceptorDoAnexoDoPedido>();
        services.AddScoped<IReceptorDoResultadoDaVerificacao, ReceptorDoDocumentoDaVersao>();
    }
}
