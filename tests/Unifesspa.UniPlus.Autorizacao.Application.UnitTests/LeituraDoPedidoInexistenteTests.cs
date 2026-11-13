namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// A leitura de um pedido que não existe responde como a do pedido que a pessoa não vê, para que a resposta não revele
/// se o pedido existe: 404 a quem o token identifica, com as negativas registradas, e 401 a quem não identifica, como no
/// pedido existente. Paulo Nogueira não tem permissão alguma e pediu o pedido existente para si mesmo.
/// </summary>
public sealed class LeituraDoPedidoInexistenteTests
{
    private static readonly PessoaNoToken Paulo = new(new Identidade(Emissor, "paulo.nogueira"), "Paulo Nogueira", "paulo.nogueira@uniplus-test.local", true);

    [Fact(DisplayName = "Com o token sem sujeito, o pedido inexistente responde identidade incompleta, como o existente, e nada se grava")]
    public async Task SemSujeito_InexistenteEExistente_MesmaRecusa()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        Result<PedidoDeConcessao> inexistente = await LerAsync(pedido: null, verificador);
        Result<PedidoDeConcessao> existente = await LerAsync(Pedido(), verificador);

        (inexistente.Error?.Code, existente.Error?.Code).Should().Be((PedidoErrorCodes.IdentidadeIncompleta, PedidoErrorCodes.IdentidadeIncompleta));
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "Com o token identificado e sem permissão, o pedido inexistente responde não encontrado e grava as negativas")]
    public async Task Identificado_Inexistente_NaoEncontradoComAsNegativas()
    {
        VerificadorDeTeste verificador = new(Paulo.Identidade.Sub, permitidas: []);

        Result<PedidoDeConcessao> resultado = await LerAsync(pedido: null, verificador);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
        verificador.Gravados.Should().ContainSingle().Which.Should().NotBeEmpty().And.OnlyContain(static r => !r.Permitido && r.ResourceId == null);
    }

    private static Task<Result<PedidoDeConcessao>> LerAsync(PedidoDeConcessao? pedido, IVerificadorDeAcesso verificador)
    {
        IPedidoDeConcessaoRepository repository = Substitute.For<IPedidoDeConcessaoRepository>();
        repository.ObterParaLeituraAsync(default, default).ReturnsForAnyArgs(pedido);

        return VisibilidadeDoPedido.ObterAsync(pedido?.Id ?? Guid.CreateVersion7(), Paulo, repository, verificador, CancellationToken.None);
    }

    private static PedidoDeConcessao Pedido() =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(Paulo, null),
            PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional()),
            EscopoDaConcessao.DaInstituicao(),
            new DeclaracaoDoPedido("Acompanha os pedidos de atendimento do setor."),
            new DominiosDeTeste(),
            new CatalogoInstitucional(),
            DateTimeOffset.UtcNow).Value!;
}
