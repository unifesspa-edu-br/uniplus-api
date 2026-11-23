namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

public sealed class ChavesEIdentificadoresDoEsquemaTests
{
    [Theory]
    [InlineData("prova-objetiva")]
    [InlineData("Etapa")]
    [InlineData("1etapa")]
    [InlineData("etapa__x")]
    [InlineData("etapa_")]
    public void Chave_FormatoInvalido_Recusa(string chave)
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["chave"] = chave;
        CaminhosComErro(documento).Should().Contain("/etapas/0/chave");
    }

    [Fact]
    public void Chave_ComSessentaECincoCaracteres_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["chave"] = new string('a', 65);
        CaminhosComErro(documento).Should().Contain("/etapas/0/chave");
    }

    [Fact]
    public void Chave_ComSublinhadosERepeticao_Aceita()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["chave"] = "etapa_prova_escrita_2";
        CaminhosComErro(documento).Should().BeEmpty();
    }

    [Fact]
    public void Identificadores_NasPartes_Aceita()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        foreach (JsonNode parte in Partes(documento))
        {
            parte["id"] = "3f2a9c1e-5b7d-4e8a-9c3f-1a2b3c4d5e6f";
        }
        CaminhosComErro(documento).Should().BeEmpty();
    }

    [Fact]
    public void Identificadores_TextoInvalido_RecusaComCaminhos()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        foreach (JsonNode parte in Partes(documento))
        {
            parte["id"] = "invalido";
        }
        CaminhosComErro(documento).Should().Contain(["/cronograma/fases/0/id", "/etapas/0/id", "/etapas/0/itensDeAvaliacao/0/id", "/etapas/0/bancas/0/id", "/documentosExigidos/exigencias/0/id"]);
    }

    [Theory]
    [InlineData("cronograma.schema.json", "$defs/fase/properties/id")]
    [InlineData("etapas.schema.json", "properties/id")]
    [InlineData("etapas.schema.json", "$defs/itemDeAvaliacao/properties/id")]
    [InlineData("bancas.schema.json", "properties/id")]
    [InlineData("exigencias.schema.json", "$defs/exigencia/properties/id")]
    public void Identificador_DaParte_SoDeLeitura(string arquivo, string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode campo = caminho.Split('/').Aggregate(Esquema(arquivo), static (no, passo) => no[passo]!);
        campo["readOnly"]!.GetValue<bool>().Should().BeTrue();
        campo["format"]!.GetValue<string>().Should().Be("uuid");
    }

    [Fact]
    public void Identificador_NoNivel_RecusaComCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["escalaDeAtendimento"]![0]!["id"] = "3f2a9c1e-5b7d-4e8a-9c3f-1a2b3c4d5e6f";
        CaminhosComErro(documento).Should().Contain("/etapas/0/escalaDeAtendimento/0/id");
    }

    [Fact]
    public void Identificador_NoLugarDeChaveOuCodigo_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        const string Identificador = "3f2a9c1e-5b7d-4e8a-9c3f-1a2b3c4d5e6f";
        documento["etapas"]![0]!["chave"] = Identificador;
        documento["cronograma"]!["fases"]![0]!["faseCanonicaCodigo"] = Identificador;
        CaminhosComErro(documento).Should().Contain(["/etapas/0/chave", "/cronograma/fases/0/faseCanonicaCodigo"]);
    }

    [Fact]
    public void Ordem_NaEtapa_RecusaComCaminho()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["ordem"] = 1;
        CaminhosComErro(documento).Should().Contain("/etapas/0/ordem");
    }

    private static JsonNode[] Partes(JsonNode documento) =>
    [
        documento["cronograma"]!["fases"]![0]!,
        documento["etapas"]![0]!,
        documento["etapas"]![0]!["itensDeAvaliacao"]![0]!,
        documento["etapas"]![0]!["bancas"]![0]!,
        documento["documentosExigidos"]!["exigencias"]![0]!,
    ];
}
