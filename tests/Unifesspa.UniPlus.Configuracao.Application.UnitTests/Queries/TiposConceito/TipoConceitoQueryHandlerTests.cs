namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries.TiposConceito;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoConceitoQueryHandlerTests
{
    private readonly ITipoConceitoRepository _repository = Substitute.For<ITipoConceitoRepository>();

    [Fact(DisplayName = "Listar devolve os tipos como DTO, com os cursores de página")]
    public async Task Listar_DevolveOsDtosEOsCursores()
    {
        (string SortKey, Guid Id) proximo = ("chave", Guid.NewGuid());
        IReadOnlyList<TipoConceito> itens = [TiposConceitoDeTeste.Numerico(), TiposConceitoDeTeste.Categorico()];
        _repository.ListarPaginadoAsync(null, null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns((itens, ((string SortKey, Guid Id)?)null, ((string SortKey, Guid Id)?)proximo));

        ListarTiposConceitoResult resultado = await ListarTiposConceitoQueryHandler.Handle(
            new ListarTiposConceitoQuery(null, null, 20, PaginationDirection.Next), _repository, CancellationToken.None);

        resultado.Items.Select(i => i.Codigo).Should().Equal("NOTA_ENTREVISTA", "PARECER_ANALISE");
        resultado.Proximo.Should().Be(proximo);
    }

    [Fact(DisplayName = "Obter devolve o tipo ativo como DTO")]
    public async Task Obter_TipoAtivo_DevolveODto()
    {
        TipoConceito tipo = TiposConceitoDeTeste.Categorico();
        _repository.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result<TipoConceitoDto> resultado = await ObterTipoConceitoPorIdQueryHandler.Handle(
            new ObterTipoConceitoPorIdQuery(tipo.Id), _repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be("PARECER_ANALISE");
    }

    [Fact(DisplayName = "Obter recusa o tipo desativado com o erro de tipo de conceito não encontrado")]
    public async Task Obter_TipoDesativado_NaoDevolve()
    {
        TipoConceito tipo = TiposConceitoDeTeste.Numerico();
        tipo.Desativar();
        _repository.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result<TipoConceitoDto> resultado = await ObterTipoConceitoPorIdQueryHandler.Handle(
            new ObterTipoConceitoPorIdQuery(tipo.Id), _repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoConceitoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Obter recusa o tipo inexistente com o erro de tipo de conceito não encontrado")]
    public async Task Obter_TipoInexistente_NaoDevolve()
    {
        Result<TipoConceitoDto> resultado = await ObterTipoConceitoPorIdQueryHandler.Handle(
            new ObterTipoConceitoPorIdQuery(Guid.NewGuid()), _repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoConceitoErrorCodes.NaoEncontrado);
    }
}
