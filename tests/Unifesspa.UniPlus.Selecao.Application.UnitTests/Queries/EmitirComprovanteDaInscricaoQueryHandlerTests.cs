namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;


/// <summary>
/// A unidade emite o comprovante de qualquer inscrição do processo, inclusive a cancelada, se alcança as inscrições e tem a
/// permissão de emitir. O comprovante só é lido depois das duas decisões.
/// </summary>
public sealed class EmitirComprovanteDaInscricaoQueryHandlerTests
{
    private static readonly DateTimeOffset CanceladaEm = new(2026, 11, 8, 1, 32, 5, TimeSpan.Zero);

    private static DecisoesDeAcessoDeTeste Diogo(params PermissaoSelecao[] permissoes) => new("diogo.souza", permissoes);

    private static DecisoesDeAcessoDeTeste ComAsDuas() =>
        Diogo(PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);

    private static async Task<InscricaoAdministrativaDeTeste.Mundo> Montar(InscricaoAdministrativaDeTeste cenario)
    {
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        Inscricao inscricao = (await mundo.Inscricoes.ObterPorIdAsync(cenario.InscricaoId, CancellationToken.None))!;
        mundo.Inscricoes.ObterComprovanteAsync(cenario.InscricaoId, 1, Arg.Any<CancellationToken>())
            .Returns(new ComprovanteNaPosicao(ComprovanteDeTeste.Emitir(inscricao, ComprovanteDeTeste.Respostas()), 1, 1));
        return mundo;
    }

    private static Task<Result<ComprovanteDaInscricaoDto>> Pedir(
        InscricaoAdministrativaDeTeste.Mundo mundo, DecisoesDeAcessoDeTeste persona, Guid processoId, Guid inscricaoId, int numero = 1)
    {
        MontadorDoComprovante montador = new(
            mundo.Processos, mundo.Codecs,
            new PrazoDaCorrecaoDoComprovante(
                Substitute.For<ICertameDivulgadoRepository>(), mundo.Processos, mundo.Codecs, DivulgacaoDeTeste.Fuso(), TimeProvider.System),
            new CancelamentoDoComprovante(DivulgacaoDeTeste.Fuso()));

        return EmitirComprovanteDaInscricaoQueryHandler.Handle(
            new EmitirComprovanteDaInscricaoQuery(processoId, inscricaoId, numero),
            persona.Verificador, mundo.Processos, mundo.Inscricoes, montador, CancellationToken.None);
    }

    [Fact(DisplayName = "Com consultar e emitir, a inscrição enviada sai sem cancelamento")]
    public async Task Enviada_SemCancelamento()
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(await Montar(cenario), ComAsDuas(), cenario.ProcessoId, cenario.InscricaoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.InscricaoId.Should().Be(cenario.InscricaoId);
        resultado.Value.Cancelamento.Should().BeNull();
    }

    [Fact(DisplayName = "A inscrição cancelada também sai, identificada como cancelada com a data e sem prazo de correção")]
    public async Task Cancelada_SaiComCancelamento()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, SituacaoInscricao.Cancelada)
            .CanceladaEm(CanceladaEm, "Mudei de curso");

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(await Montar(cenario), ComAsDuas(), cenario.ProcessoId, cenario.InscricaoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Cancelamento.Should().Be(new CancelamentoNoComprovanteDto(
            CanceladaEm, "Mudei de curso", "Documento sem validade: inscrição cancelada em 07/11/2026 às 22:32:05"));
        resultado.Value.PrazoDaCorrecao.Should().BeNull();
    }

    [Fact(DisplayName = "A cancelada sem motivo sai com o motivo nulo")]
    public async Task CanceladaSemMotivo_MotivoNulo()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, SituacaoInscricao.Cancelada)
            .CanceladaEm(CanceladaEm);

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(await Montar(cenario), ComAsDuas(), cenario.ProcessoId, cenario.InscricaoId);

        resultado.Value!.Cancelamento!.Motivo.Should().BeNull();
    }

    [Fact(DisplayName = "Consultar sem emitir recusa com 403 e não lê o comprovante")]
    public async Task SemEmitir_Recusa403_SemLerOComprovante()
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);
        InscricaoAdministrativaDeTeste.Mundo mundo = await Montar(cenario);

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(
            mundo, Diogo(PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar), cenario.ProcessoId, cenario.InscricaoId);

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.SemPermissaoNaUnidade);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterComprovanteAsync(default, default, default);
    }

    [Fact(DisplayName = "Emitir sem consultar as inscrições responde o 404 da inscrição que não se alcança, sem ler o comprovante")]
    public async Task SemConsultar_Responde404()
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);
        InscricaoAdministrativaDeTeste.Mundo mundo = await Montar(cenario);

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(
            mundo, Diogo(PermissaoSelecao.InscricoesEmitirComprovante), cenario.ProcessoId, cenario.InscricaoId);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.NaoEncontrada);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterComprovanteAsync(default, default, default);
    }

    [Theory(DisplayName = "A inscrição de outro processo e a inexistente respondem 404 mesmo a quem tem as duas permissões")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForaDoProcesso_Responde404(bool deOutroProcesso)
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);
        InscricaoAdministrativaDeTeste.Mundo mundo = await Montar(cenario);
        Guid processo = deOutroProcesso ? Guid.CreateVersion7() : cenario.ProcessoId;
        Guid inscricao = deOutroProcesso ? cenario.InscricaoId : Guid.CreateVersion7();

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(mundo, ComAsDuas(), processo, inscricao);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.NaoEncontrada);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterComprovanteAsync(default, default, default);
    }

    [Fact(DisplayName = "O comprovante que a inscrição não tem responde comprovante não encontrado")]
    public async Task NumeroInexistente_ComprovanteNaoEncontrado()
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);

        Result<ComprovanteDaInscricaoDto> resultado = await Pedir(await Montar(cenario), ComAsDuas(), cenario.ProcessoId, cenario.InscricaoId, numero: 9);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ComprovanteNaoEncontrado);
    }

    [Fact(DisplayName = "As duas decisões são exigidas sobre a inscrição pedida, consultar antes de emitir")]
    public async Task DecideSobreAInscricao_NaOrdem()
    {
        InscricaoAdministrativaDeTeste cenario = new(PersonasDeTeste.FernandaDrumond);
        DecisoesDeAcessoDeTeste diogo = ComAsDuas();

        await Pedir(await Montar(cenario), diogo, cenario.ProcessoId, cenario.InscricaoId);

        string[] pedidas = [.. diogo.Verificador.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.ExigirAsync))
            .Select(static c => ((PermissionRequirement)c.GetArguments()[0]!).Permissao)];
        pedidas.Should().Equal(PermissaoSelecao.InscricoesConsultar.Codigo, PermissaoSelecao.InscricoesEmitirComprovante.Codigo);
        await diogo.Verificador.Received(2).ExigirAsync(
            Arg.Any<PermissionRequirement>(), Arg.Is<ResourceContext?>(r => r!.InscricaoId == cenario.InscricaoId), Arg.Any<CancellationToken>());
    }
}
