namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Cenarios;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConsequenciaDaAusenciaNoEnvelopeTests
{
    [Fact]
    public void BancaDaFase_ConsequenciaDeclarada_GravaELerCodigo()
    {
        JsonObject fase = Fase();
        JsonObject banca = fase["bancasRequeridas"]![0]!.AsObject();
        banca["contexto"]!["consequenciaDaAusenciaRotulo"] = "Rótulo alterado";
        LeitorEnvelope leitor = new();
        BancasLidas<BancaRequerida> lidas = LeituraDoCronograma.LerBancasRequeridas(leitor, fase, "fase");
        leitor.Falhou.Should().BeFalse();
        lidas.Bancas.Single().ConsequenciaDaAusencia.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact]
    public void BancaDaEtapa_ConsequenciaDeclarada_GravaELerCodigo()
    {
        ProcessoSeletivo processo = ProcessoComEtapaBuilder.Criar();
        processo.Etapas.Single().DefinirBancas([BancaDaEtapa.Criar(Chave(), Tipo(), consequenciaDaAusencia: ConsequenciaDoDescumprimento.EliminaDoProcesso)]);
        JsonObject documento = new() { ["etapas"] = SerializacaoDasEtapas.SerializarEtapas(processo) };
        JsonObject copia = JsonNode.Parse(PerfilCanonicoV1.Instancia.Serializar(documento))!.AsObject();
        LeitorEnvelope leitor = new();
        IReadOnlyList<EtapaProcesso> lidas = LeituraDasEtapas.LerEtapas(leitor, copia);
        leitor.Falhou.Should().BeFalse();
        lidas.Single().Bancas.Single().ConsequenciaDaAusencia.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact]
    public void ConsequenciaDiferente_MudaResumoERotuloDiferenteNaoMudaComparacao()
    {
        JsonObject fase = Fase();
        JsonObject outra = fase.DeepClone().AsObject();
        outra["bancasRequeridas"]![0]!["consequenciaDaAusencia"] = "RETIRA_A_MODALIDADE";
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        perfil.HashHex(perfil.Serializar(outra)).Should().NotBe(perfil.HashHex(perfil.Serializar(fase)));
        outra["bancasRequeridas"]![0]!["consequenciaDaAusencia"] = "ELIMINA_DO_PROCESSO";
        outra["bancasRequeridas"]![0]!["contexto"]!["consequenciaDaAusenciaRotulo"] = "Outro texto";
        JsonNode.DeepEquals(ContextoDosRotulos.SemContexto(fase), ContextoDosRotulos.SemContexto(outra)).Should().BeTrue();
    }

    private static JsonObject Fase()
    {
        BancaRequerida banca = BancaRequerida.Criar(Chave(), Tipo(), [], consequenciaDaAusencia: ConsequenciaDoDescumprimento.EliminaDoProcesso);
        OrigemCanonicaDaFase origem = OrigensCanonicasDeTeste.ComNome(new(Guid.NewGuid(), "SELECAO", OrigemDataFase.NoEdital, false, false, false, false));
        FaseCronograma fase = FaseCronograma.Reidratar(ChaveLocal.Criar("fase").Value, Guid.NewGuid(), origem,
            new DecisoesDeCronogramaDaFase(1, null, null, null), new PublicacoesDaFase([], false, null), [banca]);
        return JsonNode.Parse(PerfilCanonicoV1.Instancia.Serializar(SerializacaoDoCronograma.SerializarFase(fase)))!.AsObject();
    }

    private static ChaveLocal Chave() => ChaveLocal.Criar("banca").Value;

    private static TipoDeBancaCopiado Tipo() => new(Guid.NewGuid(), TipoDeBanca.AnaliseDocumental, "Banca", PapelDaBanca.AvaliaOMerito, false, false, []);
}
