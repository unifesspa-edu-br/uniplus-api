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

    [Fact(DisplayName = "A cópia grava no objeto público o conteúdo, o tipo e o cache imutável")]
    public async Task Copiar_GravaOConteudoOTipoEOCacheImutavel()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        byte[] conteudo = Encoding.UTF8.GetBytes("modelo de autodeclaração");
        string destino = $"testes/{Guid.NewGuid():N}.odt";

        bool copiou = await CopiarAsync(storage, conteudo, destino, Apresentacao);
        using HttpResponseMessage resposta = await LerAnonimamenteAsync(destino);

        copiou.Should().BeTrue();
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(conteudo);
        Cabecalho(resposta, "Content-Type").Should().Be("application/vnd.oasis.opendocument.text");
        Cabecalho(resposta, "Cache-Control").Should().Be("public, max-age=31536000, immutable");
    }

    [Fact(DisplayName = "A cópia grava no objeto público o nome de apresentação e a procedência")]
    public async Task Copiar_GravaONomeDeApresentacaoEAProcedencia()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        string destino = $"testes/{Guid.NewGuid():N}.odt";

        await CopiarAsync(storage, Encoding.UTF8.GetBytes("modelo de autodeclaração"), destino, Apresentacao);
        using HttpResponseMessage resposta = await LerAnonimamenteAsync(destino);

        Cabecalho(resposta, "Content-Disposition").Should().Be(
            "attachment; filename=\"Autodeclaracao etnico-racial.odt\"; "
            + "filename*=UTF-8''Autodeclara%C3%A7%C3%A3o%20%C3%A9tnico-racial.odt");
        Cabecalho(resposta, "x-amz-meta-sha256").Should().Be(new string('a', 64));
    }

    [Fact(DisplayName = "O PDF é gravado para abrir no navegador, com o mesmo nome de apresentação")]
    public async Task Copiar_Pdf_AbreNoNavegador()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        string destino = $"testes/{Guid.NewGuid():N}.pdf";
        ApresentacaoDoObjetoPublico pdf = Apresentacao with { ContentType = "application/pdf", NomeDeApresentacao = "Edital de abertura.pdf" };

        await CopiarAsync(storage, Encoding.UTF8.GetBytes("%PDF-1.4 edital"), destino, pdf);
        using HttpResponseMessage resposta = await LerAnonimamenteAsync(destino);

        Cabecalho(resposta, "Content-Disposition").Should().Be(
            "inline; filename=\"Edital de abertura.pdf\"; filename*=UTF-8''Edital%20de%20abertura.pdf");
    }

    [Fact(DisplayName = "A cópia aceita nome de apresentação com espaços seguidos, gravado como foi dado")]
    public async Task Copiar_NomeComEspacosSeguidos_CopiaComONomeIntacto()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        string destino = $"testes/{Guid.NewGuid():N}.odt";
        ApresentacaoDoObjetoPublico comEspacos = Apresentacao with { NomeDeApresentacao = "Declaração  de renda.odt" };

        await CopiarAsync(storage, Encoding.UTF8.GetBytes("declaração"), destino, comEspacos);
        using HttpResponseMessage resposta = await LerAnonimamenteAsync(destino);

        Cabecalho(resposta, "Content-Disposition").Should().EndWith("filename*=UTF-8''Declara%C3%A7%C3%A3o%20%20de%20renda.odt");
    }

    [Fact(DisplayName = "A cópia não sobrescreve o objeto público que já existe")]
    public async Task Copiar_QuandoDestinoExiste_NaoSobrescreve()
    {
        await minio.ProvisionarAcervoPublicoAsync();
        await using ServiceProvider provider = Provider();
        IStorageService storage = provider.GetRequiredService<IStorageService>();
        byte[] primeiro = Encoding.UTF8.GetBytes("primeira cópia");
        string destino = $"testes/{Guid.NewGuid():N}.odt";
        await CopiarAsync(storage, primeiro, destino, Apresentacao);

        bool copiou = await CopiarAsync(storage, Encoding.UTF8.GetBytes("conteúdo que não pode entrar"), destino, Apresentacao);
        using HttpResponseMessage resposta = await LerAnonimamenteAsync(destino);

        copiou.Should().BeFalse();
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(primeiro);
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
        (await BucketExisteAsync(provider, bucketAusente)).Should().BeFalse(
            "criado pela aplicação, o bucket nasceria privado e os endereços divulgados responderiam 403 em silêncio");
    }

    private static async Task<string> EnviarAoPrivadoAsync(IStorageService storage, byte[] conteudo)
    {
        string chave = $"testes/{Guid.NewGuid():N}/confirmado.odt";
        using MemoryStream stream = new(conteudo);
        await storage.UploadAsync(BucketPrivado, chave, stream, "application/octet-stream");
        return chave;
    }

    private static async Task<bool> CopiarAsync(
        IStorageService storage, byte[] conteudo, string destino, ApresentacaoDoObjetoPublico apresentacao) =>
        await storage.CopiarComoObjetoPublicoAsync(
            BucketPrivado,
            await EnviarAoPrivadoAsync(storage, conteudo),
            MinioContainerFixture.BucketDoAcervoPublico,
            destino,
            apresentacao);

    private static async Task<bool> BucketExisteAsync(ServiceProvider provider, string bucket)
    {
        IMinioClient cliente = provider.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);

        return await cliente.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket));
    }

    private static string Cabecalho(HttpResponseMessage resposta, string nome) =>
        (resposta.Headers.NonValidated.Contains(nome)
            ? resposta.Headers.NonValidated[nome]
            : resposta.Content.Headers.NonValidated[nome]).ToString();

    private async Task<HttpResponseMessage> LerAnonimamenteAsync(string destino)
    {
        using HttpClient anonimo = new();
        HttpResponseMessage resposta = await anonimo.GetAsync(new Uri($"{minio.EnderecoDoAcervoPublico}/{destino}"));

        return resposta.EnsureSuccessStatusCode();
    }

    private Dictionary<string, string?> ConfiguracaoDoMinio() => new()
    {
        ["Storage:Endpoint"] = minio.Endpoint,
        ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
        ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
    };

    private ServiceProvider Provider()
    {
        ServiceCollection services = new();
        services.AddSingleton(TimeProvider.System);
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(ConfiguracaoDoMinio()).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });

        return services.BuildServiceProvider();
    }
}
