namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A criação valida em dois passos, por decisão: primeiro os campos do pedido, acumulados num só conjunto de erros; só o
/// pedido válido lê a vigência do termo sob a trava compartilhada e confere a versão apresentada.
/// </summary>
public sealed class ValidacaoEmDoisPassosTests
{
    [Fact(DisplayName = "Com campo inválido e versão do termo inválida, só voltam os erros de campo: o termo depende da vigente lida sob trava e não chega a ser conferido")]
    public async Task CampoInvalido_ETermoInvalido_SoErrosDeCampo_TermoNaoConferido_PorDecisao()
    {
        ITermoDeResponsabilidadeRepository termos = Substitute.For<ITermoDeResponsabilidadeRepository>();
        Perfil perfil = PedidoDeConcessaoTestBase.PerfilDeGovernanca();
        CriarPedidoDeConcessaoCommand comando = new(PedidoDeConcessaoTestBase.Leandro.Email, perfil.Id, null, "curta", TermoVersaoApresentadaId: Guid.NewGuid());

        Result<NovoPedido> resultado = await Conferencia(perfil, termos).ConferirAsync(comando, PedidoDeConcessaoTestBase.Betina, CancellationToken.None);

        resultado.Errors.Select(e => e.Error.Code).Should().Equal(PedidoErrorCodes.JustificativaInvalida);
        await termos.DidNotReceive().ObterVigenteSobTravaCompartilhadaAsync(Arg.Any<CancellationToken>());
    }

    private static ConferenciaDoNovoPedido Conferencia(Perfil perfil, ITermoDeResponsabilidadeRepository termos)
    {
        IConcessaoRepository concessoes = Substitute.For<IConcessaoRepository>();
        concessoes.ObterParaConcederAsync(perfil.Id, Arg.Any<CancellationToken>()).Returns(perfil);

        return new ConferenciaDoNovoPedido(
            new PerfilParaPedir(concessoes, Substitute.For<IJanelaDeMigracaoRepository>(), TimeProvider.System),
            Substitute.For<ILeitorDeUnidade>(),
            PedidoDeConcessaoTestBase.Dominios,
            PedidoDeConcessaoTestBase.Catalogo,
            Substitute.For<IIncompatibilidadeDosPapeis>(),
            new FixacaoDoTermo(termos, PedidoDeConcessaoTestBase.Catalogo),
            TimeProvider.System);
    }
}
