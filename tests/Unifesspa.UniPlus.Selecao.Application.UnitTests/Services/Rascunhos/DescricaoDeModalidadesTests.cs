namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeModalidadesTests
{
    private const string DocumentoComModalidades = """
        { "distribuicaoVagas": [ { "ofertaCursoCodigo": "MEDICINA_MARABA", "modalidades": [
            { "codigo": "AMPLA_CONCORRENCIA", "quantidadeDeclarada": 10 },
            { "codigo": "SEM_DESCRICAO" },
            { "codigo": "MODALIDADE_REMOVIDA" } ] } ] }
        """;

    private readonly IModalidadeReader _modalidades = Substitute.For<IModalidadeReader>();

    public DescricaoDeModalidadesTests() =>
        _modalidades.ListarVivosAsync(Arg.Any<CancellationToken>()).Returns(
            [Modalidade("AMPLA_CONCORRENCIA", "Vagas sem reserva"), Modalidade("SEM_DESCRICAO", null)]);

    private static ModalidadeView Modalidade(string codigo, string? descricao) => new(
        Guid.CreateVersion7(), codigo, descricao, "AMPLA", "VAGAS_PROPRIAS", null, null, null, null, null, PredicadoDnf.SemClausula, null, null);

    [Fact(DisplayName = "A descrição da modalidade entra logo depois do código, sem alterar a quantidade declarada")]
    public async Task Descrever_ModalidadeComDescricao_TrazADescricaoAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComModalidades, new DescricaoDeModalidades(_modalidades));

        JsonNode? modalidade = documento["distribuicaoVagas"]![0]!["modalidades"]![0];
        DocumentoDescritoDeTeste.Chaves(modalidade).Should().Equal("codigo", "descricao", "quantidadeDeclarada");
        modalidade!["descricao"]!.GetValue<string>().Should().Be("Vagas sem reserva");
        modalidade["quantidadeDeclarada"]!.GetValue<int>().Should().Be(10);
    }

    [Fact(DisplayName = "A modalidade sem descrição no cadastro, ou que ele não devolve viva, fica sem o campo")]
    public async Task Descrever_ModalidadeSemDescricaoOuSemCadastroVivo_OmiteOCampo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComModalidades, new DescricaoDeModalidades(_modalidades));

        JsonArray modalidades = documento["distribuicaoVagas"]![0]!["modalidades"]!.AsArray();
        DocumentoDescritoDeTeste.Chaves(modalidades[1]).Should().Equal("codigo");
        DocumentoDescritoDeTeste.Chaves(modalidades[2]).Should().Equal("codigo");
    }

    [Fact(DisplayName = "O cadastro de modalidades é lido uma vez para o documento inteiro")]
    public async Task Descrever_VariasModalidades_LeOCadastroUmaVez()
    {
        await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComModalidades, new DescricaoDeModalidades(_modalidades));

        await _modalidades.Received(1).ListarVivosAsync(Arg.Any<CancellationToken>());
    }
}
