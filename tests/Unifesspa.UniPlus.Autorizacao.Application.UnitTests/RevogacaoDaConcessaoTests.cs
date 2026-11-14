namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;

/// <summary>
/// A regra comum da revogação decide qual mínimo conferir pelo que a concessão conta antes de revogá-la: a designação
/// vigente de Representante confere o último Representante, sem a trava dos gestores; a de gestor, o mínimo de gestores,
/// sem consultar o papel; a revogação que o agregado recusa não confere nada (UNI-REQ-0394, ADR-0152).
/// </summary>
public sealed class RevogacaoDaConcessaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Isabella = new("https://idp.test/realms/unifesspa", "isabella.figueiredo");
    private static readonly Identidade Diogo = new("https://idp.test/realms/unifesspa", "diogo.souza");

    private readonly IConcessaoRepository _concessoes = Substitute.For<IConcessaoRepository>();
    private readonly IPapelDoRepresentante _papel = Substitute.For<IPapelDoRepresentante>();
    private readonly IUnitOfWork _unidade = Substitute.For<IUnitOfWork>();

    // A conta do papel se lê antes de revogar, porque depois a designação já não está ativa; a leitura não grava nada, e
    // a recusa do agregado prevalece sobre a do último Representante, que aqui também valeria.
    [Fact(DisplayName = "A revogação que o agregado recusa devolve a recusa dele, mesmo com o papel já lido, sem descartar nem travar os gestores")]
    public async Task Aplicar_RevogacaoRecusada_DevolveARecusaDoAgregado()
    {
        Concessao designacao = DesignacaoDoRepresentante.De(Isabella, null, Agora);

        Result resultado = await AplicarAsync(designacao, motivo: " ");

        resultado.Errors.Single().Error.Code.Should().Be(ConcessaoErrorCodes.MotivoInvalido);
        await _papel.Received(1).HaOutroVigenteAsync(Isabella, Agora, Arg.Any<CancellationToken>());
        _unidade.DidNotReceive().DescartarAlteracoesNaoSalvas();
        await _concessoes.DidNotReceive().TravarGovernancaParaEscritaAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Revogar a única designação vigente de Representante descarta o rastreamento e devolve o último Representante")]
    public async Task Aplicar_UnicoRepresentante_Recusa()
    {
        Concessao designacao = DesignacaoDoRepresentante.De(Isabella, null, Agora);

        Result resultado = await AplicarAsync(designacao);

        resultado.Error!.Code.Should().Be(ConcessaoErrorCodes.UltimoRepresentante);
        resultado.Error.Message.Should().Contain("Designe primeiro quem vai substituí-lo");
        _unidade.Received(1).DescartarAlteracoesNaoSalvas();
        await _concessoes.DidNotReceiveWithAnyArgs().ObterGovernancaDoPerfilAsync(default);
    }

    [Fact(DisplayName = "Revogar a designação já vencida, anterior à regra, não consulta o papel")]
    public async Task Aplicar_DesignacaoVencida_NaoConsultaOPapel()
    {
        Concessao designacao = DesignacaoDoRepresentante.De(Isabella, null, Agora);
        typeof(Concessao).GetProperty(nameof(Concessao.ValidaAte))!.SetValue(designacao, Agora.AddMinutes(-1));

        (await AplicarAsync(designacao)).IsSuccess.Should().BeTrue();

        await _papel.DidNotReceiveWithAnyArgs().HaOutroVigenteAsync(default, default);
    }

    [Fact(DisplayName = "Revogar a concessão de gestor confere o mínimo de gestores sob a trava deles, sem consultar o papel")]
    public async Task Aplicar_Gestor_ConfereOMinimoDeGestores()
    {
        Concessao gestor = ConcessaoBuilder.DoPerfil(Perfil(TipoDeGovernanca.GestorDeAcessos), new CatalogoLivre()).Para(Isabella).Em(Agora.AddDays(-1)).Construir();
        _concessoes.ObterGovernancaDoPerfilAsync(gestor.PerfilId, Arg.Any<CancellationToken>()).Returns(TipoDeGovernanca.GestorDeAcessos);
        _concessoes.ContarGestoresAtivosAsync(default, default, default, default).ReturnsForAnyArgs(1);

        (await AplicarAsync(gestor)).Error!.Code.Should().Be(ConcessaoErrorCodes.MinimoDeGestores);

        await _concessoes.Received(1).TravarGovernancaParaEscritaAsync(Arg.Any<CancellationToken>());
        await _papel.DidNotReceiveWithAnyArgs().HaOutroVigenteAsync(default, default);
    }

    [Fact(DisplayName = "Revogar a concessão de Encarregado de dados não confere mínimo nenhum")]
    public async Task Aplicar_Encarregado_NaoConfereMinimo()
    {
        Concessao encarregado = ConcessaoBuilder.DoPerfil(Perfil(TipoDeGovernanca.EncarregadoDeDados), new CatalogoLivre()).Para(Isabella).Em(Agora.AddDays(-1)).Construir();
        _concessoes.ObterGovernancaDoPerfilAsync(encarregado.PerfilId, Arg.Any<CancellationToken>()).Returns(TipoDeGovernanca.EncarregadoDeDados);

        (await AplicarAsync(encarregado)).IsSuccess.Should().BeTrue();

        await _papel.DidNotReceiveWithAnyArgs().HaOutroVigenteAsync(default, default);
        await _concessoes.DidNotReceive().TravarGovernancaParaEscritaAsync(Arg.Any<CancellationToken>());
    }

    private Task<Result> AplicarAsync(Concessao concessao, string motivo = "Deixou a função.") =>
        new RevogacaoDaConcessao(_concessoes, _papel, _unidade)
            .AplicarAsync(concessao, Agora, () => concessao.Revogar(new AtoDoAgente(Diogo, Agora), motivo), CancellationToken.None);

    private static Perfil Perfil(TipoDeGovernanca governanca) =>
        PerfisDeTeste.DeGovernanca("Perfil de governança", "Perfil de teste.", ["autorizacao:revisoes:conduzir"], new CatalogoLivre(), governanca);
}
