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
/// O documento do edital e o modelo de documento no acervo público (ADR-0132), com Postgres e MinIO
/// reais: a cópia nasce no registro do ato, pela materialização da divulgação, e o objeto é lido
/// anonimamente, como o cidadão o lê.
/// </summary>
public sealed class DocumentosNoAcervoPublicoIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string BucketPrivado = "uniplus-documentos-acervo-test";
    private const int AnoDoAto = 2026;
    private const string TipoDoAto = "EDITAL_ABERTURA";
    private const string UnidadeDoEnvelope = "ceps";
    private const string Retificacao = "EDITAL_RETIFICACAO";
    private const string TipoAvulso = "AVISO";

    private readonly ProcessoSeletivoDbFixture _dbFixture;
    private readonly MinioContainerFixture _minio;
    private readonly ServiceProvider _provider;

    public DocumentosNoAcervoPublicoIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
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

    [Fact(DisplayName = "O registro do ato copia o edital para o acervo, na chave da ADR-0132, com nome legível, e o certame divulga o endereço")]
    public async Task RegistroDoAto_CopiaOEditalParaOAcervo()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        byte[] pdf = ArquivoDeEdital();
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, pdf);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato, identificador, edital);

        await DivulgarAsync(ato, Acervo());

        using HttpClient anonimo = new();
        using HttpResponseMessage resposta = await anonimo.GetAsync(EnderecoDoEdital(identificador, ato, edital));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(TiposDeMidiaDeDocumento.Pdf);
        string disposicao = resposta.Content.Headers.NonValidated["Content-Disposition"].ToString();
        disposicao.Should().StartWith("inline;", "o link do portal abre o edital em nova aba; como anexo, a aba ficaria em branco");
        disposicao.Should().EndWith(
            $"filename*=UTF-8''{identificador}-edital-abertura.pdf",
            "sem o nome de apresentação, o cidadão baixaria um arquivo chamado pelo resumo criptográfico");

        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        CertameDivulgado divulgado = await leitura.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processo.Id);
        JsonNode.Parse(divulgado.Certame)!["documentoEdital"]!["urlDownload"]!.GetValue<string>().Should().Be(
            AcervoDeTeste.Endereco.De(ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, TipoDoAto, identificador, ato, edital.HashSha256!, FormatoDeDocumento.Pdf)).ToString(),
            "o contrato divulga o endereço da mesma chave para a qual a divulgação copiou");
        string enderecoDoEdital = AcervoDeTeste.Endereco.De(ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, TipoDoAto, identificador, ato, edital.HashSha256!, FormatoDeDocumento.Pdf)).OriginalString;
        EditalDivulgado registrado = divulgado.EditaisDivulgados.Should().ContainSingle().Subject;
        registrado.UrlDownload.Should().Be(enderecoDoEdital, "o endereço registrado para o ato é o da chave para a qual a cópia foi feita");
        registrado.Documentos.Should().ContainSingle().Which.Should().Be(
            new DocumentoDivulgado(
                PapelDoDocumentoDivulgado.Principal, $"{identificador}-edital-abertura.pdf", TiposDeMidiaDeDocumento.Pdf, pdf.LongLength, edital.HashSha256!, enderecoDoEdital));
    }

    [Fact(DisplayName = "O registro do ato copia os anexos congelados para o acervo, byte a byte, com o nome de cada um")]
    public async Task RegistroDoAto_CopiaOsAnexosParaOAcervo()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        (AnexoDoEdital quadro, byte[] bytesDoQuadro) = await SemearAnexoAsync(processo.Id, "Quadro de vagas", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("quadro"));
        (AnexoDoEdital formulario, byte[] bytesDoFormulario) = await SemearAnexoAsync(processo.Id, "Formulário de recurso", TiposDeMidiaDeDocumento.Docx, ArquivosDeModeloDeTeste.Docx());
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato, identificador, edital, anexos: [quadro, formulario]);

        await DivulgarAsync(ato, Acervo());

        using HttpClient anonimo = new();
        foreach ((AnexoDoEdital anexo, byte[] bytes) in (ValueTuple<AnexoDoEdital, byte[]>[])[(quadro, bytesDoQuadro), (formulario, bytesDoFormulario)])
        {
            using HttpResponseMessage resposta = await anonimo.GetAsync(EnderecoDoAnexo(processo.Id, ato, anexo));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
            SHA256.HashData(bytes).Should().Equal(Convert.FromHexString(anexo.HashSha256));
            resposta.Content.Headers.ContentType!.MediaType.Should().Be(anexo.TipoDeMidia);
            resposta.Content.Headers.NonValidated["Content-Disposition"].ToString().Should().EndWith(
                $"filename*=UTF-8''{Uri.EscapeDataString(anexo.NomeArquivo)}");
        }

        IReadOnlyList<DocumentoDivulgado> documentos = (await EditaisDivulgadosAsync(processo.Id)).Single().Documentos!;
        documentos.Select(static d => (d.Papel, d.Nome, d.TamanhoBytes)).Should().Equal(
            (PapelDoDocumentoDivulgado.Principal, $"{identificador}-edital-abertura.pdf", edital.TamanhoBytes!.Value),
            (PapelDoDocumentoDivulgado.Anexo, "Quadro de vagas.pdf", bytesDoQuadro.LongLength),
            (PapelDoDocumentoDivulgado.Anexo, "Formulário de recurso.docx", bytesDoFormulario.LongLength));
        documentos[1].UrlDownload.Should().Be(
            AcervoDeTeste.Endereco.De(ChaveNoAcervoPublico.DoAnexo(processo.Id, ato, quadro)).OriginalString);
    }

    [Fact(DisplayName = "Anexo congelado sem cópia selada com o conteúdo congelado não é publicado, e a divulgação não avança")]
    public async Task AnexoComOutroConteudo_NaoEPublicado()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        (AnexoDoEdital anexo, _) = await SemearAnexoAsync(processo.Id, "Quadro de vagas", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("quadro"));
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato, anexos: [anexo with { HashSha256 = new string('e', 64) }]);

        Func<Task> divulgar = () => DivulgarAsync(ato, Acervo());

        await divulgar.Should().ThrowAsync<InvalidOperationException>(
            "o endereço do anexo apontaria para um arquivo que não é o que o edital atestou");
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processo.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "Retificação que troca um anexo publica o novo em endereço próprio, e os anexos do ato anterior continuam acessíveis")]
    public async Task RetificacaoQueTrocaUmAnexo_PublicaONovo_EOsAnterioresContinuam()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        (AnexoDoEdital mantido, byte[] bytesDoMantido) = await SemearAnexoAsync(processo.Id, "Cronograma", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("cronograma"));
        (AnexoDoEdital trocado, byte[] bytesDoTrocado) = await SemearAnexoAsync(processo.Id, "Quadro de vagas", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("quadro 1"));
        (AnexoDoEdital novo, byte[] bytesDoNovo) = await SemearAnexoAsync(processo.Id, "Quadro de vagas", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("quadro 2"));
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7();
        Guid retificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await SemearVersaoAsync(processo, modelo, abertura, identificador, edital, anexos: [mantido, trocado]);
        await DivulgarAsync(abertura, Acervo());

        await SemearRetificacaoAsync(v1, modelo, retificacao, identificador, edital, anexos: [mantido, novo]);
        await DivulgarAsync(retificacao, Acervo(), Retificacao);

        using HttpClient anonimo = new();
        (await anonimo.GetByteArrayAsync(EnderecoDoAnexo(processo.Id, retificacao, novo))).Should().Equal(bytesDoNovo);
        (await anonimo.GetByteArrayAsync(EnderecoDoAnexo(processo.Id, retificacao, mantido))).Should().Equal(bytesDoMantido,
            "o anexo herdado também é copiado para o ato da nova versão: cada versão tem os endereços dos próprios arquivos");
        (await anonimo.GetByteArrayAsync(EnderecoDoAnexo(processo.Id, abertura, trocado))).Should().Equal(bytesDoTrocado,
            "o ato retificado continua existindo, e o anexo que ele divulgou não some");
        (await anonimo.GetByteArrayAsync(EnderecoDoAnexo(processo.Id, abertura, mantido))).Should().Equal(bytesDoMantido);
        IReadOnlyList<EditalDivulgado> registrados = await EditaisDivulgadosAsync(processo.Id);
        registrados[0].Documentos!.Count(static d => d.Papel == PapelDoDocumentoDivulgado.Anexo).Should().Be(2);
        registrados[1].Documentos!.Where(static d => d.Papel == PapelDoDocumentoDivulgado.Anexo).Select(static d => d.Nome)
            .Should().Equal("Cronograma.pdf", "Quadro de vagas.pdf");
    }

    [Fact(DisplayName = "Edital confirmado de processo ainda sem ato não tem objeto no acervo")]
    public async Task EditalConfirmadoSemAto_NaoTemObjetoNoAcervo()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        string identificador = IdentificadoresDeTeste.NovoValor();
        await SemearVersaoAsync(processo, modelo, Guid.CreateVersion7(), identificador, edital);

        (await ObjetosNoAcervoAsync($"editais/{AnoDoAto:D4}/{UnidadeDoEnvelope}/edital-abertura/{identificador}/")).Should().BeEmpty(
            "publicar sem o ato registrado ainda não é divulgar: o edital continua só no bucket privado");
    }

    [Fact(DisplayName = "Reentrega com o edital já no acervo conclui a divulgação sem regravar o objeto")]
    public async Task Reentrega_ComEditalJaNoAcervo_NaoRegrava()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato, identificador, edital);
        byte[] jaCopiado = Encoding.UTF8.GetBytes("cópia de uma entrega anterior");
        using (MemoryStream stream = new(jaCopiado))
        {
            await _provider.GetRequiredService<IStorageService>().UploadAsync(
                MinioContainerFixture.BucketDoAcervoPublico,
                ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, TipoDoAto, identificador, ato, edital.HashSha256!, FormatoDeDocumento.Pdf),
                stream,
                "application/octet-stream");
        }

        await DivulgarAsync(ato, Acervo());

        using HttpClient anonimo = new();
        (await anonimo.GetByteArrayAsync(EnderecoDoEdital(identificador, ato, edital))).Should().Equal(jaCopiado);
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processo.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "Retificação que reaproveita o documento do edital publica outro objeto, e o do ato anterior continua acessível")]
    public async Task RetificacaoQueReaproveitaOEdital_PublicaOutroObjeto()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        byte[] pdf = ArquivoDeEdital();
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, pdf);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7();
        Guid primeiraRetificacao = Guid.CreateVersion7();
        Guid segundaRetificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await SemearVersaoAsync(processo, modelo, abertura, identificador, edital);
        await DivulgarAsync(abertura, Acervo());
        VersaoConfiguracao v2 = await SemearRetificacaoAsync(v1, modelo, primeiraRetificacao, identificador, edital);
        await DivulgarAsync(primeiraRetificacao, Acervo(), Retificacao);

        // Duas retificações sucessivas com o mesmo documento repetem ano, unidade, tipo, processo e
        // hash: é o identificador do ato que separa os objetos.
        await SemearRetificacaoAsync(v2, modelo, segundaRetificacao, identificador, edital);
        await DivulgarAsync(segundaRetificacao, Acervo(), Retificacao);

        using HttpClient anonimo = new();
        foreach (Guid ato in (Guid[])[primeiraRetificacao, segundaRetificacao])
        {
            using HttpResponseMessage resposta = await anonimo.GetAsync(EnderecoDoEdital(identificador, ato, edital, Retificacao));
            (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);
            resposta.Headers.NonValidated["x-amz-meta-ato-normativo-id"].ToString().Should().Be(ato.ToString("D"),
                "cada ato tem o próprio objeto, com a própria procedência — sem o ato na chave, a segunda retificação cairia no objeto da primeira");
        }

        (await anonimo.GetByteArrayAsync(EnderecoDoEdital(identificador, abertura, edital))).Should().Equal(pdf,
            "o ato retificado continua existindo, e o endereço que ele divulgou não some");

        IReadOnlyList<EditalDivulgado> registrados = await EditaisDivulgadosAsync(processo.Id);
        registrados.Select(static e => e.AtoId).Should().Equal(abertura, primeiraRetificacao, segundaRetificacao);
        registrados.Select(static e => e.UrlDownload).Should().OnlyHaveUniqueItems(
            "o documento é o mesmo e o hash coincide: só o ato separa o endereço de cada versão");
        registrados[2].UrlDownload.Should().Be(
            AcervoDeTeste.Endereco.De(ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, Retificacao, identificador, segundaRetificacao, edital.HashSha256!, FormatoDeDocumento.Pdf)).OriginalString);
    }

    [Fact(DisplayName = "Ato atrasado de uma versão anterior registra o próprio edital sem recuar a divulgação")]
    public async Task AtoAtrasado_RegistraOEdital_SemRecuarAVersao()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7();
        Guid retificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await SemearVersaoAsync(processo, modelo, abertura, identificador, edital);
        await SemearRetificacaoAsync(v1, modelo, retificacao, identificador, edital);

        // A retificação é entregue antes do ato que ela retifica: a linha nasce já na versão 2.
        await DivulgarAsync(retificacao, Acervo(), Retificacao);
        await DivulgarAsync(abertura, Acervo());

        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        CertameDivulgado divulgado = await leitura.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processo.Id);
        divulgado.NumeroVersao.Should().Be(2, "o ato atrasado não desfaz a retificação já divulgada");
        divulgado.AtoCriadorId.Should().Be(retificacao);
        divulgado.EditaisDivulgados.Select(static e => e.AtoId).Should().BeEquivalentTo([abertura, retificacao]);
    }

    [Fact(DisplayName = "Reentrega do mesmo ato não duplica o edital registrado")]
    public async Task Reentrega_DoMesmoAto_NaoDuplicaOEdital()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        Guid ato = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, ato, edital: edital);
        await DivulgarAsync(ato, Acervo());

        await DivulgarAsync(ato, Acervo());

        (await EditaisDivulgadosAsync(processo.Id)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Falha na cópia do edital de uma retificação não registra o edital dela")]
    public async Task FalhaNaCopiaDoEdital_NaoRegistraOEdital()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7();
        Guid retificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await SemearVersaoAsync(processo, modelo, abertura, identificador, edital);
        await DivulgarAsync(abertura, Acervo());
        await SemearRetificacaoAsync(v1, modelo, retificacao, identificador, edital);

        Func<Task> divulgar = () => DivulgarAsync(retificacao, Acervo(bucket: $"acervo-ausente-{Guid.NewGuid():N}"), Retificacao);

        await divulgar.Should().ThrowAsync<AcervoPublicoIndisponivelException>();
        (await EditaisDivulgadosAsync(processo.Id)).Select(static e => e.AtoId).Should().Equal(
            [abertura], "o link só aparece depois de o arquivo estar no acervo");
    }

    [Fact(DisplayName = "Edital sem cópia selada com o conteúdo congelado não é publicado, e a divulgação não avança")]
    public async Task EditalComOutroConteudo_NaoEPublicado()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar edital = await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        Guid ato = Guid.CreateVersion7();
        // O envelope aponta o documento cadastrado, mas com um hash que não é o dele.
        await SemearVersaoAsync(processo, modelo, ato, edital: edital, hashDoEditalCongelado: new string('e', 64));

        Func<Task> divulgar = () => DivulgarAsync(ato, Acervo());

        await divulgar.Should().ThrowAsync<InvalidOperationException>(
            "o endereço divulgado apontaria para um arquivo que não é o que o ato oficializou");
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processo.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "Ato que não cria versão copia o principal e os anexos para o acervo e os registra na divulgação do certame")]
    public async Task AtoQueNaoCriaVersao_CopiaOPrincipalEOsAnexos_ERegistraNaDivulgacao()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, string identificador) = await SemearCertameDivulgadoAsync();
        byte[] docx = ArquivosDeModeloDeTeste.Docx();
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        DocumentoAPublicar principal = await SemearDocumentoDoAtoAsync(processo.Id, "aviso.docx", TiposDeMidiaDeDocumento.Docx, docx);
        DocumentoAPublicar anexo = await SemearDocumentoDoAtoAsync(processo.Id, "relacao.odt", TiposDeMidiaDeDocumento.Odt, odt);
        Guid ato = Guid.CreateVersion7();
        AtoDoProcesso registro = await SemearAtoDoProcessoAsync(processo.Id, identificador, ato, principal, anexo);

        await DivulgarAtoAsync(ato, Acervo());

        string chavePrincipal = ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, TipoAvulso, identificador, ato, principal.HashSha256!, FormatoDeDocumento.Docx);
        string chaveAnexo = ChaveNoAcervoPublico.DoAnexo(processo.Id, ato, registro.Anexos.Single().Id, anexo.HashSha256!, FormatoDeDocumento.Odt);
        using HttpClient anonimo = new();
        (await anonimo.GetByteArrayAsync($"{_minio.EnderecoDoAcervoPublico}/{chavePrincipal}")).Should().Equal(docx,
            "a extensão da chave segue o formato do arquivo, e a cópia preserva os bytes");
        using HttpResponseMessage respostaDoAnexo = await anonimo.GetAsync($"{_minio.EnderecoDoAcervoPublico}/{chaveAnexo}");
        (await respostaDoAnexo.Content.ReadAsByteArrayAsync()).Should().Equal(odt);
        respostaDoAnexo.Content.Headers.NonValidated["Content-Disposition"].ToString().Should().EndWith(
            "filename*=UTF-8''Rela%C3%A7%C3%A3o%20de%20inscritos.odt",
            "o nome de download leva a extensão do formato, que o rótulo exibido não tem");

        EditalDivulgado registrado = (await EditaisDivulgadosAsync(processo.Id)).Single(e => e.AtoId == ato);
        registrado.NumeroVersao.Should().BeNull("o ato não criou versão de configuração");
        registrado.UrlDownload.Should().Be(AcervoDeTeste.Endereco.De(chavePrincipal).OriginalString);
        registrado.Documentos.Should().Equal(
        [
            new DocumentoDivulgado(PapelDoDocumentoDivulgado.Principal, $"{identificador}-aviso.docx", TiposDeMidiaDeDocumento.Docx, docx.LongLength, principal.HashSha256!, AcervoDeTeste.Endereco.De(chavePrincipal).OriginalString),
            new DocumentoDivulgado(PapelDoDocumentoDivulgado.Anexo, "Relação de inscritos", TiposDeMidiaDeDocumento.Odt, odt.LongLength, anexo.HashSha256!, AcervoDeTeste.Endereco.De(chaveAnexo).OriginalString),
        ]);
    }

    [Fact(DisplayName = "Ato que não cria versão, registrado antes da divulgação do certame, espera sem copiar nada ao acervo")]
    public async Task AtoQueNaoCriaVersao_AntesDaDivulgacaoDoCertame_EsperaSemCopiar()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, _) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        DocumentoAPublicar principal = await SemearDocumentoDoAtoAsync(processo.Id, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("aviso"));
        Guid ato = Guid.CreateVersion7();
        await SemearAtoDoProcessoAsync(processo.Id, IdentificadoresDeTeste.NovoValor(), ato, principal);

        Func<Task> divulgar = () => DivulgarAtoAsync(ato, Acervo());

        await divulgar.Should().ThrowAsync<CertameAindaNaoDivulgadoException>(
            "é a corrida com a divulgação do edital, que a política de reentrega espera");
        (await ObjetosDoProcessoNoAcervoAsync(processo.Id)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Reentrega do ato que não cria versão não duplica o registro na divulgação")]
    public async Task AtoQueNaoCriaVersao_Reentrega_NaoDuplica()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, string identificador) = await SemearCertameDivulgadoAsync();
        DocumentoAPublicar principal = await SemearDocumentoDoAtoAsync(processo.Id, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, ArquivoDeEdital("aviso"));
        Guid ato = Guid.CreateVersion7();
        await SemearAtoDoProcessoAsync(processo.Id, identificador, ato, principal);

        await DivulgarAtoAsync(ato, Acervo());
        await DivulgarAtoAsync(ato, Acervo());

        (await EditaisDivulgadosAsync(processo.Id)).Count(e => e.AtoId == ato).Should().Be(1);
    }

    [Fact(DisplayName = "Ato sem registro do processo, de outro domínio, não é divulgado nem falha")]
    public async Task AtoSemRegistroDoProcesso_NaoETocado()
    {
        await _minio.ProvisionarAcervoPublicoAsync();
        (ProcessoSeletivo processo, _) = await SemearCertameDivulgadoAsync();

        await DivulgarAtoAsync(Guid.CreateVersion7(), Acervo());

        (await EditaisDivulgadosAsync(processo.Id)).Should().ContainSingle("só o edital que a divulgação já tinha");
    }

    private Task DivulgarAtoAsync(Guid ato, IAcervoPublico acervo) => DivulgarAsync(ato, acervo, TipoAvulso);

    /// <summary>Um processo com o edital de abertura já divulgado: o que o ato avulso exige para ter onde entrar.</summary>
    private async Task<(ProcessoSeletivo Processo, string Identificador)> SemearCertameDivulgadoAsync()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await SemearProcessoComModeloAsync(ArquivosDeModeloDeTeste.Odt());
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7();
        await SemearVersaoAsync(processo, modelo, abertura, identificador);
        await DivulgarAsync(abertura, Acervo());
        return (processo, identificador);
    }

    /// <summary>Um documento de ato confirmado, com a cópia selada no bucket privado.</summary>
    private async Task<DocumentoAPublicar> SemearDocumentoDoAtoAsync(Guid processoId, string nome, string tipoDeMidia, byte[] conteudo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDeAto, nome, tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(conteudo.LongLength, Convert.ToHexStringLower(SHA256.HashData(conteudo)), TimeProvider.System)
            .IsSuccess.Should().BeTrue();
        await Storage().SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, conteudo, tipoDeMidia);

        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();
        return documento;
    }

    private async Task<AtoDoProcesso> SemearAtoDoProcessoAsync(
        Guid processoId, string identificador, Guid ato, DocumentoAPublicar principal, DocumentoAPublicar? anexo = null)
    {
        AtoDoProcesso registro = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                ato,
                processoId,
                TipoAvulso,
                "Aviso",
                Numero: null,
                new DateOnly(AnoDoAto, 10, 1),
                "Diretoria do CEPS",
                UnidadeDoEnvelope,
                identificador,
                principal.Id,
                anexo is null ? [] : [new AnexoDeclarado(anexo.Id, "Relação de inscritos")]),
            TimeProvider.System).Value!;
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.AtosDoProcesso.Add(registro);
        await context.SaveChangesAsync();
        return registro;
    }

    private async Task DivulgarAsync(Guid ato, IAcervoPublico acervo, string tipoDoAto = TipoDoAto)
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        await DivulgarCertameAoRegistrarAtoHandler.Handle(
            new AtoNormativoRegistrado(ato, AnoDoAto, tipoDoAto),
            new ProcessoSeletivoRepository(context, TimeProvider.System),
            new CertameDivulgadoRepository(context),
            new RegistroCodecsEnvelope(),
            new DocumentoAPublicarRepository(context),
            new ModeloDeDocumentoRepository(context),
            new AtoDoProcessoRepository(context),
            acervo,
            AcervoDeTeste.Endereco,
            context,
            TimeProvider.System,
            CancellationToken.None);
    }

    private async Task<IReadOnlyList<EditalDivulgado>> EditaisDivulgadosAsync(Guid processoId)
    {
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        CertameDivulgado divulgado = await leitura.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processoId);
        return divulgado.EditaisDivulgados;
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
        ProcessoSeletivo processo,
        ModeloDeDocumento modelo,
        Guid ato,
        string? identificador = null,
        DocumentoAPublicar? edital = null,
        string? hashDoEditalCongelado = null,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        edital ??= await SemearEditalAsync(processo.Id, ArquivoDeEdital());
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id,
            EnvelopeComModelo(
                modelo, identificador ?? IdentificadoresDeTeste.NovoValor(), edital.Id, hashDoEditalCongelado ?? edital.HashSha256!, anexos),
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

    private async Task<VersaoConfiguracao> SemearRetificacaoAsync(
        VersaoConfiguracao anterior,
        ModeloDeDocumento modelo,
        Guid ato,
        string identificador,
        DocumentoAPublicar? edital = null,
        IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        edital ??= await SemearEditalAsync(anterior.ProcessoSeletivoId, ArquivoDeEdital());
        VersaoConfiguracao versao = VersaoConfiguracao.Suceder(
            anterior,
            EnvelopeComModelo(modelo, identificador, edital.Id, edital.HashSha256!, anexos),
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
        return versao;
    }

    /// <summary>
    /// O envelope canônico de referência, com o documento do edital e a exigência que oferece modelo
    /// apontando os documentos semeados — a forma exata que a publicação congela.
    /// </summary>
    private static byte[] EnvelopeComModelo(
        ModeloDeDocumento modelo, string identificador, Guid editalId, string hashDoEdital, IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        JsonObject envelope = (JsonObject)JsonNode.Parse(EnvelopeCanonicoGoldenTests.CanonicalizarReferencia().Bytes)!;
        envelope["identificadorLegivel"] = identificador;
        envelope["identidadesUnidade"]!["administradora"]!["slug"] = UnidadeDoEnvelope;
        envelope["hashesEdital"] = new JsonObject
        {
            ["documentoEditalId"] = editalId,
            ["hashSha256"] = hashDoEdital,
            ["anexos"] = new JsonArray([.. (anexos ?? []).Select(static a => (JsonNode)new JsonObject
            {
                ["anexoId"] = a.Id,
                ["documentoId"] = a.DocumentoId,
                ["nomeArquivo"] = a.NomeArquivo,
                ["tipoDeMidia"] = a.TipoDeMidia,
                ["tamanhoBytes"] = a.TamanhoBytes,
                ["hashSha256"] = a.HashSha256,
            })]),
        };
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

    /// <summary>Um documento do edital confirmado, com a cópia selada no bucket privado.</summary>
    private async Task<DocumentoAPublicar> SemearEditalAsync(Guid processoId, byte[] conteudo)
    {
        DocumentoAPublicar edital = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        edital.Confirmar(conteudo.LongLength, Convert.ToHexStringLower(SHA256.HashData(conteudo)), TimeProvider.System)
            .IsSuccess.Should().BeTrue();
        await Storage().SalvarConteudoSeladoAsync(edital.ObjectKeyConfirmado!, conteudo, TiposDeMidiaDeDocumento.Pdf);

        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.DocumentosAPublicar.Add(edital);
        await context.SaveChangesAsync();
        return edital;
    }

    /// <summary>Um anexo do edital: o documento confirmado, com a cópia selada no bucket privado, e o anexo congelado dele.</summary>
    private async Task<(AnexoDoEdital Anexo, byte[] Conteudo)> SemearAnexoAsync(Guid processoId, string nome, string tipoDeMidia, byte[] conteudo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.AnexoDoEdital, nome, tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(conteudo.LongLength, Convert.ToHexStringLower(SHA256.HashData(conteudo)), TimeProvider.System)
            .IsSuccess.Should().BeTrue();
        await Storage().SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, conteudo, tipoDeMidia);

        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();
        return (AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, nome).Value!, conteudo);
    }

    private Uri EnderecoDoAnexo(Guid processoId, Guid ato, AnexoDoEdital anexo) =>
        new($"{_minio.EnderecoDoAcervoPublico}/{ChaveNoAcervoPublico.DoAnexo(processoId, ato, anexo)}");

    /// <summary>Um PDF mínimo; o texto distingue editais diferentes pelo conteúdo, e portanto pelo hash.</summary>
    private static byte[] ArquivoDeEdital(string texto = "edital") => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {texto}\n%%EOF\n");

    private Uri EnderecoDoEdital(string identificador, Guid ato, DocumentoAPublicar edital, string tipoDoAto = TipoDoAto) =>
        new($"{_minio.EnderecoDoAcervoPublico}/{ChaveNoAcervoPublico.DoEdital(AnoDoAto, UnidadeDoEnvelope, tipoDoAto, identificador, ato, edital.HashSha256!, FormatoDeDocumento.Pdf)}");

    private static string ChaveNoAcervo(Guid processoId, Guid ato, ModeloDeDocumento modelo) =>
        ChaveNoAcervoPublico.DoModelo(processoId, ato, ModeloDaExigencia.Do(modelo).Value!);

    private Uri EnderecoNoAcervo(Guid processoId, Guid ato, ModeloDeDocumento modelo) =>
        new($"{_minio.EnderecoDoAcervoPublico}/{ChaveNoAcervo(processoId, ato, modelo)}");

    private Task<List<string>> ObjetosDoProcessoNoAcervoAsync(Guid processoId) =>
        ObjetosNoAcervoAsync($"selecao/processos-seletivos/{processoId:D}/");

    private async Task<List<string>> ObjetosNoAcervoAsync(string prefixo)
    {
        IMinioClient cliente = _provider.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        List<string> chaves = [];
        await foreach (Item item in cliente.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(MinioContainerFixture.BucketDoAcervoPublico)
            .WithPrefix(prefixo)
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
