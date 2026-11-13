namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// A incompatibilidade dos papéis de governança nos três momentos do pedido: o aceite, quando a identidade do beneficiário
/// passa a ser conhecida, confere sem travar; a aprovação que concede trava a pessoa e confere sob a trava. O perfil comum
/// não trava a pessoa. Betina das Neves pede para Leandro Viana.
/// </summary>
public sealed class PapeisIncompativeisNoPedidoTests
{
    private static readonly PessoaNoToken Betina = new(new Identidade(Emissor, "betina.neves"), "Betina das Neves", "betina-dasneves81@uniplus-test.local", true);
    private static readonly PessoaNoToken Leandro = new(new Identidade(Emissor, "leandro.viana"), "Leandro Viana", "leandro-viana84@uniplus-test.local", true);

    private readonly UnidadeDeTrabalho _unidade = new();

    [Fact(DisplayName = "O aceite de quem já exerce o papel incompatível é recusado com 409, sem gravar")]
    public async Task Aceite_ComPapelIncompativel_RecusaSemGravar()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);

        (Result<PedidoDeConcessaoDto> resposta, IEnumerable<object> mensagens) = await Aceite(pedido, new PapeisDeTeste([], Leandro.Identidade))
            .AceitarAsync(pedido.Id, Leandro, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.PapeisIncompativeis);
        (mensagens.Any(), _unidade.Descartou, _unidade.Gravacoes).Should().Be((false, true, 0));
    }

    [Fact(DisplayName = "O aceite de quem não exerce o papel incompatível é gravado")]
    public async Task Aceite_SemPapelIncompativel_Grava()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);

        (Result<PedidoDeConcessaoDto> resposta, _) = await Aceite(pedido, new PapeisDeTeste()).AceitarAsync(pedido.Id, Leandro, CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "A aprovação que concede perfil de governança trava a pessoa e recusa o papel incompatível sem conceder")]
    public async Task Aprovacao_Governanca_TravaERecusa()
    {
        List<string> travas = [];
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();

        Result concedida = await new ConcessaoDoPedidoAprovado(concessoes, new CatalogoInstitucional(), new PapeisDeTeste(travas, Betina.Identidade))
            .ConcederAsync(Decisao(PerfisDeSistema.EncarregadoDeDadosId), DateTimeOffset.UtcNow, CancellationToken.None);

        concedida.Error!.Code.Should().Be(PedidoErrorCodes.PapeisIncompativeis);
        travas.Should().Equal("pessoa:" + Betina.Identidade.Sub);
        await concessoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "A aprovação que concede perfil comum não trava a pessoa nem confere papel, e segue para a concessão igual")]
    public async Task Aprovacao_PerfilComum_NaoTrava()
    {
        List<string> travas = [];
        DecisaoDaEtapa decisao = Decisao(Guid.CreateVersion7());
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();
        Concessao ativa = ConcessaoBuilder.DoPerfil(decisao.Perfil, new CatalogoInstitucional()).Para(Betina.Identidade, Betina.Email).Construir();
        concessoes.ObterNaoRevogadaIgualAsync(default!, default, default!, default).ReturnsForAnyArgs(ativa);

        Result concedida = await new ConcessaoDoPedidoAprovado(concessoes, new CatalogoInstitucional(), new PapeisDeTeste(travas, Betina.Identidade))
            .ConcederAsync(decisao, DateTimeOffset.UtcNow, CancellationToken.None);

        concedida.Error!.Code.Should().Be(PedidoErrorCodes.JaTemConcessao);
        travas.Should().BeEmpty();
    }

    private AceiteDoPedido Aceite(PedidoDeConcessao pedido, PapeisDeTeste papeis) =>
        new(new PedidosNaMemoria(pedido), new DoublesDeAvisos.PerfilQualquer(), DoublesDeAvisos.Avisos(), _unidade, papeis, TimeProvider.System);

    private static PedidoDeConcessao Pedido(bool paraSiMesmo) =>
        PedidoDeConcessao.Criar(
            Betina, paraSiMesmo ? null : Leandro.Email, PerfilComum(), EscopoDaConcessao.DaInstituicao(), "Atende o setor de matrículas.",
            new DominiosDeTeste(), new CatalogoInstitucional(), DateTimeOffset.UtcNow).Value!;

    // O pedido é de Betina para si mesma; o perfil decidido recebe o id do perfil de sistema que a aprovação concederia.
    private static DecisaoDaEtapa Decisao(Guid perfilId)
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Perfil decidido", "Perfil decidido no teste.", ["geral:pedidos:consultar"], new CatalogoInstitucional());
        typeof(Perfil).GetProperty(nameof(Perfil.Id))!.SetValue(perfil, perfilId);

        return new DecisaoDaEtapa(Pedido(paraSiMesmo: true), perfil, PerfilMapping.Avaliar(perfil, new CatalogoDaAplicacao([])), Leandro.Identidade);
    }

    private static Perfil PerfilComum() =>
        PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional());
}
