namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Text.Json.Nodes;

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

    private static string ComIdentificadoresReais(string congelado)
    {
        string[] trechos = congelado.Split("<<id-");
        return trechos[0] + string.Concat(trechos.Skip(1).Select(IdentificadorSeguidoDoRestoDoTrecho));
    }

    private static string IdentificadorSeguidoDoRestoDoTrecho(string trecho)
    {
        int fim = trecho.IndexOf(">>", StringComparison.Ordinal);
        Guid identificador = new(int.Parse(trecho[..fim], CultureInfo.InvariantCulture), 0, 0, new byte[8]);
        return identificador + trecho[(fim + 2)..];
    }
}
