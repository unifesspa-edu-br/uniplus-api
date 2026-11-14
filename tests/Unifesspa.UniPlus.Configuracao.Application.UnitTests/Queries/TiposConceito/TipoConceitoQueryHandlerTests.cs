namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries.TiposConceito;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoConceitoQueryHandlerTests
{
    private readonly ITipoConceitoRepository _repository = Substitute.For<ITipoConceitoRepository>();

    [Fact(DisplayName = "Listar devolve os tipos como DTO, com os cursores de página")]
    public async Task Listar_DevolveOsDtosEOsCursores()
    {
        Guid proximo = Guid.NewGuid();
        IReadOnlyList<TipoConceito> itens = [TiposConceitoDeTeste.Numerico(), TiposConceitoDeTeste.Categorico()];
        _repository.ListarPaginadoAsync(null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns((itens, (Guid?)null, (Guid?)proximo));

        ListarTiposConceitoResult resultado = await ListarTiposConceitoQueryHandler.Handle(
            new ListarTiposConceitoQuery(null, 20, PaginationDirection.Next), _repository, CancellationToken.None);

        resultado.Items.Select(i => i.Codigo).Should().Equal("NOTA_ENTREVISTA", "PARECER_ANALISE");
        resultado.ProximoAfterId.Should().Be(proximo);
    }

    [Fact(DisplayName = "Obter devolve o tipo ativo como DTO")]
    public async Task Obter_TipoAtivo_DevolveODto()
    {
        TipoConceito tipo = TiposConceitoDeTeste.Categorico();
        _repository.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        TipoConceitoDto? dto = await ObterTipoConceitoPorIdQueryHandler.Handle(
            new ObterTipoConceitoPorIdQuery(tipo.Id), _repository, CancellationToken.None);

        dto!.Codigo.Should().Be("PARECER_ANALISE");
    }

    [Fact(DisplayName = "Obter não devolve o tipo desativado")]
    public async Task Obter_TipoDesativado_NaoDevolve()
    {
        TipoConceito tipo = TiposConceitoDeTeste.Numerico();
        tipo.Desativar();
        _repository.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        TipoConceitoDto? dto = await ObterTipoConceitoPorIdQueryHandler.Handle(
            new ObterTipoConceitoPorIdQuery(tipo.Id), _repository, CancellationToken.None);

        dto.Should().BeNull();
    }

    [Fact(DisplayName = "Obter não devolve o tipo inexistente")]
    public async Task Obter_TipoInexistente_NaoDevolve()
    {
        TipoConceitoDto? dto = await ObterTipoConceitoPorIdQueryHandler.Handle(
            new ObterTipoConceitoPorIdQuery(Guid.NewGuid()), _repository, CancellationToken.None);

        dto.Should().BeNull();
    }
}
