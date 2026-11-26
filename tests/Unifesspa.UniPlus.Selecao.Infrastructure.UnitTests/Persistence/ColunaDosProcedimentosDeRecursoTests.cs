namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ColunaDosProcedimentosDeRecursoTests
{
    private static readonly Guid Item = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Conversor_ComAmbosOsAlvos_GravaCodigosERestaura()
    {
        ProcedimentosDeRecurso original = Procedimentos();
        string documento = (string)ColunaDosProcedimentosDeRecurso.Conversor.ConvertToProvider(original)!;
        ProcedimentosDeRecurso lido = (ProcedimentosDeRecurso)ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider(documento)!;

        using JsonDocument json = JsonDocument.Parse(documento);
        json.RootElement.EnumerateArray().Select(static p => p.GetProperty("alvoTipo").GetString())
            .Should().BeEquivalentTo("ITEM_AVALIACAO", "AREA_ENEM");
        lido.MesmoConteudo(original).Should().BeTrue();
    }

    [Theory]
    [InlineData("desconhecido")]
    [InlineData("1")]
    public void Conversor_ComTipoDesconhecido_FalhaNaLeitura(string tipo)
    {
        string documento = (string)ColunaDosProcedimentosDeRecurso.Conversor.ConvertToProvider(Procedimentos())!;
        JsonNode json = JsonNode.Parse(documento)!;
        json[0]!["alvoTipo"] = tipo;

        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider(json.ToJsonString());
        leitura.DeveRecusarComoDocumentoInvalido("procedimentos_de_recurso");
    }

    [Fact]
    public void Conversor_ComTipoNumerico_FalhaNaLeitura()
    {
        string documento = (string)ColunaDosProcedimentosDeRecurso.Conversor.ConvertToProvider(Procedimentos())!;
        JsonNode json = JsonNode.Parse(documento)!;
        json[0]!["alvoTipo"] = 1;

        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider(json.ToJsonString());
        leitura.DeveRecusarComoDocumentoInvalido("procedimentos_de_recurso");
    }

    [Fact(DisplayName = "Alvo de item com identificador inválido é recusado sem repetir o valor gravado")]
    public void Conversor_ComIdentificadorDeItemInvalido_RecusaSemRepetirOValor()
    {
        const string valorGravado = "valor-sigiloso-gravado";
        string documento = (string)ColunaDosProcedimentosDeRecurso.Conversor.ConvertToProvider(Procedimentos())!;
        JsonNode json = JsonNode.Parse(documento)!;
        json[0]!["alvoValor"] = valorGravado;

        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider(json.ToJsonString());

        InvalidOperationException erro = leitura.Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Contain("procedimentos_de_recurso").And.Contain("valor fora do formato esperado")
            .And.NotContain(valorGravado);
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "Procedimentos nulos são recusados na leitura")]
    public void Conversor_JsonNulo_RecusaDadoGravado()
    {
        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider("null");
        leitura.Should().Throw<InvalidOperationException>().WithMessage("*procedimentos_de_recurso*");
    }

    [Fact(DisplayName = "Lista vazia de procedimentos é aceita")]
    public void Conversor_ListaVazia_RetornaColecaoVazia()
    {
        var lido = (ProcedimentosDeRecurso)ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider("[]")!;
        lido.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Nome de propriedade com outra caixa é recusado")]
    public void Conversor_PropriedadeComOutraCaixa_RecusaJson()
    {
        const string documento = """[{"TipoDeDecisao":"CLASSIFICACAO","alvoTipo":null,"alvoValor":null,"regraCodigo":"RECALCULO","regraVersao":"v1","regraHash":"abc"}]""";
        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider(documento);
        leitura.DeveRecusarComoDocumentoInvalido("procedimentos_de_recurso");
    }

    [Fact(DisplayName = "JSON malformado de procedimentos é recusado")]
    public void Conversor_JsonMalformado_RecusaJson()
    {
        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider("[");
        leitura.DeveRecusarComoDocumentoInvalido("procedimentos_de_recurso");
    }

    internal static ProcedimentosDeRecurso Procedimentos() => ProcedimentosDeRecurso.De([
        Procedimento(AlvoDoProcedimentoDeRecurso.DoItem(Item)),
        Procedimento(AlvoDoProcedimentoDeRecurso.DaArea("MATEMATICA")),
    ]);

    private static ProcedimentoDeRecursoDeclarado Procedimento(AlvoDoProcedimentoDeRecurso alvo) =>
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, alvo,
            ReferenciaRegra.De(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), "v1", new string('a', 64)));
}
