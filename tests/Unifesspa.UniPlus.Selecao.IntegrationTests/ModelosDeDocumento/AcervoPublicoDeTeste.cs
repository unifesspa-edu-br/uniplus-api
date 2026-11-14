namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Minio.DataModel;
using Minio.DataModel.Args;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O acervo público sobre Postgres e MinIO reais: a divulgação do ato que copia os arquivos congelados para o
/// acervo, o que ficou registrado no certame divulgado e a leitura anônima dos objetos, como o cidadão a faz.
/// </summary>
internal sealed class AcervoPublicoDeTeste
{
    public const int AnoDoAto = 2026;
    public const string TipoDoAto = "EDITAL_ABERTURA";
    public const string UnidadeDoEnvelope = "ceps";
    public const string Retificacao = "EDITAL_RETIFICACAO";
    public const string TipoAvulso = "AVISO";

    private readonly MinioContainerFixture _minio;
    private readonly ArmazenamentoDeTesteNoMinio _armazenamento;

    public AcervoPublicoDeTeste(ProcessoSeletivoDbFixture db, MinioContainerFixture minio, string bucketPrivado)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(minio);
        Db = db;
        _minio = minio;
        _armazenamento = new ArmazenamentoDeTesteNoMinio(minio, bucketPrivado);
    }

    public ProcessoSeletivoDbFixture Db { get; }

    public IStorageService Bucketes => _armazenamento.Bucketes;

    public static string BucketInexistente() => $"acervo-ausente-{Guid.NewGuid():N}";

    public static async Task<HttpResponseMessage> LerAnonimamenteAsync(Uri endereco)
    {
        using HttpClient anonimo = new();
        return await anonimo.GetAsync(endereco);
    }

    public static async Task<byte[]> BaixarAnonimamenteAsync(Uri endereco)
    {
        using HttpClient anonimo = new();
        return await anonimo.GetByteArrayAsync(endereco);
    }

    public static void ConferirNomeDeDownload(HttpResponseMessage resposta, string fimDaDisposicao, string motivo = "") =>
        resposta.Content.Headers.NonValidated["Content-Disposition"].ToString().Should().EndWith(fimDaDisposicao, motivo);

    public static void ConferirProcedencia(HttpResponseMessage resposta, Guid ato, string motivo) =>
        resposta.Headers.NonValidated["x-amz-meta-ato-normativo-id"].ToString().Should().Be(ato.ToString("D"), motivo);

    public Task DivulgarAtoAsync(Guid ato) => DivulgarAsync(ato, TipoAvulso);

    public Task ProvisionarAsync() => _minio.ProvisionarAcervoPublicoAsync();

    public ArquivoArmazenadoStorageService Storage() => _armazenamento.ArquivosArmazenados();

    public async Task DivulgarAsync(Guid ato, string tipoDoAto = TipoDoAto, string bucket = MinioContainerFixture.BucketDoAcervoPublico)
    {
        await using UniPlusDbContext context = Db.CreateDbContext();
        await DivulgarCertameAoRegistrarAtoHandler.Handle(
            new AtoNormativoRegistrado(ato, AnoDoAto, tipoDoAto),
            new ProcessoSeletivoRepository(context, TimeProvider.System),
            new CertameDivulgadoRepository(context),
            new RegistroCodecsEnvelope(),
            new DocumentoAPublicarRepository(context),
            new ModeloDeDocumentoRepository(context),
            new AtoDoProcessoRepository(context),
            AcervoNoBucket(bucket),
            AcervoDeTeste.Endereco,
            context,
            TimeProvider.System,
            CancellationToken.None);
    }

    public async Task<CertameDivulgado> CertameDivulgadoAsync(Guid processoId)
    {
        await using UniPlusDbContext leitura = Db.CreateDbContext();
        return await leitura.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processoId);
    }

    public async Task<bool> CertameFoiDivulgadoAsync(Guid processoId)
    {
        await using UniPlusDbContext leitura = Db.CreateDbContext();
        return await leitura.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processoId);
    }

    public async Task<IReadOnlyList<EditalDivulgado>> EditaisDivulgadosAsync(Guid processoId) =>
        (await CertameDivulgadoAsync(processoId)).EditaisDivulgados;

    public async Task GravarNoAcervoAsync(string chave, byte[] conteudo)
    {
        using MemoryStream stream = new(conteudo);
        await Bucketes.UploadAsync(MinioContainerFixture.BucketDoAcervoPublico, chave, stream, "application/octet-stream");
    }

    public Uri EnderecoDaChave(string chave) => new($"{_minio.EnderecoDoAcervoPublico}/{chave}");

    public Uri EnderecoDoAnexo(Guid processoId, Guid ato, AnexoDoEdital anexo) =>
        EnderecoDaChave(ChaveNoAcervoPublico.DoAnexo(processoId, ato, anexo));

    public Uri EnderecoDoEdital(string identificador, Guid ato, DocumentoAPublicar edital, string tipoDoAto = TipoDoAto) =>
        EnderecoDaChave(ChaveDoEdital(identificador, ato, edital, tipoDoAto));

    public Uri EnderecoNoAcervo(Guid processoId, Guid ato, ModeloDeDocumento modelo) =>
        EnderecoDaChave(ChaveDoModelo(processoId, ato, modelo));

    public static string ChaveDoModelo(Guid processoId, Guid ato, ModeloDeDocumento modelo) =>
        ChaveNoAcervoPublico.DoModelo(processoId, ato, ModeloDaExigencia.Do(modelo).Value!);

    public static string ChaveDoEdital(string identificador, Guid ato, DocumentoAPublicar edital, string tipoDoAto = TipoDoAto) =>
        ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, tipoDoAto, identificador, ato, edital.HashSha256!, FormatoDeDocumento.Pdf);

    public Task<List<string>> ObjetosDoProcessoNoAcervoAsync(Guid processoId) =>
        ObjetosNoAcervoAsync($"selecao/processos-seletivos/{processoId:D}/");

    public async Task<List<string>> ObjetosNoAcervoAsync(string prefixo)
    {
        List<string> chaves = [];
        await foreach (Item item in _armazenamento.ClienteInterno.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(MinioContainerFixture.BucketDoAcervoPublico)
            .WithPrefix(prefixo)
            .WithRecursive(true)))
        {
            chaves.Add(item.Key);
        }

        return chaves;
    }

    private AcervoPublicoService AcervoNoBucket(string bucket) =>
        new(
            () => _armazenamento.Bucketes,
            _armazenamento.Opcoes,
            Options.Create(new AcervoPublicoOptions { Bucket = bucket }));
}
