namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;

public sealed class NomeNaPublicacaoTests
{
    [Fact]
    public void Canonicalizar_NomeDecomposto_GravaEmNfc()
    {
        EntradaCanonicalizacao entrada = ConfiguracaoPublicavelBuilder.Criar(ExemploDeConfiguracao.Entrada(), "Selec\u0327a\u0303o");
        SnapshotCanonico documento = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        JsonNode.Parse(documento.Bytes)!["nome"]!.GetValue<string>().Should().Be("Seleção");
    }

    [Fact]
    public void LerEnvelope_NomeAusente_Recusa()
    {
        SnapshotCanonico documento = new SnapshotPublicacaoCanonicalizer().Canonicalizar(ExemploDeConfiguracao.Entrada());
        JsonObject conteudo = JsonNode.Parse(documento.Bytes)!.AsObject();
        conteudo.Remove("nome");
        LeitorEnvelope leitor = new();
        LeituraDoEnvelope.Ler(leitor, conteudo, false);
        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("nome");
    }

    [Fact]
    public void Canonicalizar_RetificacaoComNomeAlterado_MudaResumo()
    {
        EntradaCanonicalizacao entrada = ExemploDeConfiguracao.Entrada() with { Retificacao = new(Guid.NewGuid(), "Correção") };
        SnapshotPublicacaoCanonicalizer canonicalizador = new();
        byte[] primeiro = canonicalizador.Canonicalizar(ConfiguracaoPublicavelBuilder.Criar(entrada, "Seleção anterior")).Bytes;
        byte[] segundo = canonicalizador.Canonicalizar(ConfiguracaoPublicavelBuilder.Criar(entrada, "Seleção alterada")).Bytes;
        HashCanonicalComputer.ComputeSha256Hex(primeiro).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(segundo));
        JsonObject primeiroDocumento = JsonNode.Parse(primeiro)!.AsObject();
        JsonObject segundoDocumento = JsonNode.Parse(segundo)!.AsObject();
        primeiroDocumento.Remove("nome");
        segundoDocumento.Remove("nome");
        JsonNode.DeepEquals(primeiroDocumento, segundoDocumento).Should().BeTrue();
    }
}
