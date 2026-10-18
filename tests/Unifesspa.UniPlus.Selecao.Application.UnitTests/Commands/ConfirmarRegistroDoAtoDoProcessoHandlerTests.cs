namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConfirmarRegistroDoAtoDoProcessoHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly IAtoDoProcessoRepository _repositorio = Substitute.For<IAtoDoProcessoRepository>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

    private static AtoDoProcesso NovoPendente() => AtoDoProcesso.Registrar(
        new NovoAtoDoProcesso(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "AVISO", "Aviso", "3/2026", new DateOnly(2026, 12, 10),
            "Diretor do CEPS", "ceps", "psm-medicina-2027", Guid.CreateVersion7(), []),
        new RelogioFixo(Agora)).Value!;

    private Task Entregar(Guid atoId) => ConfirmarRegistroDoAtoDoProcessoHandler.Handle(
        new AtoNormativoRegistrado(atoId, 2026, "AVISO"),
        _repositorio,
        _unitOfWork,
        new RelogioFixo(Agora.AddMinutes(2)),
        CancellationToken.None);

    [Fact(DisplayName = "A confirmação do ato passa o registro pendente a registrado e grava")]
    public async Task Handle_RegistroPendente_PassaARegistrado()
    {
        AtoDoProcesso ato = NovoPendente();
        _repositorio.ObterParaMutacaoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);

        await Entregar(ato.Id);

        ato.Situacao.Should().Be(SituacaoDoRegistroDoAto.Registrado);
        ato.RegistradoEm.Should().Be(Agora.AddMinutes(2));
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A entrega repetida da confirmação não muda o registro nem grava")]
    public async Task Handle_RegistroJaRegistrado_NaoGrava()
    {
        AtoDoProcesso ato = NovoPendente();
        ato.ConfirmarRegistro(new RelogioFixo(Agora.AddMinutes(1)));
        _repositorio.ObterParaMutacaoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);

        await Entregar(ato.Id);

        ato.RegistradoEm.Should().Be(Agora.AddMinutes(1));
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ato sem registro na Seleção é ignorado")]
    public async Task Handle_AtoSemRegistro_Ignora()
    {
        await Entregar(Guid.CreateVersion7());

        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
