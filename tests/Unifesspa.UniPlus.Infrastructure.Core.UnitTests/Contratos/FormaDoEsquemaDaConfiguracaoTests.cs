namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// A forma errada é recusada com o caminho da propriedade: tipo, instante, limite de tamanho, propriedade desconhecida e
/// identificador interno no lugar do código; e o esquema descreve cada propriedade.
/// </summary>
public sealed class FormaDoEsquemaDaConfiguracaoTests
{
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

    [Fact(DisplayName = "A fração do nível da escala de atendimento acima de 1 é recusada")]
    public void FracaoDoNivelAcimaDeUm_ApontaOCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["escalaDeAtendimento"]![0]!["fracao"] = 1.5;

        CaminhosComErro(documento).Should().Contain("/etapas/0/escalaDeAtendimento/0/fracao");
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
        string[] referencias = [.. ArquivosDoEsquema.SelectMany(static arquivo => NomesDePropriedades(Esquema(arquivo)))
            .Where(static nome => nome.EndsWith("Id", StringComparison.Ordinal) || nome.EndsWith("Ids", StringComparison.Ordinal))];

        referencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Toda propriedade e toda definição do esquema tem título e descrição")]
    public void Esquema_DescreveCadaPropriedadeEmPortugues()
    {
        string[] semDescricao = [.. ArquivosDoEsquema.SelectMany(static arquivo => SemTituloOuDescricao(arquivo))];

        semDescricao.Should().BeEmpty();
    }

    private static IEnumerable<string> SemTituloOuDescricao(string arquivo)
    {
        JsonNode raiz = Esquema(arquivo);
        IEnumerable<(string Caminho, JsonNode No)> nomeados = SubesquemasNomeados(raiz, arquivo).Prepend((arquivo, raiz));
        return nomeados.Where(par => Texto(par.No, "title", arquivo) is null || Texto(par.No, "description", arquivo) is null)
            .Select(static par => par.Caminho);
    }

    private static string? Texto(JsonNode subesquema, string campo, string arquivo)
    {
        string? proprio = subesquema[campo]?.GetValue<string>();
        string? alvo = subesquema["$ref"]?.GetValue<string>();
        string? texto = string.IsNullOrWhiteSpace(proprio) && alvo is not null
            ? ResolverReferencia(arquivo, alvo)[campo]?.GetValue<string>()
            : proprio;
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
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
