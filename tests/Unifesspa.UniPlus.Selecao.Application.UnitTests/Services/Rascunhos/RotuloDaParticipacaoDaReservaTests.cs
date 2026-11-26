namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A exportação do rascunho traz, ao lado da participação da reserva de cada chamada, o rótulo atual dela.</summary>
public sealed class RotuloDaParticipacaoDaReservaTests
{
    private const string CadastroDeReserva = """
        { "cadastroDeReserva": [
          { "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" },
          { "numeroDaChamada": 2, "pessoasDaReservaPorVaga": 0 },
          { "numeroDaChamada": 3, "pessoasDaReservaPorVaga": 1, "participacaoDaReserva": "CODIGO_QUE_NAO_EXISTE" } ] }
        """;

    [Fact(DisplayName = "A descrição devolve o rótulo de cada código que o catálogo conhece e omite o desconhecido")]
    public async Task Descricao_DevolveORotuloDosCodigosConhecidos()
    {
        (string Codigo, string? Complemento)[] codigos = [("ANTECIPA_A_HABILITACAO", null), ("CODIGO_QUE_NAO_EXISTE", null)];

        IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> rotulos = await new DescricaoDaParticipacaoDaReserva().LerAsync(codigos, CancellationToken.None);

        rotulos.Should().ContainSingle().Which.Value.Should().Equal("A reserva só antecipa a habilitação");
    }

    [Fact(DisplayName = "A exportação escreve o rótulo logo depois da participação, só na chamada que a declara com código conhecido")]
    public async Task Exportacao_EscreveORotuloAoLadoDaParticipacao()
    {
        CenarioDoRascunho cenario = new();
        RascunhoDeProcessoSeletivo rascunho = cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, CadastroDeReserva);

        JsonObject exportado = await cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        JsonArray chamadas = exportado["cadastroDeReserva"]!.AsArray();
        chamadas[0]!.AsObject().Select(static propriedade => propriedade.Key).Should().Equal(
            "numeroDaChamada", "pessoasDaReservaPorVaga", "participacaoDaReserva", "participacaoDaReservaRotulo");
        chamadas[0]!["participacaoDaReservaRotulo"]!.GetValue<string>().Should().Be("A reserva assume na mesma chamada");
        chamadas[1]!["participacaoDaReservaRotulo"].Should().BeNull();
        chamadas[2]!["participacaoDaReservaRotulo"].Should().BeNull();
    }
}
