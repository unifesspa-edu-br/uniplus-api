namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeTiposDeFaseTests
{
    private const string DocumentoComTresFases = """
        { "cronograma": { "fases": [
            { "chave": "a", "tipoFaseCodigo": "INSCRICAO" },
            { "chave": "b", "tipoFaseCodigo": "INSCRICAO" },
            { "chave": "c", "tipoFaseCodigo": "FASE_DESATIVADA" } ] } }
        """;

    private readonly IFaseCanonicaReader _fases = Substitute.For<IFaseCanonicaReader>();

    public DescricaoDeTiposDeFaseTests() =>
        _fases.ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new FaseCanonicaView(Guid.CreateVersion7(), "INSCRICAO", "Inscrição", null, false, false, null, true, false, false, "NO_EDITAL")]);

    [Fact(DisplayName = "O nome do tipo de fase entra logo depois do código, e o cadastro é lido uma vez com os códigos distintos")]
    public async Task Descrever_FasesComTipoConhecido_TrazONomeAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTresFases, new DescricaoDeTiposDeFase(_fases));

        JsonArray fases = documento["cronograma"]!["fases"]!.AsArray();
        DocumentoDescritoDeTeste.Chaves(fases[0]).Should().Equal("chave", "tipoFaseCodigo", "tipoFaseNome");
        fases[0]!["tipoFaseNome"]!.GetValue<string>().Should().Be("Inscrição");
        fases[1]!["tipoFaseNome"]!.GetValue<string>().Should().Be("Inscrição");
        await _fases.Received(1).ListarVivasPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 2 && codigos.Contains("INSCRICAO") && codigos.Contains("FASE_DESATIVADA")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de fase que o cadastro não devolve vivo fica sem nome, e as demais fases seguem descritas")]
    public async Task Descrever_TipoDeFaseSemCadastroVivo_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTresFases, new DescricaoDeTiposDeFase(_fases));

        JsonArray fases = documento["cronograma"]!["fases"]!.AsArray();
        fases[2]!["tipoFaseNome"].Should().BeNull();
        DocumentoDescritoDeTeste.Chaves(fases[2]).Should().Equal("chave", "tipoFaseCodigo");
    }

    [Fact(DisplayName = "O nome que o esquema não declara só de leitura não é escrito")]
    public async Task Descrever_NomeNaoDeclaradoSomenteLeitura_NaoEscreve()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(
            DocumentoComTresFases, FormaDosDescritivosDeTeste.Documento(somenteLeitura: false), new DescricaoDeTiposDeFase(_fases));

        documento["cronograma"]!["fases"]![0]!["tipoFaseNome"].Should().BeNull();
    }
}
