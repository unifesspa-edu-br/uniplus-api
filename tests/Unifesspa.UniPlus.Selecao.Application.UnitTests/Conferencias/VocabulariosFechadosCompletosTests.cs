namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Conferencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Confere que a leitura pública anuncia os vocabulários aceitos pelo domínio.
/// </summary>
public sealed class VocabulariosFechadosCompletosTests
{
    [Fact(DisplayName = "Todo fundamento de isenção do enum, exceto a sentinela, é anunciado pela leitura pública")]
    public void FundamentosDoEnum_TodosAnunciados()
    {
        string[] noDominio =
        [
            .. Enum.GetValues<FundamentoIsencao>()
                .Where(static f => f != FundamentoIsencao.Nenhum)
                .Select(static f => f.ToCodigo()),
        ];

        string[] anunciados = [.. ListarFundamentosIsencaoQueryHandler
            .Handle(new ListarFundamentosIsencaoQuery())
            .Select(static f => f.Codigo)];

        anunciados.Should().BeEquivalentTo(noDominio,
            "um fundamento que o domínio aceita e a leitura não anuncia é um fundamento que o cliente nunca oferece");
    }

    [Fact(DisplayName = "Todo fundamento anunciado tem rótulo e descrição — nenhum entra na resposta como código cru")]
    public void FundamentosAnunciados_TemRotuloEDescricao()
    {
        ListarFundamentosIsencaoQueryHandler
            .Handle(new ListarFundamentosIsencaoQuery())
            .Should().AllSatisfy(f =>
            {
                f.Nome.Should().NotBeNullOrWhiteSpace();
                f.Descricao.Should().NotBeNullOrWhiteSpace();
            });
    }

    [Fact(DisplayName = "Todo campo permitido pelo domínio é anunciado pela leitura pública")]
    public void CamposPermitidos_TodosAnunciados()
    {
        string[] anunciados = [.. ListarCamposDivulgacaoQueryHandler
            .Handle(new ListarCamposDivulgacaoQuery())
            .Select(static c => c.Codigo)];

        anunciados.Should().BeEquivalentTo(
            ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo));
    }

}
