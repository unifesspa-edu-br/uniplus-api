namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// A revisão como quem lê a vê: os itens da concessão dele vêm marcados, e a listagem leva ao repositório a situação pedida,
/// para filtrar antes da paginação. Leandro Viana é o beneficiário; Marlene Martins conduz a revisão.
/// </summary>
public sealed class RevisoesDoLeitorEFiltroTests
{
    private static readonly Identidade Leandro = new(Emissor, "leandro.viana", "Leandro Viana");
    private static readonly Identidade Marlene = new(Emissor, "marlene.martins", "Marlene Martins");

    [Fact(DisplayName = "O item da concessão de quem lê vem marcado, e o dos outros não")]
    public void ToDto_ItemDoLeitor_Marcado()
    {
        RevisaoDetalhada detalhada = ComUmItemDe(Leandro);

        RevisaoDeAcessosDto paraLeandro = RevisaoMapping.ToDto(detalhada, new HashSet<Identidade>(), Leandro);
        RevisaoDeAcessosDto paraMarlene = RevisaoMapping.ToDto(detalhada, new HashSet<Identidade>(), Marlene);
        RevisaoDeAcessosDto semLeitor = RevisaoMapping.ToDto(detalhada, new HashSet<Identidade>(), null);

        (paraLeandro.Itens.Single().EhDoLeitor, paraMarlene.Itens.Single().EhDoLeitor, semLeitor.Itens.Single().EhDoLeitor).Should().Be((true, false, false));
    }

    [Theory(DisplayName = "A listagem leva ao repositório a situação pedida, ou nenhuma")]
    [InlineData(SituacaoDaRevisao.EmAndamento)]
    [InlineData(SituacaoDaRevisao.Concluida)]
    [InlineData(null)]
    public async Task Listar_LevaASituacaoAoRepositorio(SituacaoDaRevisao? situacao)
    {
        IRevisaoDeAcessosRepository revisoes = Substitute.For<IRevisaoDeAcessosRepository>();
        revisoes.ListarPaginadoAsync(default!, default, default, default, default).ReturnsForAnyArgs(([], null, null));

        await ListarRevisoesQueryHandler.Handle(
            new ListarRevisoesQuery(null, 10, PaginationDirection.Next, situacao), revisoes, new VerificadorDeTeste("marlene.martins", ["autorizacao:revisoes:conduzir"]), TimeProvider.System, CancellationToken.None);

        await revisoes.Received(1).ListarPaginadoAsync(Arg.Is<FiltroDeRevisoes>(f => f.Situacao == situacao), null, 10, PaginationDirection.Next, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O código da situação da revisão é lido sem distinguir caixa, e o desconhecido não vira situação")]
    [InlineData("em_andamento", SituacaoDaRevisao.EmAndamento)]
    [InlineData(" CONCLUIDA ", SituacaoDaRevisao.Concluida)]
    [InlineData("pendente", null)]
    [InlineData(null, null)]
    public void DoCodigo_LeOsCodigosPublicados(string? codigo, SituacaoDaRevisao? esperada) =>
        SituacaoDaRevisaoCodigo.DoCodigo(codigo).Should().Be(esperada);

    private static RevisaoDetalhada ComUmItemDe(Identidade beneficiario)
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional());
        Concessao concessao = ConcessaoBuilder.DoPerfil(perfil, new CatalogoInstitucional())
            .Para(beneficiario, "leandro-viana84@uniplus-test.local").ConcedidaPor(Marlene).Em(DateTimeOffset.UtcNow).Construir();
        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(Marlene, [concessao.Id], DateTimeOffset.UtcNow);

        return new RevisaoDetalhada(revisao, [new ItemDaRevisaoComConcessao(revisao.Itens[0], concessao, perfil.Nome, ConcessaoAtiva: true)]);
    }
}
