namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class AcessoAoProcessoLeituraTests
{
    [Theory(DisplayName = "Publicado e encerrado são lidos sem consultar o verificador")]
    [InlineData(StatusProcesso.Publicado)]
    [InlineData(StatusProcesso.Encerrado)]
    public async Task LeituraAberta_NaoConsultaOVerificador(StatusProcesso status)
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();

        ResultadoDoAcesso resultado = await Verificar(verificador, status);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "O cancelado que já foi publicado segue lido sem consultar o verificador")]
    public async Task CanceladoQueJaFoiPublicado_NaoConsultaOVerificador()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();

        ResultadoDoAcesso resultado = await Verificar(verificador, StatusProcesso.Cancelado, jaPublicado: true);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Theory(DisplayName = "Rascunho, cancelado sem publicação e status fora da lista seguem a regra do rascunho: pedem configurar na unidade dona")]
    [InlineData(StatusProcesso.Rascunho)]
    [InlineData(StatusProcesso.Cancelado)]
    [InlineData(StatusProcesso.Nenhum)]
    public async Task ForaDaLista_PedeConfigurarNaUnidade(StatusProcesso status)
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Negado);

        ResultadoDoAcesso resultado = await Verificar(verificador, status);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await verificador.ReceivedWithAnyArgs(1).ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "Saber se quem lê configura o processo é pergunta de capacidade: não decide pelo pedido nem grava")]
    public async Task Configura_PerguntaACapacidade()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.PodeAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Permitido);

        bool configura = await AcessoAoProcesso.ConfiguraAsync(verificador, Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None);

        configura.Should().BeTrue();
        await verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    private static Task<ResultadoDoAcesso> Verificar(
        IVerificadorDeAcesso verificador, StatusProcesso status, bool jaPublicado = false)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterEstadoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new EstadoDoProcesso(Guid.CreateVersion7(), status, jaPublicado));

        return AcessoAoProcesso.VerificarLeituraAsync(verificador, processos, Guid.CreateVersion7(), CancellationToken.None);
    }
}
