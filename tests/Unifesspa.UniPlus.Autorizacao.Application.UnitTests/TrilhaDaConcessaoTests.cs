namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A trilha da concessão montada na leitura, com a origem e as providências solicitadas sobre ela (UNI-REQ-0375), cada
/// providência exposta registrada no registro de acesso (ADR-0153).
/// </summary>
public sealed class TrilhaDaConcessaoTests
{
    private const string Administrar = "autorizacao:concessoes:administrar";
    private const string Solicitar = "autorizacao:providencias:solicitar";

    private readonly IRevisaoDeAcessosRepository _revisoes = Substitute.For<IRevisaoDeAcessosRepository>();
    private readonly IJanelaDeMigracaoRepository _janelas = Substitute.For<IJanelaDeMigracaoRepository>();
    private readonly IProvidenciaRepository _providencias = Substitute.For<IProvidenciaRepository>();
    private readonly IEventosDaOrigem _origem = Substitute.For<IEventosDaOrigem>();
    private readonly IConcessaoRepository _concessoes = Substitute.For<IConcessaoRepository>();

    [Fact(DisplayName = "A trilha traz a concessão, a origem, a providência e o atendimento, em ordem cronológica")]
    public async Task Montar_ComAProvidenciaEmOrdem()
    {
        ProvidenciaBuilder cenario = new();
        Providencia atendida = cenario.Aberta();
        atendida.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(ProvidenciaBuilder.Agora.AddMinutes(30)), new DeclaracaoDoAtendimento(ProvidenciaBuilder.Motivo, ProvidenciaBuilder.Leandro.Email));
        _providencias.ListarDaConcessaoAsync(cenario.Concessao.Id, Arg.Any<CancellationToken>()).Returns([atendida]);
        _origem.Tipo.Returns(TipoDeOrigem.Pedido);
        _origem.ObterAsync(cenario.Concessao.OrigemId, Arg.Any<CancellationToken>())
            .Returns([new EventoDaTrilhaDto("pedido_criado", ProvidenciaBuilder.Agora.AddDays(-31), null, null)]);

        Result<IReadOnlyList<EventoDaTrilhaDto>> trilha = await ObterAsync(cenario.Concessao);

        trilha.Value!.Select(static e => e.Tipo).Should().Equal("pedido_criado", "concedida", "providencia_solicitada", "revogada", "providencia_atendida");
    }

    [Fact(DisplayName = "A concessão inexistente responde não encontrada")]
    public async Task Obter_Inexistente_NaoEncontrada()
    {
        Result<IReadOnlyList<EventoDaTrilhaDto>> trilha = await ObterTrilhaDaConcessaoQueryHandler.Handle(
            new ObterTrilhaDaConcessaoQuery(Guid.CreateVersion7()), _concessoes, Trilha(), new VerificadorDeTeste("diogo.souza", [Administrar]), CancellationToken.None);

        trilha.Error!.Code.Should().Be(ConcessaoErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "Cada providência exposta na trilha é registrada pela permissão que dá a leitura das providências")]
    public async Task Montar_RegistraCadaProvidencia()
    {
        ProvidenciaBuilder cenario = new();
        Providencia[] providencias = [cenario.Respondida(), cenario.Aberta()];
        _providencias.ListarDaConcessaoAsync(cenario.Concessao.Id, Arg.Any<CancellationToken>()).Returns(providencias);
        VerificadorDeTeste verificador = new("cecilia.moura", [Solicitar]);

        await ObterAsync(cenario.Concessao, verificador);

        verificador.Gravados.Should().ContainSingle().Which.Select(static r => (r.Permissao, r.ResourceTipo, r.ResourceId)).Should().Equal(
            providencias.Select(static p => (Solicitar, "providencia-sobre-concessao", (Guid?)p.Id)));
    }

    [Fact(DisplayName = "Quem não tem permissão de ler providências não as vê na trilha, e nada é registrado")]
    public async Task Montar_SemPermissaoDeLerProvidencias_Omite()
    {
        ProvidenciaBuilder cenario = new();
        _providencias.ListarDaConcessaoAsync(cenario.Concessao.Id, Arg.Any<CancellationToken>()).Returns([cenario.Aberta()]);
        VerificadorDeTeste verificador = new("leitor.de.concessoes", ["autorizacao:concessoes:consultar"]);

        Result<IReadOnlyList<EventoDaTrilhaDto>> trilha = await ObterAsync(cenario.Concessao, verificador);

        trilha.Value!.Select(static e => e.Tipo).Should().NotContain("providencia_solicitada");
        verificador.Gravados.Should().BeEmpty();
    }

    private Task<Result<IReadOnlyList<EventoDaTrilhaDto>>> ObterAsync(Concessao concessao, VerificadorDeTeste? verificador = null)
    {
        _concessoes.ObterComPerfilAsync(concessao.Id, Arg.Any<CancellationToken>()).Returns(new ConcessaoComPerfil(concessao, CenarioDaProvidencia.PerfilNome, 1));

        return ObterTrilhaDaConcessaoQueryHandler.Handle(
            new ObterTrilhaDaConcessaoQuery(concessao.Id), _concessoes, Trilha(), verificador ?? new VerificadorDeTeste("diogo.souza", [Administrar]), CancellationToken.None);
    }

    private TrilhaDaConcessao Trilha() =>
        new(_revisoes, _janelas, _providencias, [_origem], new CenarioDaGovernancaDaVersao.RelogioFixo(ProvidenciaBuilder.Agora.AddHours(1)));
}
