namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProjecaoDosEnvelopesDeReferenciaTests
{
    [Theory]
    [InlineData("envelope.json")]
    [InlineData("envelope-rico.json")]
    [InlineData("envelope-cascata.json")]
    public void Projetar_EnvelopeDeReferencia_EAceitoPeloEsquemaV1(string arquivo)
    {
        VersaoPublicada versao = new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, EnvelopesDeReferenciaDeTeste.Ler(arquivo));
        EsquemaDoDocumentoDaConfiguracao esquema = new();
        ProjecaoDaVersaoPublicada projecao = new(esquema);

        JsonObject documento = projecao.Projetar(versao);

        documento.Should().NotBeNull();
        IReadOnlyList<FieldError> erros = esquema.Avaliar(documento);
        erros.Should().BeEmpty("as violações do esquema são: {0}", string.Join("; ", erros.Select(erro => $"{erro.Field}: {erro.Error.Message}")));
    }
}
