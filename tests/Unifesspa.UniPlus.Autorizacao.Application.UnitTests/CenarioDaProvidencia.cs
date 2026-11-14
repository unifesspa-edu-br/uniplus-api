namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

/// <summary>
/// O cenário dos casos de uso da providência: os repositórios e a unidade de trabalho substituídos, os avisos de verdade
/// sobre um registro na memória e os titulares de quem administra concessões. Diogo e Marlene administram concessões;
/// Cecília, que solicita, e Leandro, o beneficiário, também, para que os testes confiram quem fica de fora.
/// </summary>
internal sealed class CenarioDaProvidencia
{
    public const string PerfilNome = "Banca de heteroidentificação";
    public const string EmailDiogo = "diogo.bruno.souza@uniplus-test.local";
    public const string EmailMarlene = "marlenemarciamartins@uniplus-test.local";

    private static readonly string Administrar = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Codigo;

    public IProvidenciaRepository Providencias { get; } = Substitute.For<IProvidenciaRepository>();

    public IConcessaoRepository Concessoes { get; } = Substitute.For<IConcessaoRepository>();

    public IUnitOfWork Unidade { get; } = Substitute.For<IUnitOfWork>();

    public IPapelDoRepresentante Papel { get; } = Substitute.For<IPapelDoRepresentante>();

    public ITitularesDaPermissao Titulares { get; } = Substitute.For<ITitularesDaPermissao>();

    public LoggerDeTeste<AvisosDaProvidencia> Logger { get; } = new();

    public CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria Registro { get; } = new();

    public CenarioDaProvidencia()
    {
        Titulares.ListarTitularesComEmailAsync(Administrar, Arg.Any<CancellationToken>()).Returns(
        [
            new(ProvidenciaBuilder.Emissor, "diogo.souza", EmailDiogo),
            new(ProvidenciaBuilder.Emissor, "marlene.martins", EmailMarlene),
            new(ProvidenciaBuilder.Emissor, ProvidenciaBuilder.Cecilia.Identidade.Sub, ProvidenciaBuilder.Cecilia.Email!),
            new(ProvidenciaBuilder.Emissor, ProvidenciaBuilder.Leandro.Identidade.Sub, ProvidenciaBuilder.Leandro.Email!),
        ]);
    }

    public AvisosDaProvidencia Avisos => new(
        new EmissorDeAvisos(Registro, DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
        new GruposDaGovernanca(Titulares),
        new DoublesDeAvisos.LinkDeTeste(),
        Logger);

    public AberturaDaProvidencia Abertura => new(Providencias, Gravacao(), Relogio());

    public RespostaDaProvidencia Resposta => new(Providencias, Gravacao(), Relogio());

    public AtendimentoDaProvidencia Atendimento => new(
        Providencias, Concessoes, new TravasDaRevogacao(Concessoes, Papel, new PapeisDeTeste()), Gravacao(), new RevogacaoDaConcessao(Concessoes, Papel, Unidade), Relogio());

    /// <summary>A concessão como os repositórios a leem: sob a trava compartilhada, com o perfil e travada para alterar.</summary>
    public Concessao ComConcessao(Concessao concessao)
    {
        Providencias.ObterConcessaoSobTravaCompartilhadaAsync(concessao.Id, Arg.Any<CancellationToken>()).Returns(new ConcessaoDaProvidencia(concessao, PerfilNome));
        Concessoes.ObterComPerfilAsync(concessao.Id, Arg.Any<CancellationToken>()).Returns(new ConcessaoComPerfil(concessao, PerfilNome, 1));
        Concessoes.ObterParaAlterarAsync(concessao.Id, Arg.Any<CancellationToken>()).Returns(concessao);
        return concessao;
    }

    /// <summary>A providência gravada sobre a concessão do cenário, como o repositório a devolve.</summary>
    public Providencia ComProvidencia(ProvidenciaBuilder cenario, Func<ProvidenciaBuilder, Providencia> montar)
    {
        ComConcessao(cenario.Concessao);
        Providencia providencia = montar(cenario);
        Providencias.ObterConcessaoIdAsync(providencia.Id, Arg.Any<CancellationToken>()).Returns(providencia.ConcessaoId);
        Providencias.ObterPorIdAsync(providencia.Id, Arg.Any<CancellationToken>()).Returns(providencia);
        return providencia;
    }

    private GravacaoDaProvidencia Gravacao() => new(Unidade, Avisos);

    private static CenarioDaGovernancaDaVersao.RelogioFixo Relogio() => new(ProvidenciaBuilder.Agora.AddMinutes(30));
}

/// <summary>O logger que guarda as mensagens já formatadas, para o teste conferir o que foi registrado.</summary>
internal sealed class LoggerDeTeste<T> : ILogger<T>
{
    public List<string> Mensagens { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Mensagens.Add(formatter(state, exception));
    }
}
