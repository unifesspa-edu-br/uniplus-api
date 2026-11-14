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
        JsonNode fase = documento["cronograma"]!["fases"]![1]!;

        fase["faseConcluinteChave"]!.GetValue<string>().Should().Be(documento["cronograma"]!["fases"]![2]!["chave"]!.GetValue<string>());
        fase["faseConcluinteChave"] = "Fase Final";
        CaminhosComErro(documento).Should().Contain("/cronograma/fases/1/faseConcluinteChave");
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
