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
        leitura.Should().Throw<JsonException>();
    }

    [Fact]
    public void Conversor_ComTipoNumerico_FalhaNaLeitura()
    {
        string documento = (string)ColunaDosProcedimentosDeRecurso.Conversor.ConvertToProvider(Procedimentos())!;
        JsonNode json = JsonNode.Parse(documento)!;
        json[0]!["alvoTipo"] = 1;

        Action leitura = () => ColunaDosProcedimentosDeRecurso.Conversor.ConvertFromProvider(json.ToJsonString());
        leitura.Should().Throw<JsonException>();
    }

    internal static ProcedimentosDeRecurso Procedimentos() => ProcedimentosDeRecurso.De([
        Procedimento(AlvoDoProcedimentoDeRecurso.DoItem(Item)),
        Procedimento(AlvoDoProcedimentoDeRecurso.DaArea("MATEMATICA")),
    ]);

    private static ProcedimentoDeRecursoDeclarado Procedimento(AlvoDoProcedimentoDeRecurso alvo) =>
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, alvo,
            ReferenciaRegra.De(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), "v1", new string('a', 64)));
}
