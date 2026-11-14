namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Json.Schema;

/// <summary>
/// O esquema da configuração do processo seletivo é o contrato público do documento de rascunho, de importação e de
/// exportação. Os exemplos publicados validam contra ele, o rascunho incompleto também, e a forma errada é recusada com o
/// caminho da propriedade.
/// </summary>
public sealed class SchemaDaConfiguracaoDoProcessoTests
{
    private static readonly string PastaDoContrato = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao");

    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(Path.Join(PastaDoContrato, "v1.schema.json")));

    private static readonly EvaluationOptions Opcoes = new() { RequireFormatValidation = true, OutputFormat = OutputFormat.List };

    private static readonly string[] ArquivosDeExemplo = [.. Directory.GetFiles(Path.Join(PastaDoContrato, "exemplos"), "*.json")
        .Select(static caminho => Path.GetFileName(caminho))];

    public static TheoryData<string> Exemplos => [.. ArquivosDeExemplo];

    [Theory(DisplayName = "Cada exemplo publicado valida contra o esquema")]
    [MemberData(nameof(Exemplos))]
    public void Exemplo_Valida(string arquivo) =>
        CaminhosComErro(Carregar(Path.Join("exemplos", arquivo))).Should().BeEmpty();

    [Fact(DisplayName = "Há um exemplo com inscrição pelo sistema e outro de resultado importado")]
    public void Exemplos_CobremAsDuasOrigensDeCandidatos()
    {
        string[] origens = [.. ArquivosDeExemplo
            .Select(static arquivo => Carregar(Path.Join("exemplos", arquivo))["identificacao"]!["origemCandidatos"]!.GetValue<string>())];

        origens.Should().BeEquivalentTo(["inscricaoPropria", "importacaoExterna"]);
    }

    [Fact(DisplayName = "O rascunho só com a identificação e uma fase sem datas valida")]
    public void RascunhoIncompleto_Valida()
    {
        JsonNode rascunho = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "identificacao": { "nome": "Processo seletivo em preparação" },
              "cronograma": { "fases": [ { "chave": "inscricao" } ] } }
            """);

        CaminhosComErro(rascunho).Should().BeEmpty();
    }

    [Fact(DisplayName = "A data de início da fase como número é recusada com o caminho da propriedade")]
    public void InicioDaFaseComoNumero_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["cronograma"]!["fases"]![0]!["inicio"] = 20270110;

        CaminhosComErro(documento).Should().Contain("/cronograma/fases/0/inicio");
    }

    [Fact(DisplayName = "O instante sem fuso UTC é recusado")]
    public void InstanteSemFusoUtc_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["cronograma"]!["fases"]![0]!["fim"] = "2027-01-20T23:59:59-03:00";

        CaminhosComErro(documento).Should().Contain("/cronograma/fases/0/fim");
    }

    [Fact(DisplayName = "O instante UTC com microssegundos valida")]
    public void InstanteUtcComMicrossegundos_Valida()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["cronograma"]!["fases"]![0]!["inicio"] = "2027-01-10T03:00:00.123456Z";

        CaminhosComErro(documento).Should().BeEmpty();
    }

    [Fact(DisplayName = "Propriedade desconhecida é recusada com o caminho")]
    public void PropriedadeDesconhecida_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["cronograma"]!["fases"]![0]!["campoInexistente"] = "valor";

        CaminhosComErro(documento).Should().Contain("/cronograma/fases/0/campoInexistente");
    }

    [Fact(DisplayName = "A versão do formato diferente de 1 é recusada")]
    public void VersaoDoFormatoDiferenteDeUm_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["versaoFormato"] = 2;

        CaminhosComErro(documento).Should().Contain("/versaoFormato");
    }

    [Fact(DisplayName = "O nome acima do limite de tamanho é recusado")]
    public void NomeAcimaDoLimite_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["identificacao"]!["nome"] = new string('a', 301);

        CaminhosComErro(documento).Should().Contain("/identificacao/nome");
    }

    [Theory(DisplayName = "Identificador interno do banco no lugar do código do catálogo é recusado")]
    [InlineData("/identificacao/tipoProcessoCodigo")]
    [InlineData("/identificacao/unidadeAdministradoraCodigo")]
    [InlineData("/cronograma/fases/0/faseCanonicaCodigo")]
    [InlineData("/cronograma/fases/0/unidadeResponsavelCodigo")]
    [InlineData("/etapas/0/tipoEtapaCodigo")]
    [InlineData("/etapas/0/bancas/0/tipoBancaCodigo")]
    public void IdentificadorInterno_NoLugarDoCodigo_Recusa(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        Atribuir(documento, caminho, Guid.CreateVersion7().ToString());

        CaminhosComErro(documento).Should().Contain(caminho);
    }

    [Fact(DisplayName = "Nenhuma propriedade do esquema é referência por identificador interno")]
    public void Esquema_NaoTemReferenciaPorIdentificadorInterno()
    {
        string[] referencias = [.. NomesDePropriedades(EsquemaComoNo())
            .Where(static nome => nome.EndsWith("Id", StringComparison.Ordinal) || nome.EndsWith("Ids", StringComparison.Ordinal))];

        referencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase referencia outra fase pela chave local, definida no próprio documento")]
    public void FaseConcluinte_ReferenciaPelaChaveLocal()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode fase = documento["cronograma"]!["fases"]![2]!;

        fase["faseConcluinteChave"]!.GetValue<string>().Should().Be(documento["cronograma"]!["fases"]![3]!["chave"]!.GetValue<string>());
        fase["faseConcluinteChave"] = "Fase Final";
        CaminhosComErro(documento).Should().Contain("/cronograma/fases/2/faseConcluinteChave");
    }

    [Fact(DisplayName = "A referência temporal dos fatos fica na coleta e cita a fase pela chave local")]
    public void ReferenciaTemporalDosFatos_FicaNaColetaECitaAFasePelaChave()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode referencia = documento["coleta"]!["referenciaTemporalFatos"]!;
        referencia["tipo"] = "FIM_FASE";
        referencia["faseChave"] = "inscricao";
        CaminhosComErro(documento).Should().BeEmpty();

        referencia["faseChave"] = "Fase Inscrição";
        CaminhosComErro(documento).Should().Contain("/coleta/referenciaTemporalFatos/faseChave");
    }

    [Fact(DisplayName = "A referência temporal dos fatos na identificação é recusada")]
    public void ReferenciaTemporalDosFatos_NaIdentificacao_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["identificacao"]!["referenciaTemporalFatos"] = new JsonObject { ["tipo"] = "FIM_INSCRICAO" };

        CaminhosComErro(documento).Should().Contain("/identificacao/referenciaTemporalFatos");
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

    [Fact(DisplayName = "A data de publicação é um instante UTC e o instante com outro fuso é recusado")]
    public void PublicadoEm_EInstanteUtc()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["publicadoEm"]!.GetValue<string>().Should().EndWith("Z");
        CaminhosComErro(documento).Should().BeEmpty();

        documento["publicadoEm"] = "2026-11-10T12:00:00-03:00";
        CaminhosComErro(documento).Should().Contain("/publicadoEm");
    }

    [Fact(DisplayName = "A fração do nível da escala de atendimento acima de 1 é recusada")]
    public void FracaoDoNivelAcimaDeUm_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["escalaDeAtendimento"]![0]!["fracao"] = 1.5;

        CaminhosComErro(documento).Should().Contain("/etapas/0/escalaDeAtendimento/0/fracao");
    }

    [Fact(DisplayName = "O recurso da etapa aceita a âncora de ciência individual e recusa o que não é âncora")]
    public void RecursoDaEtapa_AncoraDeCienciaIndividual()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode recurso = documento["etapas"]![0]!["recursos"]![0]!;
        recurso["ancora"] = "cienciaIndividual";
        CaminhosComErro(documento).Should().BeEmpty();

        recurso["ancora"] = "nenhuma";
        CaminhosComErro(documento).Should().Contain("/etapas/0/recursos/0/ancora");
    }

    [Fact(DisplayName = "Toda propriedade e toda definição do esquema tem título e descrição")]
    public void Esquema_DescreveCadaPropriedadeEmPortugues()
    {
        JsonNode esquema = EsquemaComoNo();
        string[] semDescricao = [.. SubesquemasNomeados(esquema, "#")
            .Where(par => Texto(par.No, "title", esquema) is null || Texto(par.No, "description", esquema) is null)
            .Select(static par => par.Caminho)];

        semDescricao.Should().BeEmpty();
    }

    private static string? Texto(JsonNode subesquema, string campo, JsonNode esquema)
    {
        string? proprio = subesquema[campo]?.GetValue<string>();
        string? alvo = subesquema["$ref"]?.GetValue<string>();
        string? texto = string.IsNullOrWhiteSpace(proprio) && alvo is not null
            ? esquema["$defs"]![alvo.Split('/')[^1]]![campo]?.GetValue<string>()
            : proprio;
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static JsonNode Documento(string json) => JsonNode.Parse(json)!;

    private static JsonNode Carregar(string relativo) => JsonNode.Parse(File.ReadAllText(Path.Join(PastaDoContrato, relativo)))!;

    private static JsonNode EsquemaComoNo() => JsonNode.Parse(File.ReadAllText(Path.Join(PastaDoContrato, "v1.schema.json")))!;

    private static string[] CaminhosComErro(JsonNode documento)
    {
        EvaluationResults resultado = Schema.Evaluate(JsonSerializer.SerializeToElement(documento), Opcoes);
        string[] invalidos = [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNot(r)).Select(static r => r.EvaluationPath.ToString())];
        return [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNot(r))
            .Where(r => !invalidos.Any(outro => outro.StartsWith(r.EvaluationPath + "/", StringComparison.Ordinal)))
            .Select(static r => r.InstanceLocation.ToString()).Distinct()];
    }

    private static bool EstaSobNot(EvaluationResults resultado)
    {
        string caminho = resultado.EvaluationPath.ToString();
        return caminho.EndsWith("/not", StringComparison.Ordinal) || caminho.Contains("/not/", StringComparison.Ordinal);
    }

    private static IEnumerable<EvaluationResults> Detalhes(EvaluationResults resultado) =>
        (resultado.Details ?? []).SelectMany(Detalhes).Prepend(resultado);

    private static void Atribuir(JsonNode raiz, string ponteiro, JsonNode valor)
    {
        string[] passos = ponteiro.Split('/', StringSplitOptions.RemoveEmptyEntries);
        JsonNode atual = raiz;
        foreach (string passo in passos[..^1])
        {
            atual = atual is JsonArray lista ? lista[int.Parse(passo, System.Globalization.CultureInfo.InvariantCulture)]! : atual[passo]!;
        }

        atual[passos[^1]] = valor;
    }

    private static IEnumerable<string> NomesDePropriedades(JsonNode no) => SubesquemasNomeados(no, "#")
        .Select(static par => par.Caminho.Split('/')[^1]).Where(static nome => !nome.StartsWith('#'));

    private static IEnumerable<(string Caminho, JsonNode No)> SubesquemasNomeados(JsonNode no, string caminho) =>
        Filhos(no, caminho).SelectMany(par => SubesquemasNomeados(par.No, par.Caminho).Prepend(par))
            .Concat(no["items"] is JsonObject itens ? SubesquemasNomeados(itens, caminho) : []);

    private static IEnumerable<(string Caminho, JsonNode No)> Filhos(JsonNode no, string caminho) =>
        new[] { "properties", "$defs" }.Select(secao => no[secao]).OfType<JsonObject>().SelectMany(static objeto => objeto)
            .Select(par => ($"{caminho}/{par.Key}", par.Value!));
}
