namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class ProjecaoDosEnvelopesDeReferenciaTests
{
    [Theory]
    [InlineData("envelope.json")]
    [InlineData("envelope-rico.json")]
    [InlineData("envelope-cascata.json")]
    public void Projetar_EnvelopeDeReferencia_EAceitoPeloEsquemaV1(string arquivo)
    {
        string caminho = Path.Join(AppContext.BaseDirectory, "ProcessosSeletivos", "Fixtures", arquivo);
        VersaoPublicada versao = new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, ComIdentificadoresReais(File.ReadAllText(caminho)));
        EsquemaDoDocumentoDaConfiguracao esquema = new();
        ProjecaoDaVersaoPublicada projecao = new(esquema);

        JsonObject documento = projecao.Projetar(versao);

        documento.Should().NotBeNull();
        IReadOnlyList<FieldError> erros = esquema.Avaliar(documento);
        erros.Should().BeEmpty("as violações do esquema são: {0}", string.Join("; ", erros.Select(erro => $"{erro.Field}: {erro.Error.Message}")));
    }

    private static string ComIdentificadoresReais(string congelado) =>
        Regex.Replace(congelado, "<<id-(\\d+)>>", ocorrencia =>
            new Guid(int.Parse(ocorrencia.Groups[1].Value, CultureInfo.InvariantCulture), 0, 0, new byte[8]).ToString());
}
