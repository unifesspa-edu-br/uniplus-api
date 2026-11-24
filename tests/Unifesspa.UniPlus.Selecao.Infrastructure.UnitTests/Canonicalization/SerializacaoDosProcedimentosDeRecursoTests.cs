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
    public void Serializar_ComAmbosOsAlvos_EscreveContextoEMantemOsCodigos()
    {
        ProcedimentosDeRecurso procedimentos = ColunaDosProcedimentosDeRecursoTests.Procedimentos();
        JsonArray escritos = SerializacaoDosProcedimentosDeRecurso.Serializar(procedimentos);
        ConferirContextosERemover(escritos, procedimentos);
        byte[] atual = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["procedimentos"] = escritos });
        byte[] anterior = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["procedimentos"] = SerializacaoAnterior(procedimentos) });

        atual.Should().Equal(anterior);
        HashCanonicalComputer.ComputeSha256Hex(atual).Should().Be(HashCanonicalComputer.ComputeSha256Hex(anterior));
    }

    private static void ConferirContextosERemover(JsonArray escritos, ProcedimentosDeRecurso procedimentos)
    {
        for (int indice = 0; indice < escritos.Count; indice++)
        {
            JsonObject item = escritos[indice]!.AsObject();
            ProcedimentoDeRecursoDeclarado procedimento = procedimentos.Itens[indice];
            item["contexto"]!["tipoDeDecisaoRotulo"]!.GetValue<string>().Should().Be(procedimento.TipoDeDecisao.Rotulo());
            item["alvo"]!["contexto"]!["tipoRotulo"]!.GetValue<string>().Should().Be(procedimento.Alvo!.Tipo.Rotulo());
            item.Remove("contexto");
            item["alvo"]!.AsObject().Remove("contexto");
        }
    }

    private static JsonArray SerializacaoAnterior(ProcedimentosDeRecurso procedimentos) =>
        new([.. procedimentos.Itens.Select(static p => (JsonNode)new JsonObject
        {
            ["tipoDeDecisao"] = p.TipoDeDecisao.Codigo(),
            ["alvo"] = new JsonObject
            {
                ["tipo"] = p.Alvo!.Tipo == TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao ? "ITEM_AVALIACAO" : "AREA_ENEM",
                ["valor"] = p.Alvo.Valor,
            },
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(p.Regra),
        })]);
}
