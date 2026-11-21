namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>A leitura do cadastro de motivos: o filtro chega ao repositório e a projeção sai em códigos do wire.</summary>
public sealed class MotivosDeDecisaoQueryHandlersTests
{
    private readonly IMotivoDeDecisaoRepository _repository = Substitute.For<IMotivoDeDecisaoRepository>();

    [Fact(DisplayName = "A listagem repassa o filtro e projeta o tipo com código e rótulo, a subclassificação e o sentido como código")]
    public async Task Listar_RepassaFiltroEProjeta()
    {
        MotivoDeDecisao motivo = Motivo();
        FiltroDosMotivos filtro = new(TipoMotivoDeDecisao.IsencaoDaTaxa, FundamentoIsencaoCodigo.CadastroUnico, ApenasAtivos: true);

        ListarMotivosDeDecisaoResult resultado = await ListarAsync(filtro, motivo);

        resultado.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            motivo.Id,
            Tipo = CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa),
            Codigo = "RENDA_ACIMA_DO_LIMITE",
            Subclassificacao = FundamentoIsencaoCodigo.CadastroUnico,
            Sentido = SentidoDoMotivoCodigo.Favoravel,
            Ativo = true,
        });
    }

    [Fact(DisplayName = "A leitura por Id devolve nulo para motivo inexistente")]
    public async Task Obter_Inexistente_Nulo()
    {
        MotivoDeDecisaoDto? lido = await ObterMotivoDeDecisaoQueryHandler.Handle(
            new ObterMotivoDeDecisaoQuery(Guid.CreateVersion7()), _repository, CancellationToken.None);

        lido.Should().BeNull();
    }

    [Fact(DisplayName = "A leitura por Id projeta o motivo")]
    public async Task Obter_Existente_Projeta()
    {
        MotivoDeDecisao motivo = Motivo();
        _repository.ObterPorIdAsync(motivo.Id, Arg.Any<CancellationToken>()).Returns(motivo);

        MotivoDeDecisaoDto? lido = await ObterMotivoDeDecisaoQueryHandler.Handle(
            new ObterMotivoDeDecisaoQuery(motivo.Id), _repository, CancellationToken.None);

        lido!.Codigo.Should().Be("RENDA_ACIMA_DO_LIMITE");
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
            TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), "RENDA_ACIMA_DO_LIMITE", "Renda dentro do limite legal.", FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel)).Value!;
}
