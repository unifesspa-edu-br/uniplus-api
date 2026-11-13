namespace Unifesspa.UniPlus.Autorizacao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

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

        AdicionarTrilhaDaConcessao(services);
        AdicionarFluxoDoPedido(services);
        AdicionarDecisoesDaGovernanca(services);
        AdicionarInstalacao(services);
        AdicionarMetricasDaGovernanca(services);

        AdicionarVersoesDoPerfil(services);
        AdicionarReceptoresDoResultado(services);

        return services;
    }

    private static void AdicionarTrilhaDaConcessao(IServiceCollection services)
    {
        // As origens "instalação" e "pedido" acrescentam os eventos delas à trilha da concessão que gerou.
        services.AddScoped<IEventosDaOrigem, EventosDaInstalacao>();
        services.AddScoped<IEventosDaOrigem, EventosDoPedido>();
        services.AddScoped<TrilhaDaConcessao>();
    }

    private static void AdicionarFluxoDoPedido(IServiceCollection services)
    {
        services.AddScoped<AvisosDoPedido>();
        services.AddScoped<AvisoDePedidoDevolvido>();
        services.AddScoped<AvisoDeDevolucaoPorTermoExigido>();
        services.AddScoped<ConferenciaDoNovoPedido>();
        services.AddScoped<FixacaoDoTermo>();
        services.AddScoped<PublicacaoDoTermo>();
        services.AddScoped<GravacaoDoNovoPedido>();
        services.AddScoped<AceiteDoPedido>();
    }

    private static void AdicionarDecisoesDaGovernanca(IServiceCollection services)
    {
        services.AddScoped<ConcessaoDoPedidoAprovado>();
        services.AddScoped<GravacaoDaDecisaoDoPedido>();
        services.AddScoped<AprovacaoDoPedido>();
        services.AddScoped<TravasDaRevogacao>();
        services.AddScoped<RevogacaoDaConcessao>();
        services.AddScoped<PreparoDaDecisaoDoItem>();
        services.AddScoped<LeituraDaRevisao>();
        services.AddScoped<AberturaDaRevisao>();
        services.AddScoped<AvisosDaProvidencia>();
        services.AddScoped<GravacaoDaProvidencia>();
        services.AddScoped<AberturaDaProvidencia>();
        services.AddScoped<RespostaDaProvidencia>();
        services.AddScoped<AtendimentoDaProvidencia>();
        services.AddScoped<LeituraDaProvidencia>();
    }

    private static void AdicionarInstalacao(IServiceCollection services)
    {
        services.AddScoped<TentativaDeConfirmacao>();
        services.AddScoped<ConcessaoDosGestores>();
    }

    private static void AdicionarMetricasDaGovernanca(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MetricasDaGovernancaDePerfis>();
        services.AddScoped<AmostragemDaGovernanca>();
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
        services.AddScoped<LeitorDoDocumentoDaVersao>();
        services.AddScoped<EfeitosDaTrocaDeVigencia>();
        services.AddScoped<ResultadoDoDocumentoDaVersao>();

        AdicionarGovernancaDaVersao(services);
    }

    private static void AdicionarGovernancaDaVersao(IServiceCollection services)
    {
        AdicionarFilas(services);
        AdicionarDecisoesDaVersao(services);
        AdicionarEfeitosDaVersao(services);
        AdicionarEmissaoDeAvisos(services);
        AdicionarJanelaDeMigracao(services);
        AdicionarReclassificacoes(services);
    }

    private static void AdicionarFilas(IServiceCollection services)
    {
        services.AddScoped<FilaDeVersoesDaEtapa>();
        services.AddScoped<FilaDeJanelasDaEtapa>();
    }

    private static void AdicionarDecisoesDaVersao(IServiceCollection services)
    {
        services.AddScoped<AprovacoesDaVersao>();
        services.AddScoped<CancelamentoDaVersao>();
        services.AddScoped<RemocaoDePermissoes>();
        services.AddScoped<ContencaoPorSuspensao>();
        services.AddScoped<AtivacaoDoPerfil>();
    }

    private static void AdicionarEfeitosDaVersao(IServiceCollection services)
    {
        services.AddScoped<AvisosDaContencao>();
        services.AddScoped<EfeitosDoDesfechoDaVersao>();
        services.AddScoped<ConferenciaDoCatalogoNaVersao>();
        services.AddScoped<ExpiracaoDasVersoesPendentes>();
        services.AddScoped<AvisosDaVersao>();
    }

    private static void AdicionarEmissaoDeAvisos(IServiceCollection services)
    {
        services.AddScoped<EmissorDeAvisos>();
        services.AddScoped<AvisosDosEfeitosDaJanela>();
        services.AddScoped<GruposDaGovernanca>();
        services.AddScoped<AvisoDeEtapaApta>();
    }

    private static void AdicionarJanelaDeMigracao(IServiceCollection services)
    {
        services.AddScoped<LeitorDaMigracao>();
        services.AddScoped<MigracaoSobreOPerfil>();
        services.AddScoped<AutorizacaoDaMigracao>();
        services.AddScoped<DecisoesDaJanela>();
        services.AddScoped<EfeitosDaJanelaNaTroca>();
        services.AddScoped<AvisosDaJanela>();
        services.AddScoped<ProcessamentoDaJanela>();
        services.AddScoped<ProcessamentoDasJanelas>();
        services.AddScoped<MaterializacaoPrevia>();
        services.AddScoped<PerfilParaPedir>();
        services.AddScoped<LeituraDaJanela>();
        services.AddScoped<DetalheDoPerfil>();
    }

    private static void AdicionarReclassificacoes(IServiceCollection services)
    {
        services.AddScoped<ConcessoesDaReclassificacao>();
        services.AddScoped<AutorizacaoDaReclassificacao>();
        services.AddScoped<DecisoesDaReclassificacao>();
        services.AddScoped<FilaDeReclassificacoes>();
        services.AddScoped<DetalheDaReclassificacao>();
        services.AddScoped<AvisosDaReclassificacao>();
        services.AddScoped<ProcessamentoDosAvisosDaReclassificacao>();
    }

    private static void AdicionarReceptoresDoResultado(IServiceCollection services)
    {
        // A ordem é a da consulta do resultado: instalação, anexo de pedido e documento de versão.
        services.AddScoped<IReceptorDoResultadoDaVerificacao, ReceptorDoDocumentoDaInstalacao>();
        services.AddScoped<IReceptorDoResultadoDaVerificacao, ReceptorDoAnexoDoPedido>();
        services.AddScoped<IReceptorDoResultadoDaVerificacao, ReceptorDoDocumentoDaVersao>();
    }
}
