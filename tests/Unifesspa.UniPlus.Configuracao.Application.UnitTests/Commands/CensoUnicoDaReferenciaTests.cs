namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CensoUnicoDaReferenciaTests
{
    private readonly IReferenciaReservaDemograficaRepository _repository =
        Substitute.For<IReferenciaReservaDemograficaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static ReferenciaReservaDemografica Existente() =>
        ReferenciaReservaDemografica.Criar("2022", 78.50m, 1.20m, 8.40m, "Lei 12.711/2012, art. 10, III").ValorEsperado();

    [Fact(DisplayName = "Censo que já pertence a outra referência viva é recusado com CensoJaExiste")]
    public async Task Conferir_CensoJaExistente_Recusa()
    {
        Guid editada = Guid.NewGuid();
        _repository.CensoExisteEntreLivosAsync("2010", editada, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await CensoUnicoDaReferencia.ConferirAsync("2010", editada, _repository, CancellationToken.None);

        resultado.Error?.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste);
    }

    [Fact(DisplayName = "Censo livre entre as referências vivas é aceito")]
    public async Task Conferir_CensoLivre_Aceita()
    {
        Result resultado = await CensoUnicoDaReferencia.ConferirAsync("2010", null, _repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Na edição, Censo igual ao atual não consulta o repositório")]
    public async Task ConferirNaEdicao_CensoIgualAoAtual_NaoConsulta()
    {
        Result resultado = await CensoUnicoDaReferencia.ConferirNaEdicaoAsync(
            Existente(), " 2022 ", _repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive().CensoExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Na edição, Censo não informado não consulta o repositório")]
    public async Task ConferirNaEdicao_CensoAusente_NaoConsulta()
    {
        Result resultado = await CensoUnicoDaReferencia.ConferirNaEdicaoAsync(
            Existente(), null, _repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive().CensoExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Na edição, Censo novo é consultado excluindo a própria referência")]
    public async Task ConferirNaEdicao_CensoNovo_ConsultaExcluindoAPropria()
    {
        ReferenciaReservaDemografica referencia = Existente();
        _repository.CensoExisteEntreLivosAsync("2010", referencia.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await CensoUnicoDaReferencia.ConferirNaEdicaoAsync(
            referencia, "2010", _repository, CancellationToken.None);

        resultado.Error?.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste);
    }

    [Fact(DisplayName = "Gravar com sucesso salva as alterações")]
    public async Task Gravar_SemConflito_Salva()
    {
        Result resultado = await CensoUnicoDaReferencia.GravarAsync(_unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida no índice único do Censo vira CensoJaExiste e descarta as alterações")]
    public async Task Gravar_ViolacaoDoIndiceDoCenso_RecusaEDescarta()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.ReferenciaDemograficaCensoAtivos)));

        Result resultado = await CensoUnicoDaReferencia.GravarAsync(_unitOfWork, CancellationToken.None);

        resultado.Error?.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outro índice único propaga sem descartar")]
    public async Task Gravar_OutraConstraint_Propaga()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("outra", PostgresExceptionFactory.Create("23505", "ix_outro")));

        Func<Task> acao = () => CensoUnicoDaReferencia.GravarAsync(_unitOfWork, CancellationToken.None);

        await acao.Should().ThrowAsync<DbUpdateException>();
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }
}
