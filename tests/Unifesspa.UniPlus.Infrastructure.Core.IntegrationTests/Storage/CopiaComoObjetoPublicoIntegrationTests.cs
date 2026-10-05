namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Storage;

using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

using Minio;
using Minio.DataModel.Args;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A cópia para o acervo público contra um MinIO real: o objeto público é lido anonimamente, como o
/// cidadão o lê, e tudo que a resposta diz tem de ter sido gravado no objeto — não há aplicação no
/// caminho da leitura (ADR-0132).
/// </summary>
[Collection(MinioContainerFixture.CollectionName)]
public sealed class CopiaComoObjetoPublicoIntegrationTests(MinioContainerFixture minio)
{
    private const string BucketPrivado = "uniplus-copia-publica-tests";

    private static readonly ApresentacaoDoObjetoPublico Apresentacao = new(
        "application/vnd.oasis.opendocument.text",
        "Autodeclaração étnico-racial.odt",
        new Dictionary<string, string> { ["sha256"] = new string('a', 64) });

    [Fact(DisplayName = "A cópia grava no objeto público o tipo, o cache imutável, o nome de apresentação e a procedência")]
    public async Task Copiar_GravaOsCabecalhosNoObjetoPublico()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        byte[] conteudo = Encoding.UTF8.GetBytes("modelo de autodeclaração");
        string origem = await EnviarAoPrivadoAsync(storage, conteudo);
        string destino = $"testes/{Guid.NewGuid():N}.odt";

        bool copiou = await storage.CopiarComoObjetoPublicoAsync(
            BucketPrivado, origem, MinioContainerFixture.BucketDoAcervoPublico, destino, Apresentacao);

        copiou.Should().BeTrue();
        using HttpClient anonimo = new();
        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri($"{minio.EnderecoDoAcervoPublico}/{destino}"));
        resposta.EnsureSuccessStatusCode();
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(conteudo);
        resposta.Content.Headers.NonValidated["Content-Type"].ToString().Should().Be("application/vnd.oasis.opendocument.text");
        resposta.Headers.NonValidated["Cache-Control"].ToString().Should().Be("public, max-age=31536000, immutable");
        resposta.Content.Headers.NonValidated["Content-Disposition"].ToString().Should().Be(
            "attachment; filename=\"Autodeclaracao etnico-racial.odt\"; "
            + "filename*=UTF-8''Autodeclara%C3%A7%C3%A3o%20%C3%A9tnico-racial.odt");
        resposta.Headers.NonValidated["x-amz-meta-sha256"].ToString().Should().Be(new string('a', 64));
    }

    [Fact(DisplayName = "A cópia aceita nome de apresentação com espaços seguidos, gravado como foi dado")]
    public async Task Copiar_NomeComEspacosSeguidos_CopiaComONomeIntacto()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        string destino = $"testes/{Guid.NewGuid():N}.odt";

        await storage.CopiarComoObjetoPublicoAsync(
            BucketPrivado,
            await EnviarAoPrivadoAsync(storage, Encoding.UTF8.GetBytes("declaração")),
            MinioContainerFixture.BucketDoAcervoPublico,
            destino,
            Apresentacao with { NomeDeApresentacao = "Declaração  de renda.odt" });

        using HttpClient anonimo = new();
        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri($"{minio.EnderecoDoAcervoPublico}/{destino}"));
        resposta.EnsureSuccessStatusCode();
        resposta.Content.Headers.NonValidated["Content-Disposition"].ToString().Should().EndWith(
            "filename*=UTF-8''Declara%C3%A7%C3%A3o%20%20de%20renda.odt");
    }

    [Fact(DisplayName = "A cópia não sobrescreve o objeto público que já existe")]
    public async Task Copiar_QuandoDestinoExiste_NaoSobrescreve()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        byte[] primeiro = Encoding.UTF8.GetBytes("primeira cópia");
        string destino = $"testes/{Guid.NewGuid():N}.odt";
        await storage.CopiarComoObjetoPublicoAsync(
            BucketPrivado, await EnviarAoPrivadoAsync(storage, primeiro), MinioContainerFixture.BucketDoAcervoPublico, destino, Apresentacao);

        bool copiou = await storage.CopiarComoObjetoPublicoAsync(
            BucketPrivado,
            await EnviarAoPrivadoAsync(storage, Encoding.UTF8.GetBytes("conteúdo que não pode entrar")),
            MinioContainerFixture.BucketDoAcervoPublico,
            destino,
            Apresentacao);

        copiou.Should().BeFalse();
        using HttpClient anonimo = new();
        (await anonimo.GetByteArrayAsync(new Uri($"{minio.EnderecoDoAcervoPublico}/{destino}"))).Should().Equal(primeiro);
    }

    [Fact(DisplayName = "A cópia não cria o bucket público ausente: falha, e o bucket continua não existindo")]
    public async Task Copiar_QuandoBucketDeDestinoAusente_FalhaSemCriar()
    {
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        string origem = await EnviarAoPrivadoAsync(storage, Encoding.UTF8.GetBytes("modelo"));
        string bucketAusente = $"acervo-ausente-{Guid.NewGuid():N}";

        Func<Task> copiar = () => storage.CopiarComoObjetoPublicoAsync(
            BucketPrivado, origem, bucketAusente, "testes/modelo.odt", Apresentacao);

        await copiar.Should().ThrowAsync<Exception>();
        IMinioClient cliente = provider.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        (await cliente.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketAusente))).Should().BeFalse(
            "criado pela aplicação, o bucket nasceria privado e os endereços divulgados responderiam 403 em silêncio");
    }

    private static async Task<string> EnviarAoPrivadoAsync(IStorageService storage, byte[] conteudo)
    {
        string chave = $"testes/{Guid.NewGuid():N}/confirmado.odt";
        using MemoryStream stream = new(conteudo);
        await storage.UploadAsync(BucketPrivado, chave, stream, "application/octet-stream");
        return chave;
    }

    private ServiceProvider Provider()
    {
        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
        };
        ServiceCollection services = new();
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });
        return services.BuildServiceProvider();
    }
}
