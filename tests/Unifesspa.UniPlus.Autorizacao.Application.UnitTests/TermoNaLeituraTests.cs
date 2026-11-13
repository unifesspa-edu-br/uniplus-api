namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Termos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As consultas do termo e a evidência do aceite na trilha do pedido.</summary>
public sealed class TermoNaLeituraTests
{
    private readonly ITermoDeResponsabilidadeRepository _termos = Substitute.For<ITermoDeResponsabilidadeRepository>();

    [Fact(DisplayName = "Sem versão publicada, a consulta da vigente responde que não há termo")]
    public async Task ObterVigente_SemVersao_Recusa()
    {
        Result<VersaoDoTermoDto> resultado = await ObterTermoVigenteQueryHandler.Handle(new(), _termos, CancellationToken.None);

        resultado.Error!.Code.Should().Be(TermoErrorCodes.SemVersaoVigente);
    }

    [Fact(DisplayName = "A versão lida pelo id diz se é a vigente, e a inexistente é recusada")]
    public async Task ObterVersao_MarcaAVigente()
    {
        VersaoDoTermo antiga = TermosDeTeste.Publicada();
        _termos.ObterVersaoAsync(antiga.Id, Arg.Any<CancellationToken>()).Returns(antiga);
        _termos.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns(TermosDeTeste.Publicada("segunda"));

        (await ObterVersaoDoTermoQueryHandler.Handle(new(antiga.Id), _termos, CancellationToken.None)).Value!.Vigente.Should().BeFalse();
        (await ObterVersaoDoTermoQueryHandler.Handle(new(Guid.NewGuid()), _termos, CancellationToken.None)).Error!.Code
            .Should().Be(TermoErrorCodes.VersaoNaoEncontrada);
    }

    [Fact(DisplayName = "O histórico lista as versões sem o texto e marca a vigente")]
    public async Task Listar_MarcaAVigente()
    {
        VersaoDoTermo vigente = TermosDeTeste.Publicada();
        _termos.ListarVersoesAsync(null, null, 10, Kernel.Pagination.PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns(([vigente], ((string, Guid)?)null, ((string, Guid)?)null));
        _termos.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns(vigente);

        Result<ListarVersoesDoTermoResult> resultado = await ListarVersoesDoTermoQueryHandler.Handle(
            new(null, null, 10, Kernel.Pagination.PaginationDirection.Next), _termos, CancellationToken.None);

        resultado.Value!.Items.Should().ContainSingle().Which.Vigente.Should().BeTrue();
    }

    [Fact(DisplayName = "O aceite do termo aparece na trilha com a versão e o resumo, e não com o texto")]
    public void Trilha_MostraOAceiteDoTermo()
    {
        PedidoDeConcessaoBuilder cenario = PedidoDeConcessaoBuilder.Pedido().ParaSiMesmo().Protegido();
        PedidoDeConcessao pedido = cenario.Criado();

        EventoDaTrilhaDto evento = EventosDoPedido.Montar(pedido, []).Single(e => e.Tipo == "termo_aceito");

        evento.Detalhe.Should().Be($"Versão 1, resumo {cenario.Vigente.Resumo}.");
        evento.Autor!.Sub.Should().Be("betina.neves");
        PedidoMapping.ToDto(pedido, null).Termo!.Aceite!.Resumo.Should().Be(cenario.Vigente.Resumo);
    }

    [Fact(DisplayName = "O pedido devolvido por exigir o termo conta a devolução na trilha")]
    public void Trilha_MostraADevolucaoPorTermoExigido()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Aprovar(new Identidade("https://idp.test/realms/unifesspa", "diogo.souza"), new(["a"], "sensivel", true, null), new(false), DateTimeOffset.UtcNow);

        EventosDoPedido.Montar(pedido, []).Select(e => e.Tipo).Should().Contain("termo_exigido");
    }
}
