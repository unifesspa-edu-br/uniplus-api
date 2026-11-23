namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

public sealed class SerializacaoDosProcedimentosDeRecursoTests
{
    [Fact]
    public void Serializar_ComAmbosOsAlvos_MantemBytesEHashAnteriores()
    {
        ProcedimentosDeRecurso procedimentos = ColunaDosProcedimentosDeRecursoTests.Procedimentos();
        byte[] atual = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["procedimentos"] = SerializacaoDosProcedimentosDeRecurso.Serializar(procedimentos) });
        byte[] anterior = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["procedimentos"] = SerializacaoAnterior(procedimentos) });

        atual.Should().Equal(anterior);
        HashCanonicalComputer.ComputeSha256Hex(atual).Should().Be(HashCanonicalComputer.ComputeSha256Hex(anterior));
    }

    private static JsonArray SerializacaoAnterior(ProcedimentosDeRecurso procedimentos) =>
        new([.. procedimentos.Itens.Select(static p => (JsonNode)new JsonObject
        {
            ["tipoDeDecisao"] = p.TipoDeDecisao.Codigo(),
            ["alvo"] = new JsonObject
            {
                ["tipo"] = p.Alvo!.Tipo == TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao ? "item_avaliacao" : "area_enem",
                ["valor"] = p.Alvo.Valor,
            },
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(p.Regra),
        })]);
}
