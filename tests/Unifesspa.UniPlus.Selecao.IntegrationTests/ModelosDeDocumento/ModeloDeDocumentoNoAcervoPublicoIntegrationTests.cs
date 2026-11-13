namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Net;
using System.Security.Cryptography;
using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O modelo de documento no acervo público (ADR-0132), com Postgres e MinIO reais: a cópia nasce no registro do
/// ato, pela materialização da divulgação, e o objeto é lido anonimamente, como o cidadão o lê. O acervo é
/// provisionado antes de cada teste.
/// </summary>
public sealed class ModeloDeDocumentoNoAcervoPublicoIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>, IAsyncLifetime
{
    private const string BucketPrivado = "uniplus-documentos-acervo-test";

    private readonly AcervoPublicoDeTeste _acervo;
    private readonly SemeadorDoAcervoPublico _semeador;

    public ModeloDeDocumentoNoAcervoPublicoIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        _acervo = new AcervoPublicoDeTeste(dbFixture, minio, BucketPrivado);
        _semeador = new SemeadorDoAcervoPublico(_acervo);
    }

    public Task InitializeAsync() => _acervo.ProvisionarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "O registro do ato copia o modelo congelado para o acervo, na chave do ato, com o nome e a procedência")]
    public async Task RegistroDoAto_CopiaOModeloParaOAcervo()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato);

        await _acervo.DivulgarAsync(ato);

        using HttpResponseMessage resposta = await AcervoPublicoDeTeste.LerAnonimamenteAsync(_acervo.EnderecoNoAcervo(processo.Id, ato, modelo));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        SHA256.HashData(await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(Convert.FromHexString(modelo.HashSha256!));
        AcervoPublicoDeTeste.ConferirNomeDeDownload(resposta, "filename*=UTF-8''Autodeclara%C3%A7%C3%A3o%20%C3%A9tnico-racial.odt");
        AcervoPublicoDeTeste.ConferirProcedencia(
            resposta, ato, "o objeto público carrega o ato que o publicou: o acervo é reconciliável sem consultar o banco");
    }

    [Fact(DisplayName = "Modelo confirmado de processo ainda sem ato não tem objeto no acervo")]
    public async Task ModeloConfirmadoSemAto_NaoTemObjetoNoAcervo()
    {
        await using EnvioDoModeloDeDocumento envio = await EnvioDoModeloDeDocumento.NovoAsync(_acervo.Db, _acervo.Storage(), "PS 2027 — modelo no acervo");
        IniciarEnvioDoModeloDeDocumentoDto iniciar = await envio.IniciarEEnviarAsync(ArquivosDeModeloDeTeste.Odt(), "Autodeclaração étnico-racial");

        Result<ModeloDeDocumentoDto> confirmar = await envio.ConfirmarAsync(iniciar.ModeloDeDocumentoId);

        confirmar.IsSuccess.Should().BeTrue(confirmar.Error?.Message);
        (await _acervo.ObjetosDoProcessoNoAcervoAsync(envio.Processo.Id)).Should().BeEmpty(
            "confirmar é pré-requisito de publicar, não o ato de publicar: o arquivo continua só no bucket privado");
    }

    [Fact(DisplayName = "Falha na cópia para o acervo não avança a divulgação e sai nomeada para a reentrega")]
    public async Task FalhaNaCopia_NaoAvancaADivulgacao()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato);

        Func<Task> divulgar = () => _acervo.DivulgarAsync(ato, bucket: AcervoPublicoDeTeste.BucketInexistente());

        await divulgar.Should().ThrowAsync<AcervoPublicoIndisponivelException>(
            "é a falha que a política de reentrega reagenda em minutos, e não esgota em segundos");
        (await _acervo.CertameFoiDivulgadoAsync(processo.Id)).Should().BeFalse(
            "o link só é divulgado depois de o arquivo estar no acervo — a reentrega tenta de novo");
    }

    /// <summary>
    /// O que uma entrega anterior deixou ao copiar e falhar antes de gravar a divulgação; o conteúdo distinto é só o
    /// que permite ver que ele não foi regravado.
    /// </summary>
    [Fact(DisplayName = "Reentrega com o objeto já no acervo conclui a divulgação sem regravar o objeto")]
    public async Task Reentrega_ComObjetoJaNoAcervo_NaoRegrava()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato);
        byte[] jaCopiado = Encoding.UTF8.GetBytes("cópia de uma entrega anterior");
        await _acervo.GravarNoAcervoAsync(AcervoPublicoDeTeste.ChaveDoModelo(processo.Id, ato, modelo), jaCopiado);

        await _acervo.DivulgarAsync(ato);

        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoNoAcervo(processo.Id, ato, modelo))).Should().Equal(jaCopiado);
        (await _acervo.CertameFoiDivulgadoAsync(processo.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "Retificação que troca o modelo publica o novo no acervo, e o do ato anterior continua acessível")]
    public async Task RetificacaoQueTrocaOModelo_PublicaONovo_EOAnteriorContinua()
    {
        byte[] odtAnterior = ArquivosDeModeloDeTeste.Odt();
        byte[] odtNovo = ArquivosDeModeloDeTeste.Odt(entradaExtra: "styles.xml");
        (ProcessoSeletivo processo, ModeloDeDocumento anterior) = await _semeador.ProcessoComModeloAsync(odtAnterior);
        ModeloDeDocumento novo = await _semeador.ModeloAsync(processo.Id, odtNovo);
        Guid atoAnterior = Guid.CreateVersion7(), atoRetificador = Guid.CreateVersion7();
        string identificador = IdentificadoresDeTeste.NovoValor();
        VersaoConfiguracao v1 = await _semeador.AberturaDivulgadaAsync(processo, anterior, atoAnterior, identificador);

        await _semeador.RetificacaoAsync(v1, novo, atoRetificador, identificador);
        await _acervo.DivulgarAsync(atoRetificador);

        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoNoAcervo(processo.Id, atoRetificador, novo))).Should().Equal(odtNovo);
        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoNoAcervo(processo.Id, atoAnterior, anterior))).Should().Equal(odtAnterior,
            "o ato retificado continua existindo, e o endereço que ele divulgou não muda de conteúdo nem some");
    }
}
