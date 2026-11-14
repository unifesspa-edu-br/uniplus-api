namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A revogação do item da revisão de acessos segue a regra do último Representante do controlador da revogação comum
/// (UNI-REQ-0394): Marlene abriu a revisão, Isabella é Representante, e Diogo, gestor de acessos, decide o item dela. A
/// conferência roda sob a trava do papel que o preparo da decisão tomou antes da concessão, e a recusa não grava o item.
/// </summary>
public sealed class RevogacaoDoItemDaRevisaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Motivo = "Isabella deixou a função de Representante.";

    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Marlene = new(Emissor, "marlene.martins");
    private static readonly Identidade Isabella = new(Emissor, "isabella.figueiredo");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza", "Diogo Bruno Filipe Souza");

    private static readonly string ReverProtecaoDeDados =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.RevisoesReverProtecaoDeDados).Codigo;

    private readonly IRevisaoDeAcessosRepository _revisoes = Substitute.For<IRevisaoDeAcessosRepository>();
    private readonly IConcessaoRepository _concessoes = Substitute.For<IConcessaoRepository>();
    private readonly IPapelDoRepresentante _papel = Substitute.For<IPapelDoRepresentante>();
    private readonly IUnitOfWork _unidade = Substitute.For<IUnitOfWork>();
    private readonly Concessao _designacao = DesignacaoDoRepresentante.De(Isabella, null, Agora);
    private readonly RevisaoDeAcessos _revisao;

    public RevogacaoDoItemDaRevisaoTests()
    {
        _revisao = RevisaoDeAcessos.Abrir(Marlene, [_designacao.Id], Agora.AddDays(-1));
        _revisoes.ObterParaAlterarAsync(_revisao.Id, Arg.Any<CancellationToken>()).Returns(_revisao);
        _concessoes.ObterComPerfilAsync(_designacao.Id, Arg.Any<CancellationToken>()).Returns(new ConcessaoComPerfil(_designacao, "Representante do controlador", null));
        _concessoes.ObterParaAlterarAsync(_designacao.Id, Arg.Any<CancellationToken>()).Returns(_designacao);
        _papel.EVigenteAsync(Isabella, Agora, Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact(DisplayName = "A revogação do item da única designação vigente de Representante é recusada e o item fica pendente")]
    public async Task Revogar_UnicoRepresentante_RecusaSemGravar()
    {
        Result resultado = await RevogarAsync();

        resultado.Error!.Code.Should().Be(ConcessaoErrorCodes.UltimoRepresentante);
        Received.InOrder(() =>
        {
            _ = _papel.TravarAsync(Arg.Any<CancellationToken>());
            _ = _concessoes.ObterParaAlterarAsync(_designacao.Id, Arg.Any<CancellationToken>());
            _ = _papel.HaOutroVigenteAsync(Isabella, Agora, Arg.Any<CancellationToken>());
        });
        _unidade.Received().DescartarAlteracoesNaoSalvas();
        await _unidade.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com outra pessoa vigente no papel, a revogação do item encerra a designação e grava a decisão")]
    public async Task Revogar_ComOutroRepresentante_RevogaEGrava()
    {
        _papel.HaOutroVigenteAsync(Isabella, Agora, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await RevogarAsync();

        resultado.IsSuccess.Should().BeTrue();
        (_designacao.RevogadaEm, _designacao.MotivoRevogacao).Should().Be(((DateTimeOffset?)Agora, Motivo));
        await _unidade.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private Task<Result> RevogarAsync()
    {
        PreparoDaDecisaoDoItem preparo = new(
            _revisoes, _concessoes, _papel, new PapeisDeTeste(), new CenarioDaGovernancaDaVersao.RelogioFixo(Agora));

        return RevogarItemDaRevisaoCommandHandler.Handle(
            new RevogarItemDaRevisaoCommand(_revisao.Id, _revisao.Itens[0].Id, Motivo),
            preparo,
            new RevogacaoDaConcessao(_concessoes, _papel, _unidade),
            new VerificadorDeTeste(Diogo.Sub, [ReverProtecaoDeDados]),
            _unidade,
            Usuarios.De(Diogo),
            CancellationToken.None);
    }
}
