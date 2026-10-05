namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento.AcervoPublico;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A API UniPlus com Wolverine, Postgres e um MinIO real com o acervo público provisionado: o
/// caminho inteiro da publicação até o download anônimo do modelo.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative used as collection fixture state.")]
public sealed class AcervoPublicoApiFactory(string connectionString, MinioContainerFixture minio)
    : MonolitoApiFactory(connectionString, wolverineEnabled: true)
{
    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
        new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
            ["Storage:BucketName"] = "uniplus-documentos-e2e",
            ["AcervoPublico:Bucket"] = MinioContainerFixture.BucketDoAcervoPublico,

            // A porta dinâmica do container: o appsettings.Development.json, que a fábrica de teste
            // carrega, aponta o acervo para a porta fixa do MinIO do docker-compose.
            ["AcervoPublico:EnderecoBase"] = minio.EnderecoDoAcervoPublico,
        };
}
