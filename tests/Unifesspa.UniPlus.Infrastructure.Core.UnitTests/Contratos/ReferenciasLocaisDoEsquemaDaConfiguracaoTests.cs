namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// As partes do documento se referenciam pela chave local, o item não tem identidade própria e o id e a versão vivem só na
/// raiz; a etapa e o recurso seguem as regras de chave e de âncora.
/// </summary>
public sealed class ReferenciasLocaisDoEsquemaDaConfiguracaoTests
{
    [Fact(DisplayName = "A fase não declara a fase que conclui o ciclo de recurso: o exemplo valida sem a propriedade e a fase que a traz é recusada")]
    public void FaseConcluinteChave_PropriedadeRetirada_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode fase = documento["cronograma"]!["fases"]![2]!;

        fase.AsObject().ContainsKey("faseConcluinteChave").Should().BeFalse();
        CaminhosComErro(documento).Should().BeEmpty();

        fase["faseConcluinteChave"] = documento["cronograma"]!["fases"]![1]!["chave"]!.GetValue<string>();
        CaminhosComErro(documento).Should().Contain("/cronograma/fases/2/faseConcluinteChave");
    }

    [Fact(DisplayName = "A referência temporal dos fatos fica na coleta e cita a fase pela chave local")]
    public void ReferenciaTemporalDosFatos_FicaNaColetaECitaAFasePelaChave()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode referencia = documento["coleta"]!["referenciaTemporalDosDados"]!;
        referencia["tipo"] = "FIM_FASE";
        referencia["faseChave"] = "inscricao";
        CaminhosComErro(documento).Should().BeEmpty();

        referencia["faseChave"] = "Fase Inscrição";
        CaminhosComErro(documento).Should().Contain("/coleta/referenciaTemporalDosDados/faseChave");
    }

    [Fact(DisplayName = "A referência temporal dos fatos na identificação é recusada")]
    public void ReferenciaTemporalDosFatos_NaIdentificacao_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["identificacao"]!["referenciaTemporalDosDados"] = new JsonObject { ["tipo"] = "FIM_INSCRICAO" };

        CaminhosComErro(documento).Should().Contain("/identificacao/referenciaTemporalDosDados");
    }

    [Fact(DisplayName = "A etapa só com a chave valida, como num rascunho")]
    public void EtapaSoComAChave_Valida()
    {
        JsonNode rascunho = Documento("""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "etapas": [ { "chave": "prova-objetiva" } ] }""");

        CaminhosComErro(rascunho).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa sem chave é recusada com o caminho da etapa")]
    public void EtapaSemChave_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]!.AsArray()[0]!.AsObject().Remove("chave");

        CaminhosComErro(documento).Should().Contain("/etapas/0");
    }

    [Fact(DisplayName = "O item de avaliação sem chave é recusado e a etapa cita a fase pela chave local")]
    public void ItemSemChave_EFaseDaEtapaPelaChave()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode etapa = documento["etapas"]![0]!;
        etapa["faseChave"]!.GetValue<string>().Should().Be(documento["cronograma"]!["fases"]![1]!["chave"]!.GetValue<string>());
        etapa["itensDeAvaliacao"]![0]!.AsObject().Remove("chave");
        etapa["faseChave"] = "Fase Avaliação";

        CaminhosComErro(documento).Should().Contain(["/etapas/0/itensDeAvaliacao/0", "/etapas/0/faseChave"]);
    }

    [Fact(DisplayName = "O nível da escala de atendimento sem chave é recusado")]
    public void NivelSemChave_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["escalaDeAtendimento"]![0]!.AsObject().Remove("chave");

        CaminhosComErro(documento).Should().Contain("/etapas/0/escalaDeAtendimento/0");
    }

    [Fact(DisplayName = "O id do processo é um uuid e a versão é um inteiro a partir de 1, os dois na raiz")]
    public void IdEVersao_NaRaiz()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["id"]!.GetValue<string>().Should().MatchRegex("^[0-9a-f]{8}-");
        documento["versao"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(1);
        CaminhosComErro(documento).Should().BeEmpty();

        documento["id"] = "processo-exemplo";
        documento["versao"] = 0;
        CaminhosComErro(documento).Should().Contain(["/id", "/versao"]);
    }

    [Fact(DisplayName = "O item não tem id: a propriedade id num item é recusada")]
    public void IdNoItem_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["cronograma"]!["fases"]![0]!["id"] = "3f2a9c1e-5b7d-4e8a-9c3f-1a2b3c4d5e6f";

        CaminhosComErro(documento).Should().Contain("/cronograma/fases/0/id");
    }

    [Fact(DisplayName = "O recurso da etapa aceita a âncora de ciência individual e recusa o que não é âncora")]
    public void RecursoDaEtapa_AncoraDeCienciaIndividual()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode recurso = documento["etapas"]![0]!["recursos"]![0]!;
        recurso["eventoInicialDoPrazo"] = "cienciaIndividual";
        CaminhosComErro(documento).Should().BeEmpty();

        recurso["eventoInicialDoPrazo"] = "nenhuma";
        CaminhosComErro(documento).Should().Contain("/etapas/0/recursos/0/eventoInicialDoPrazo");
    }
}
