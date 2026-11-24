namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.TiposConceito;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class CriarTipoConceitoCommandHandlerTests
{
    private readonly ITipoConceitoRepository _repository = Substitute.For<ITipoConceitoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private Task<Result<Guid>> Criar(CriarTipoConceitoCommand comando) =>
        CriarTipoConceitoCommandHandler.Handle(comando, _repository, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Código livre cria o tipo numérico, persiste e retorna o Id")]
    public async Task Handle_Numerico_CriaEPersiste()
    {
        Result<Guid> resultado = await Criar(TiposConceitoDeTeste.CriarNumerico());

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<TipoConceito>(tipo => tipo.Id == resultado.Value && tipo.EscalaMaxima == 10m && tipo.Natureza == FormaDoConceito.Numerico),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cria o tipo categórico com os valores na ordem do corpo")]
    public async Task Handle_Categorico_CriaComOsValoresEmOrdem()
    {
        await Criar(TiposConceitoDeTeste.CriarCategorico());

        await _repository.Received(1).AdicionarAsync(
            Arg.Is<TipoConceito>(tipo => tipo.Valores.Select(v => v.Codigo).SequenceEqual(new[] { "DEFERIDO", "INDEFERIDO" })),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente retorna conflito sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteAsync("NOTA_ENTREVISTA", Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await Criar(TiposConceitoDeTeste.CriarNumerico());

        resultado.Error!.Code.Should().Be(TipoConceitoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoConceito>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corpo inválido acumula as violações sem consultar a unicidade nem persistir")]
    public async Task Handle_CorpoInvalido_AcumulaSemConsultarNemPersistir()
    {
        Result<Guid> resultado = await Criar(TiposConceitoDeTeste.CriarNumerico() with
        {
            Codigo = "",
            Definicao = TiposConceitoDeTeste.CriarNumerico().Definicao! with { Nome = "" },
        });

        resultado.Errors.Select(e => e.Field).Should().Equal("codigo", "definicao.nome");
        await _repository.DidNotReceive().CodigoExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Valor nulo na lista do corpo volta como erro do índice dele, sem derrubar a requisição")]
    public async Task Handle_ValorNulo_VoltaComoErroDoIndice()
    {
        CriarTipoConceitoCommand categorico = TiposConceitoDeTeste.CriarCategorico();
        CriarTipoConceitoCommand comando = categorico with
        {
            Definicao = categorico.Definicao! with { Valores = [new ValorCategoricoCommand("DEFERIDO", "Deferido", true), null!] },
        };

        Result<Guid> resultado = await Criar(comando);

        resultado.Errors.Select(e => e.Field).Should().Equal("definicao.valores[1].codigo", "definicao.valores[1].rotulo", "definicao.valores[1].contaComoAprovacao");
    }

    [Fact(DisplayName = "Corrida no índice único do código vira conflito e descarta o rastreamento")]
    public async Task Handle_CorridaNoIndiceUnico_TraduzEDescartaRastreamento()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética de teste", PostgresExceptionFactory.Create("23505", "ix_tipos_conceito_codigo")));

        Result<Guid> resultado = await Criar(TiposConceitoDeTeste.CriarNumerico());

        resultado.Error!.Code.Should().Be(TipoConceitoErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outra constraint não é engolida")]
    public async Task Handle_OutraViolacaoDeConstraint_Propaga()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética de teste", PostgresExceptionFactory.Create("23505", "outra_constraint")));

        Func<Task> act = () => Criar(TiposConceitoDeTeste.CriarNumerico());

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
