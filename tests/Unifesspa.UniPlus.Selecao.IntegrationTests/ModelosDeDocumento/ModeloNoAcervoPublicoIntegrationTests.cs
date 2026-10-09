namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
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
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O modelo de documento no acervo público (ADR-0132), com Postgres e MinIO reais: a cópia nasce no
/// registro do ato, pela materialização da divulgação, e o objeto é lido anonimamente, como o
/// cidadão o lê.
/// </summary>
public sealed class ModeloNoAcervoPublicoIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string BucketPrivado = "uniplus-documentos-acervo-test";

    private readonly ProcessoSeletivoDbFixture _dbFixture;
    private readonly MinioContainerFixture _minio;
    private readonly ServiceProvider _provider;

    public ModeloNoAcervoPublicoIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        ArgumentNullException.ThrowIfNull(minio);
        _dbFixture = dbFixture;
        _minio = minio;

        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
            ["Storage:BucketName"] = BucketPrivado,
        };
        ServiceCollection services = new();
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });
        _provider = services.BuildServiceProvider();
    }

    [Fact(DisplayName = "O registro do ato copia o modelo congelado para o acervo, na chave do ato, com o nome e a procedência")]
    public async Task RegistroDoAto_CopiaOModeloParaOAcervo()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(odt);
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato);

        await DivulgarAsync(ato, Acervo());

        using HttpClient anonimo = new();
        using HttpResponseMessage resposta = await anonimo.GetAsync(EnderecoNoAcervo(processo.Id, ato, modelo));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        SHA256.HashData(await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(Convert.FromHexString(modelo.HashSha256!));
        resposta.Content.Headers.NonValidated["Content-Disposition"].ToString().Should().EndWith(
            "filename*=UTF-8''Autodeclara%C3%A7%C3%A3o%20%C3%A9tnico-racial.odt");
        resposta.Headers.NonValidated["x-amz-meta-ato-normativo-id"].ToString().Should().Be(ato.ToString("D"),
            "o objeto público carrega o ato que o publicou: o acervo é reconciliável sem consultar o banco");
    }

    [Fact(DisplayName = "Modelo confirmado de processo ainda sem ato não tem objeto no acervo")]
    public async Task ModeloConfirmadoSemAto_NaoTemObjetoNoAcervo()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso();
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        ModeloDeDocumentoRepository modelos = new(context);
        ProcessoSeletivoRepository processos = new(context, TimeProvider.System);

        Result<IniciarEnvioDoModeloDeDocumentoDto> iniciar = await IniciarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new IniciarEnvioDoModeloDeDocumentoCommand(processo.Id, "Autodeclaração étnico-racial", "ODT"),
            processos, modelos, Storage(), context, TimeProvider.System, CancellationToken.None);
        using HttpClient http = new();
        using ByteArrayContent corpo = new(ArquivosDeModeloDeTeste.Odt());
        corpo.Headers.ContentType = new MediaTypeHeaderValue(iniciar.Value!.ContentTypeExigido);
        (await http.PutAsync(iniciar.Value.UrlUpload, corpo)).EnsureSuccessStatusCode();
        Result<ModeloDeDocumentoDto> confirmar = await ConfirmarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new ConfirmarEnvioDoModeloDeDocumentoCommand(processo.Id, iniciar.Value.ModeloDeDocumentoId),
            modelos, Storage(), context, TimeProvider.System, CancellationToken.None);
        confirmar.IsSuccess.Should().BeTrue(confirmar.Error?.Message);

        (await ObjetosDoProcessoNoAcervoAsync(processo.Id)).Should().BeEmpty(
            "confirmar é pré-requisito de publicar, não o ato de publicar: o arquivo continua só no bucket privado");
    }

    [Fact(DisplayName = "Falha na cópia para o acervo não avança a divulgação e sai nomeada para a reentrega")]
    public async Task FalhaNaCopia_NaoAvancaADivulgacao()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato);

        Func<Task> divulgar = () => DivulgarAsync(ato, Acervo(bucket: $"acervo-ausente-{Guid.NewGuid():N}"));

        await divulgar.Should().ThrowAsync<AcervoPublicoIndisponivelException>(
            "é a falha que a política de reentrega reagenda em minutos, e não esgota em segundos");
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processo.Id)).Should().BeFalse(
            "o link só é divulgado depois de o arquivo estar no acervo — a reentrega tenta de novo");
    }

    [Fact(DisplayName = "Reentrega com o objeto já no acervo conclui a divulgação sem regravar o objeto")]
    public async Task Reentrega_ComObjetoJaNoAcervo_NaoRegrava()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato);
        // O que uma entrega anterior deixou ao copiar e falhar antes de gravar a divulgação; o
        // conteúdo distinto é só o que permite ver que ele não foi regravado.
        byte[] jaCopiado = Encoding.UTF8.GetBytes("cópia de uma entrega anterior");
        using (MemoryStream stream = new(jaCopiado))
        {
            await _provider.GetRequiredService<IStorageService>().UploadAsync(
                MinioContainerFixture.BucketDoAcervoPublico, ChaveNoAcervo(processo.Id, ato, modelo), stream, "application/octet-stream");
        }

        await DivulgarAsync(ato, Acervo());

        using HttpClient anonimo = new();
        (await anonimo.GetByteArrayAsync(EnderecoNoAcervo(processo.Id, ato, modelo))).Should().Equal(jaCopiado);
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processo.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "Retificação que troca o modelo publica o novo no acervo, e o do ato anterior continua acessível")]
    public async Task RetificacaoQueTrocaOModelo_PublicaONovo_EOAnteriorContinua()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        byte[] odtAnterior = ArquivosDeModeloDeTeste.Odt();
        byte[] odtNovo = ArquivosDeModeloDeTeste.Odt(entradaExtra: "styles.xml");
        (ProcessoSeletivo processo, ModeloDeDocumento anterior) = await SemearProcessoComModeloAsync(odtAnterior);
        ModeloDeDocumento novo = await SemearModeloAsync(processo.Id, odtNovo);
        Guid atoAnterior = Guid.CreateVersion7();
        Guid atoRetificador = Guid.CreateVersion7();
        string identificador = IdentificadoresDeTeste.NovoValor();
        VersaoConfiguracao v1 = await SemearVersaoAsync(processo, anterior, atoAnterior, identificador);
        await DivulgarAsync(atoAnterior, Acervo());

        await SemearRetificacaoAsync(v1, novo, atoRetificador, identificador);
        await DivulgarAsync(atoRetificador, Acervo());

        using HttpClient anonimo = new();
        (await anonimo.GetByteArrayAsync(EnderecoNoAcervo(processo.Id, atoRetificador, novo))).Should().Equal(odtNovo);
        (await anonimo.GetByteArrayAsync(EnderecoNoAcervo(processo.Id, atoAnterior, anterior))).Should().Equal(odtAnterior,
            "o ato retificado continua existindo, e o endereço que ele divulgou não muda de conteúdo nem some");
    }

    private async Task DivulgarAsync(Guid ato, IAcervoPublico acervo)
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        await DivulgarCertameAoRegistrarAtoHandler.Handle(
            new AtoNormativoRegistrado(ato),
            new ProcessoSeletivoRepository(context, TimeProvider.System),
            new CertameDivulgadoRepository(context),
            new RegistroCodecsEnvelope(),
            new ModeloDeDocumentoRepository(context),
            acervo,
            AcervoDeTeste.Endereco,
            context,
            TimeProvider.System,
            CancellationToken.None);
    }

    private async Task<(ProcessoSeletivo Processo, ModeloDeDocumento Modelo)> SemearProcessoComModeloAsync(byte[] conteudo)
    {
        ProcessoSeletivo processo = NovoProcesso();
        await using (SelecaoDbContext context = _dbFixture.CreateDbContext())
        {
            context.ProcessosSeletivos.Add(processo);
            await context.SaveChangesAsync();
        }

        return (processo, await SemearModeloAsync(processo.Id, conteudo));
    }

    /// <summary>Um modelo confirmado, com a cópia selada no bucket privado, como a confirmação o deixa.</summary>
    private async Task<ModeloDeDocumento> SemearModeloAsync(Guid processoId, byte[] conteudo)
    {
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(
            processoId, "Autodeclaração étnico-racial", "ODT", TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        modelo.Confirmar(conteudo.LongLength, Convert.ToHexStringLower(SHA256.HashData(conteudo)), TimeProvider.System)
            .IsSuccess.Should().BeTrue();
        await Storage().SalvarConteudoSeladoAsync(modelo.ObjectKeyConfirmado!, conteudo, modelo.ContentType);

        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.ModelosDeDocumento.Add(modelo);
        await context.SaveChangesAsync();
        return modelo;
    }

    private async Task<VersaoConfiguracao> SemearVersaoAsync(
        ProcessoSeletivo processo, ModeloDeDocumento modelo, Guid ato, string? identificador = null)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id,
            EnvelopeComModelo(modelo, identificador ?? IdentificadoresDeTeste.NovoValor()),
            CorpusEnvelope.Codec.SchemaVersion,
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: ato,
            atoCriadorHash: new string('a', 64),
            atorUsuarioSub: "teste",
            instante: DateTimeOffset.UtcNow);
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.VersoesConfiguracao.Add(versao);
        await context.SaveChangesAsync();
        return versao;
    }

    private async Task SemearRetificacaoAsync(VersaoConfiguracao anterior, ModeloDeDocumento modelo, Guid ato, string identificador)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Suceder(
            anterior,
            EnvelopeComModelo(modelo, identificador),
            CorpusEnvelope.Codec.SchemaVersion,
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: ato,
            atoCriadorHash: new string('b', 64),
            atoCriadorRetificaId: anterior.AtoCriadorId,
            atorUsuarioSub: "teste",
            instante: DateTimeOffset.UtcNow);
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.VersoesConfiguracao.Add(versao);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// O envelope canônico de referência, com a exigência que oferece modelo apontando o modelo
    /// semeado — a forma exata que a publicação congela.
    /// </summary>
    private static byte[] EnvelopeComModelo(ModeloDeDocumento modelo, string identificador)
    {
        JsonObject envelope = (JsonObject)JsonNode.Parse(EnvelopeCanonicoGoldenTests.CanonicalizarReferencia().Bytes)!;
        envelope["identificadorLegivel"] = identificador;
        JsonObject[] comModelo = [.. envelope["documentosExigidos"]!["exigencias"]!.AsArray()
            .OfType<JsonObject>()
            .Where(static e => e["modelo"] is JsonObject)];
        comModelo.Should().NotBeEmpty("o envelope de referência precisa ter uma exigência com modelo para o teste ter objeto");
        foreach (JsonObject exigencia in comModelo)
        {
            exigencia["modelo"] = new JsonObject
            {
                ["modeloId"] = modelo.Id,
                ["nomeArquivo"] = modelo.NomeArquivo,
                ["formato"] = ModeloDeDocumento.TokenDe(modelo.Formato),
                ["hashSha256"] = modelo.HashSha256,
            };
        }

        return Encoding.UTF8.GetBytes(envelope.ToJsonString());
    }

    private static string ChaveNoAcervo(Guid processoId, Guid ato, ModeloDeDocumento modelo) =>
        ChaveNoAcervoPublico.DoModelo(processoId, ato, ModeloDaExigencia.Do(modelo).Value!);

    private Uri EnderecoNoAcervo(Guid processoId, Guid ato, ModeloDeDocumento modelo) =>
        new($"{_minio.EnderecoDoAcervoPublico}/{ChaveNoAcervo(processoId, ato, modelo)}");

    private async Task<List<string>> ObjetosDoProcessoNoAcervoAsync(Guid processoId)
    {
        IMinioClient cliente = _provider.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        List<string> chaves = [];
        await foreach (Item item in cliente.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(MinioContainerFixture.BucketDoAcervoPublico)
            .WithPrefix($"selecao/processos-seletivos/{processoId:D}/")
            .WithRecursive(true)))
        {
            chaves.Add(item.Key);
        }

        return chaves;
    }

    private ArquivoArmazenadoStorageService Storage() =>
        new(_provider.GetRequiredService<IStorageService>(), _provider.GetRequiredService<IOptions<StorageOptions>>());

    private AcervoPublicoService Acervo(string bucket = MinioContainerFixture.BucketDoAcervoPublico) =>
        new(
            () => _provider.GetRequiredService<IStorageService>(),
            _provider.GetRequiredService<IOptions<StorageOptions>>(),
            Options.Create(new AcervoPublicoOptions { Bucket = bucket }));

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2027 — modelo no acervo", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
