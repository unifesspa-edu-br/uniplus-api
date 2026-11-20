namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// A projeção pública do certame sobre o envelope de referência, com os valores de entrada que cada campo do
/// contrato precisa ter diferentes dos demais para que um campo trocado por outro apareça.
/// </summary>
internal static class ProjecaoDoCertameDeTeste
{
    internal const string Nome = "Certame de teste";

    internal static readonly Guid ProcessoSeletivoId = Guid.Parse("11111111-aaaa-4aaa-8aaa-111111111111");

    internal static readonly AtoDaDivulgacao Ato = new(Guid.Parse("22222222-bbbb-4bbb-8bbb-222222222222"), 2026, "EDITAL");

    internal static readonly string HashDaConfiguracao = new('c', 64);

    internal static JsonObject Envelope() =>
        (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(EnvelopeProjetavelDeTeste.Bytes))!;

    internal static Uri EnderecoNoAcervo(string chave) => new($"https://acervo.teste/{chave}");

    internal static Result<CertamePublicadoDto> Projetar(JsonObject envelope) =>
        ProjecaoDoCertamePublicado.Projetar(ProcessoSeletivoId, Ato, Nome, HashDaConfiguracao, envelope, EnderecoNoAcervo);

    internal static CertamePublicadoDto Projetado() => Projetado(Envelope());

    internal static CertamePublicadoDto Projetado(JsonObject envelope)
    {
        Result<CertamePublicadoDto> resultado = Projetar(envelope);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    /// <summary>Projeta o envelope de referência com cada bloco indicado trocado por um número, que nenhuma parte aceita.</summary>
    internal static Result<CertamePublicadoDto> ProjetarComBlocosIlegiveis(params string[] blocos)
    {
        JsonObject envelope = Envelope();
        foreach (string bloco in blocos)
        {
            envelope[bloco] = 42;
        }

        return Projetar(envelope);
    }
}
