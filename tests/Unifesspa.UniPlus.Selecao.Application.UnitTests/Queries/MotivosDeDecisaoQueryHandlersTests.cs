namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>A leitura do cadastro de motivos: o filtro chega ao repositório e a projeção sai em códigos do wire.</summary>
public sealed class MotivosDeDecisaoQueryHandlersTests
{
    private readonly IMotivoDeDecisaoRepository _repository = Substitute.For<IMotivoDeDecisaoRepository>();

    [Fact(DisplayName = "A listagem repassa o filtro e projeta o tipo e o sentido com código e rótulo e a subclassificação")]
    public async Task Listar_RepassaFiltroEProjeta()
    {
        MotivoDeDecisao motivo = Motivo();
        FiltroDosMotivos filtro = new(TipoMotivoDeDecisao.IsencaoDaTaxa, "CADASTRO_UNICO", ApenasAtivos: true);

        ListarMotivosDeDecisaoResult resultado = await ListarAsync(filtro, motivo);

        resultado.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            motivo.Id,
            Tipo = CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa),
            Codigo = "RENDA_ACIMA_DO_LIMITE",
            Subclassificacao = "CADASTRO_UNICO",
            Sentido = new { Codigo = "FAVORAVEL", Rotulo = "favorável" },
            Ativo = true,
        });
    }

    [Fact(DisplayName = "A leitura por Id recusa com o código de motivo não encontrado para motivo inexistente")]
    public async Task Obter_Inexistente_RecusaComMotivoNaoEncontrado()
    {
        Result<MotivoDeDecisaoDto> lido = await ObterMotivoDeDecisaoQueryHandler.Handle(
            new ObterMotivoDeDecisaoQuery(Guid.CreateVersion7()), _repository, CancellationToken.None);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "A leitura por Id projeta o motivo")]
    public async Task Obter_Existente_Projeta()
    {
        MotivoDeDecisao motivo = Motivo();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        Result<MotivoDeDecisaoDto> resultado = await ObterMotivoDeDecisaoQueryHandler.Handle(
            new ObterMotivoDeDecisaoQuery(motivo.Id), _repository, CancellationToken.None);

        MotivoDeDecisaoDto lido = resultado.Value!;
        lido.Codigo.Should().Be("RENDA_ACIMA_DO_LIMITE");
        lido.Tipo.Should().Be(CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa));
    }

    private async Task<ListarMotivosDeDecisaoResult> ListarAsync(FiltroDosMotivos filtro, MotivoDeDecisao motivo)
    {
        _repository.ListarPaginadoAsync(null, 20, PaginationDirection.Next, filtro, Arg.Any<CancellationToken>())
            .Returns(([motivo], (Guid?)null, (Guid?)null));

        return await ListarMotivosDeDecisaoQueryHandler.Handle(
            new ListarMotivosDeDecisaoQuery(null, 20, PaginationDirection.Next, filtro), _repository, CancellationToken.None);
    }

    private static MotivoDeDecisao Motivo() =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), "RENDA_ACIMA_DO_LIMITE", "Renda dentro do limite legal.", "CADASTRO_UNICO", SentidoDoMotivo.Favoravel)).Value!;
}
