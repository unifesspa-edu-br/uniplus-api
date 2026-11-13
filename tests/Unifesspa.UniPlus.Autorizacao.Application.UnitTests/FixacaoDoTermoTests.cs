namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Quem lê a vigência do termo sob a trava compartilhada, e a exceção do termo na trilha da instalação.</summary>
public sealed class FixacaoDoTermoTests
{
    private readonly ITermoDeResponsabilidadeRepository _termos = Substitute.For<ITermoDeResponsabilidadeRepository>();

    [Fact(DisplayName = "O pedido protegido lê a vigente sob a trava compartilhada e a fixa")]
    public async Task Fixar_Protegido_LeSobATrava()
    {
        VersaoDoTermo vigente = TermosDeTeste.Publicada();
        _termos.ObterVigenteSobTravaCompartilhadaAsync(Arg.Any<CancellationToken>()).Returns(vigente);
        PedidoDeConcessao pedido = Novo(PedidoDeConcessaoTestBase.PerfilDeGovernanca());

        Result resultado = await FixarAsync(pedido, PedidoDeConcessaoTestBase.PerfilDeGovernanca(), vigente.Id);

        resultado.IsSuccess.Should().BeTrue();
        pedido.TermoVersaoId.Should().Be(vigente.Id);
    }

    [Fact(DisplayName = "O pedido não protegido não lê a vigência do termo")]
    public async Task Fixar_NaoProtegido_NaoTrava()
    {
        Result resultado = await FixarAsync(Novo(PedidoDeConcessaoTestBase.PerfilComum()), PedidoDeConcessaoTestBase.PerfilComum(), null);

        resultado.IsSuccess.Should().BeTrue();
        await _termos.DidNotReceive().ObterVigenteSobTravaCompartilhadaAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A trilha da instalação mostra a exceção do termo, registrada pelo sistema")]
    public async Task TrilhaDaInstalacao_MostraAExcecao()
    {
        IInstalacaoRepository instalacoes = Substitute.For<IInstalacaoRepository>();
        Instalacao instalacao = (Instalacao)Activator.CreateInstance(typeof(Instalacao), nonPublic: true)!;
        instalacoes.ObterParaLeituraAsync(instalacao.Id, Arg.Any<CancellationToken>()).Returns(instalacao);
        instalacoes.ListarTentativasAsync(instalacao.Id, Arg.Any<CancellationToken>()).Returns([]);
        _termos.ObterExcecaoDaInstalacaoAsync(instalacao.Id, Arg.Any<CancellationToken>())
            .Returns(TermoNaInstalacao.Registrar(instalacao.Id, null, DateTimeOffset.UtcNow));

        IReadOnlyList<EventoDaTrilhaDto>? trilha = await new EventosDaInstalacao(instalacoes, _termos).ObterAsync(instalacao.Id, CancellationToken.None);

        EventoDaTrilhaDto evento = trilha!.Single(e => e.Tipo == "termo_excecao_da_instalacao");
        (evento.Ator, evento.Detalhe).Should().Be(("sistema", "Gestores instalados antes de existir termo de responsabilidade publicado."));
    }

    private Task<Result> FixarAsync(PedidoDeConcessao pedido, Perfil perfil, Guid? apresentada) =>
        new FixacaoDoTermo(_termos, PedidoDeConcessaoTestBase.Catalogo)
            .FixarAsync(pedido, perfil, apresentada, DateTimeOffset.UtcNow, CancellationToken.None);

    private static PedidoDeConcessao Novo(Perfil perfil) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(PedidoDeConcessaoTestBase.Betina, PedidoDeConcessaoTestBase.Leandro.Email),
            perfil,
            PedidoDeConcessaoTestBase.EscopoDe(perfil),
            new DeclaracaoDoPedido(PedidoDeConcessaoTestBase.Justificativa),
            PedidoDeConcessaoTestBase.Dominios,
            PedidoDeConcessaoTestBase.Catalogo,
            DateTimeOffset.UtcNow).Value!;
}
