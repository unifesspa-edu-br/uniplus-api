namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DescricaoDeTiposDeEtapaTests
{
    private const string DocumentoComEtapas = """
        { "etapas": [
            { "chave": "e1", "tipoEtapaCodigo": "PROVA_OBJETIVA" },
            { "chave": "e2", "tipoEtapaCodigo": "TIPO_DESATIVADO" },
            { "chave": "e3", "tipoEtapaCodigo": "PROVA_OBJETIVA" } ] }
        """;

    private readonly ITipoEtapaReader _tipos = Substitute.For<ITipoEtapaReader>();

    public DescricaoDeTiposDeEtapaTests() =>
        _tipos.ListarAtivosAsync(Arg.Any<CancellationToken>()).Returns(
        [
            TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "PROVA_OBJETIVA").Chamado("Prova objetiva").Construir(),
            TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "REDACAO").Chamado("Redação").Construir(),
        ]);

    [Fact(DisplayName = "O nome do tipo de etapa entra logo depois do código, e o cadastro de tipos ativos é lido uma vez")]
    public async Task Descrever_EtapasComTipoAtivo_TrazONomeAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComEtapas, new DescricaoDeTiposDeEtapa(_tipos));

        JsonArray etapas = documento["etapas"]!.AsArray();
        DocumentoDescritoDeTeste.Chaves(etapas[0]).Should().Equal("chave", "tipoEtapaCodigo", "tipoEtapaNome");
        etapas[0]!["tipoEtapaNome"]!.GetValue<string>().Should().Be("Prova objetiva");
        etapas[2]!["tipoEtapaNome"]!.GetValue<string>().Should().Be("Prova objetiva");
        await _tipos.Received(1).ListarAtivosAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de etapa que não está entre os ativos fica sem nome")]
    public async Task Descrever_TipoDeEtapaInativo_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComEtapas, new DescricaoDeTiposDeEtapa(_tipos));

        documento["etapas"]![1]!["tipoEtapaNome"].Should().BeNull();
    }

    [Fact(DisplayName = "O tipo de etapa ativo que o documento não cita não aparece no documento")]
    public async Task Descrever_TipoAtivoNaoCitado_NaoEntraNoDocumento()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComEtapas, new DescricaoDeTiposDeEtapa(_tipos));

        documento.ToJsonString().Should().NotContain("Redação");
    }
}
