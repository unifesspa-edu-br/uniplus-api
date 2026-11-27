namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os cadastros vivos da distribuição pela Lei de Cotas, no lugar das modalidades e das regras de <see cref="CadastrosDasVagasDoRascunhoDeTeste"/>
/// (que cadastra as ofertas e deve ter sido chamado antes): a ampla concorrência com as oito modalidades federais,
/// as regras de distribuição e de ajuste e a referência demográfica, cujo percentual de pessoas pretas, pardas e indígenas o teste troca.
/// </summary>
internal static class CadastrosDaLeiDeCotasDoRascunhoDeTeste
{
    public const string ReferenciaDemografica = "2022";

    private static readonly string RegraDeDistribuicao = RegraDeDistribuicaoDeVagas.Lei12711.Codigo();
    private static readonly string RegraDeAjuste = RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo();

    public static void Cadastrar(CenarioDoRascunho cenario, decimal percentualDePessoasPretasPardasEIndigenas)
    {
        ArgumentNullException.ThrowIfNull(cenario);

        CadastrosDasVagasDoRascunhoDeTeste.CadastrarModalidades(cenario, Modalidades());
        cenario.Regras.ListarPorReferenciasAsync(default!, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<RegraCatalogo>>([Regra(RegraDeDistribuicao, TipoRegra.RegraDistribuicaoVagas), Regra(RegraDeAjuste, TipoRegra.RegraAjusteDistribuicaoVagas)]));
        TrocarOPercentual(cenario, percentualDePessoasPretasPardasEIndigenas);
    }

    /// <summary>Troca a referência demográfica, como se o cadastro tivesse mudado depois da leitura anterior.</summary>
    public static void TrocarOPercentual(CenarioDoRascunho cenario, decimal percentualDePessoasPretasPardasEIndigenas)
    {
        ArgumentNullException.ThrowIfNull(cenario);

        cenario.ReferenciasDemograficas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<ReferenciaReservaDemograficaView>>(
                [new(Guid.CreateVersion7(), ReferenciaDemografica, percentualDePessoasPretasPardasEIndigenas, 10m, 10m, "Censo de teste")]));
    }

    public static string Distribuicao(string ofertaCodigo, int vagasOfertadas) =>
        $$"""
        { "ofertaCursoCodigo": "{{ofertaCodigo}}", "vagasOfertadasBase": {{vagasOfertadas}}, "percentualDaReservaLegal": 0.5,
          "regraDistribuicao": { "codigo": "{{RegraDeDistribuicao}}", "versao": "v1" },
          "regraAjuste": { "codigo": "{{RegraDeAjuste}}", "versao": "v1" },
          "referenciaDemograficaCodigo": "{{ReferenciaDemografica}}",
          "modalidades": [ {{string.Join(", ", ModalidadesFederaisLei12711.CodigosComAc.Select(static codigo => $$"""{ "codigo": "{{codigo}}" }"""))}} ] }
        """;

    private static ModalidadeView[] Modalidades() =>
    [
        .. ModalidadesFederaisLei12711.Codigos.Select(static codigo => new ModalidadeView(
            Guid.CreateVersion7(), codigo, null, "COTA_RESERVADA", "DENTRO_DA_RESERVA_LEGAL", null, "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(CatalogoDe<NaturezaLegalModalidade>.Obter("COTA_RESERVADA")), null, "Lei de teste")),
        new ModalidadeView(
            Guid.CreateVersion7(), ModalidadesFederaisLei12711.Ac, "Ampla concorrência", "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS", null, null, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(CatalogoDe<NaturezaLegalModalidade>.Obter("AMPLA")), null, "Lei de teste"),
    ];

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo)
    {
        using JsonDocument vazio = JsonDocument.Parse("{}");
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(codigo, "v1", tipo, vazio.RootElement.Clone(), invariantes.RootElement.Clone(), "Regra de teste");
    }
}
