namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O documento do edital e os anexos no acervo público (ADR-0132), com Postgres e MinIO reais: a cópia nasce no
/// registro do ato, pela materialização da divulgação, na chave de cada ato, e o objeto é lido anonimamente, como o
/// cidadão o lê. O acervo é provisionado antes de cada teste.
/// </summary>
public sealed class DocumentosDoEditalNoAcervoPublicoIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>, IAsyncLifetime
{
    private const string BucketPrivado = "uniplus-documentos-acervo-test";

    private readonly AcervoPublicoDeTeste _acervo;
    private readonly SemeadorDoAcervoPublico _semeador;

    public DocumentosDoEditalNoAcervoPublicoIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        _acervo = new AcervoPublicoDeTeste(dbFixture, minio, BucketPrivado);
        _semeador = new SemeadorDoAcervoPublico(_acervo);
    }

    public Task InitializeAsync() => _acervo.ProvisionarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A cópia do ato copia o edital para o acervo, na chave da ADR-0132, com nome legível, e o certame divulga o endereço")]
    public async Task RegistroDoAto_CopiaOEditalParaOAcervo()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        byte[] pdf = SemeadorDoAcervoPublico.ArquivoDeEdital();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id, pdf);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato, identificador, edital);

        await _acervo.CopiarAoAcervoAsync(ato);

        await ConferirEditalLidoAnonimamenteAsync(identificador, ato, edital, pdf);
        await ConferirEditalRegistradoNoCertameAsync(processo.Id, identificador, ato, edital, pdf.LongLength);
    }

    [Fact(DisplayName = "A cópia do ato copia os anexos congelados para o acervo, byte a byte, com o nome de cada um")]
    public async Task RegistroDoAto_CopiaOsAnexosParaOAcervo()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        AnexoSemeado quadro = await _semeador.AnexoPdfAsync(processo.Id, "Quadro de vagas", "quadro");
        AnexoSemeado formulario = await _semeador.AnexoAsync(
            processo.Id, "Formulário de recurso", TiposDeMidiaDeDocumento.Docx, ArquivosDeModeloDeTeste.Docx());
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato, identificador, edital, anexos: [quadro.Anexo, formulario.Anexo]);

        await _acervo.CopiarAoAcervoAsync(ato);

        await ConferirAnexoLidoAnonimamenteAsync(processo.Id, ato, quadro);
        await ConferirAnexoLidoAnonimamenteAsync(processo.Id, ato, formulario);
        await ConferirDocumentosDivulgadosAsync(processo.Id, ato, identificador, edital, [quadro, formulario]);
    }

    [Fact(DisplayName = "Anexo congelado sem cópia selada com o conteúdo congelado não é publicado, e a entrada do ato continua sem endereço")]
    public async Task AnexoComOutroConteudo_NaoEPublicado()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        AnexoSemeado quadro = await _semeador.AnexoPdfAsync(processo.Id, "Quadro de vagas", "quadro");
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato, anexos: [quadro.Anexo with { HashSha256 = new string('e', 64) }]);

        Func<Task> divulgar = () => _acervo.CopiarAoAcervoAsync(ato);

        await divulgar.Should().ThrowAsync<InvalidOperationException>(
            "o endereço do anexo apontaria para um arquivo que não é o que o edital atestou");
        await ConferirEntradaSemEnderecoAsync(processo.Id, ato);
    }

    [Fact(DisplayName = "Retificação que troca um anexo publica o novo em endereço próprio, e os anexos do ato anterior continuam acessíveis")]
    public async Task RetificacaoQueTrocaUmAnexo_PublicaONovo_EOsAnterioresContinuam()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        AnexoSemeado mantido = await _semeador.AnexoPdfAsync(processo.Id, "Cronograma", "cronograma");
        AnexoSemeado trocado = await _semeador.AnexoPdfAsync(processo.Id, "Quadro de vagas", "quadro 1");
        AnexoSemeado novo = await _semeador.AnexoPdfAsync(processo.Id, "Quadro de vagas", "quadro 2");
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7(), retificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await _semeador.AberturaDivulgadaAsync(processo, modelo, abertura, identificador, edital, [mantido.Anexo, trocado.Anexo]);
        await _semeador.RetificacaoDivulgadaAsync(v1, modelo, retificacao, identificador, edital, [mantido.Anexo, novo.Anexo]);

        await ConferirAnexosDeCadaAtoAsync(processo.Id, (abertura, retificacao), mantido, trocado, novo);
    }

    [Fact(DisplayName = "Edital de ato publicado cuja cópia ainda não aconteceu não tem objeto no acervo")]
    public async Task EditalSemCopia_NaoTemObjetoNoAcervo()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        string identificador = IdentificadoresDeTeste.NovoValor();
        await _semeador.VersaoAsync(processo, modelo, Guid.CreateVersion7(), identificador, edital);

        string prefixo = $"editais/{AcervoPublicoDeTeste.AnoDoAto:D4}/{AcervoPublicoDeTeste.UnidadeDoEnvelope}/edital-abertura/{identificador}/";
        (await _acervo.ObjetosNoAcervoAsync(prefixo)).Should().BeEmpty(
            "a cópia é posterior à publicação: até ela acontecer, o edital continua só no bucket privado");
    }

    [Fact(DisplayName = "Reentrega com o edital já no acervo conclui a divulgação sem regravar o objeto")]
    public async Task Reentrega_ComEditalJaNoAcervo_NaoRegrava()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato, identificador, edital);
        byte[] jaCopiado = Encoding.UTF8.GetBytes("cópia de uma entrega anterior");
        await _acervo.GravarNoAcervoAsync(AcervoPublicoDeTeste.ChaveDoEdital(identificador, ato, edital), jaCopiado);

        await _acervo.CopiarAoAcervoAsync(ato);

        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDoEdital(identificador, ato, edital))).Should().Equal(jaCopiado);
        (await _acervo.AtosDivulgadosAsync(processo.Id)).Single().UrlDownload.Should().NotBeNull();
    }

    /// <summary>
    /// Duas retificações sucessivas com o mesmo documento repetem ano, unidade, tipo, processo e hash: é o
    /// identificador do ato que separa os objetos.
    /// </summary>
    [Fact(DisplayName = "Retificação que reaproveita o documento do edital publica outro objeto, e o do ato anterior continua acessível")]
    public async Task RetificacaoQueReaproveitaOEdital_PublicaOutroObjeto()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        byte[] pdf = SemeadorDoAcervoPublico.ArquivoDeEdital();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id, pdf);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7(), primeira = Guid.CreateVersion7(), segunda = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await _semeador.AberturaDivulgadaAsync(processo, modelo, abertura, identificador, edital);
        VersaoConfiguracao v2 = await _semeador.RetificacaoDivulgadaAsync(v1, modelo, primeira, identificador, edital);
        await _semeador.RetificacaoDivulgadaAsync(v2, modelo, segunda, identificador, edital);

        await ConferirUmObjetoPorAtoAsync(identificador, edital, pdf, abertura, [primeira, segunda]);
        await ConferirUmEnderecoPorAtoAsync(processo.Id, identificador, edital, [abertura, primeira, segunda]);
    }

    /// <summary>A cópia da retificação termina antes da do ato que ela retifica.</summary>
    [Fact(DisplayName = "A cópia de uma versão anterior, depois da seguinte, preenche só a entrada dela sem recuar a divulgação")]
    public async Task AtoAtrasado_RegistraOEdital_SemRecuarAVersao()
    {
        (Guid processoId, Guid abertura, Guid retificacao) = await SemearAberturaERetificacaoAsync();

        await _acervo.CopiarAoAcervoAsync(retificacao);
        (await _acervo.CertameDivulgadoAsync(processoId)).CopiouOsDocumentosDoAto(abertura).Should().BeFalse();
        await _acervo.CopiarAoAcervoAsync(abertura);

        await ConferirRetificacaoPreservadaAsync(processoId, abertura, retificacao);
    }

    private async Task<(Guid ProcessoId, Guid Abertura, Guid Retificacao)> SemearAberturaERetificacaoAsync()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7(), retificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await _semeador.VersaoAsync(processo, modelo, abertura, identificador, edital);
        await _semeador.RetificacaoAsync(v1, modelo, retificacao, identificador, edital);

        return (processo.Id, abertura, retificacao);
    }

    private async Task ConferirRetificacaoPreservadaAsync(Guid processoId, Guid abertura, Guid retificacao)
    {
        CertameDivulgado divulgado = await _acervo.CertameDivulgadoAsync(processoId);

        divulgado.NumeroVersao.Should().Be(2, "a cópia atrasada não desfaz a retificação já divulgada");
        divulgado.AtoCriadorId.Should().Be(retificacao);
        divulgado.AtosDivulgados.Select(static e => e.AtoId).Should().BeEquivalentTo([abertura, retificacao]);
        divulgado.CopiouOsDocumentosDoAto(abertura).Should().BeTrue();
        JsonNode.Parse(divulgado.DocumentoPublico)!["atoCriadorId"]!.GetValue<Guid>().Should().Be(retificacao, "o documento do certame continua o da versão 2");
    }

    [Fact(DisplayName = "Reentrega do mesmo ato não duplica o edital registrado")]
    public async Task Reentrega_DoMesmoAto_NaoDuplicaOEdital()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato, edital: edital);
        await _acervo.CopiarAoAcervoAsync(ato);

        await _acervo.CopiarAoAcervoAsync(ato);

        (await _acervo.AtosDivulgadosAsync(processo.Id)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Falha na cópia do edital de uma retificação mantém a entrada dela sem endereço")]
    public async Task FalhaNaCopiaDoEdital_NaoRegistraOEdital()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        string identificador = IdentificadoresDeTeste.NovoValor();
        Guid abertura = Guid.CreateVersion7(), retificacao = Guid.CreateVersion7();
        VersaoConfiguracao v1 = await _semeador.AberturaDivulgadaAsync(processo, modelo, abertura, identificador, edital);
        await _semeador.RetificacaoAsync(v1, modelo, retificacao, identificador, edital);

        Func<Task> divulgar = () => _acervo.CopiarAoAcervoAsync(retificacao, AcervoPublicoDeTeste.BucketInexistente());

        await divulgar.Should().ThrowAsync<AcervoPublicoIndisponivelException>();
        (await _acervo.AtosDivulgadosAsync(processo.Id)).Where(static e => e.UrlDownload is not null).Select(static e => e.AtoId).Should().Equal(
            [abertura], "o link só aparece depois de o arquivo estar no acervo");
        await ConferirEntradaSemEnderecoAsync(processo.Id, retificacao);
    }

    /// <summary>O envelope aponta o documento cadastrado, mas com um hash que não é o dele.</summary>
    [Fact(DisplayName = "Edital sem cópia selada com o conteúdo congelado não é publicado, e a entrada do ato continua sem endereço")]
    public async Task EditalComOutroConteudo_NaoEPublicado()
    {
        (ProcessoSeletivo processo, ModeloDeDocumento modelo) = await _semeador.ProcessoComModeloAsync();
        DocumentoAPublicar edital = await _semeador.EditalAsync(processo.Id);
        Guid ato = Guid.CreateVersion7();
        await _semeador.VersaoAsync(processo, modelo, ato, edital: edital, hashDoEditalCongelado: new string('e', 64));

        Func<Task> divulgar = () => _acervo.CopiarAoAcervoAsync(ato);

        await divulgar.Should().ThrowAsync<InvalidOperationException>(
            "o endereço divulgado apontaria para um arquivo que não é o que o ato oficializou");
        await ConferirEntradaSemEnderecoAsync(processo.Id, ato);
    }

    private async Task ConferirEntradaSemEnderecoAsync(Guid processoId, Guid ato)
    {
        (await _acervo.CertameDivulgadoAsync(processoId)).CopiouOsDocumentosDoAto(ato).Should().BeFalse(
            "o endereço só é registrado depois de a cópia ao acervo terminar");
    }

    private async Task ConferirEditalLidoAnonimamenteAsync(string identificador, Guid ato, DocumentoAPublicar edital, byte[] pdf)
    {
        using HttpResponseMessage resposta = await AcervoPublicoDeTeste.LerAnonimamenteAsync(_acervo.EnderecoDoEdital(identificador, ato, edital));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(TiposDeMidiaDeDocumento.Pdf);
        string disposicao = resposta.Content.Headers.NonValidated["Content-Disposition"].ToString();
        disposicao.Should().StartWith("inline;", "o link do portal abre o edital em nova aba; como anexo, a aba ficaria em branco");
        AcervoPublicoDeTeste.ConferirNomeDeDownload(
            resposta,
            $"filename*=UTF-8''{identificador}-edital-abertura.pdf",
            "sem o nome de apresentação, o cidadão baixaria um arquivo chamado pelo resumo criptográfico");
    }

    private async Task ConferirEditalRegistradoNoCertameAsync(Guid processoId, string identificador, Guid ato, DocumentoAPublicar edital, long tamanhoDoPdf)
    {
        CertameDivulgado divulgado = await _acervo.CertameDivulgadoAsync(processoId);
        Uri endereco = AcervoDeTeste.Endereco.De(AcervoPublicoDeTeste.ChaveDoEdital(identificador, ato, edital));
        JsonNode.Parse(divulgado.DocumentoPublico)!["documentoEdital"]!["urlDownload"]!.GetValue<string>().Should().Be(
            endereco.ToString(), "o contrato divulga o endereço da mesma chave para a qual a divulgação copiou");
        AtoDivulgado registrado = divulgado.AtosDivulgados.Should().ContainSingle().Subject;
        registrado.UrlDownload.Should().Be(endereco.OriginalString, "o endereço registrado para o ato é o da chave para a qual a cópia foi feita");
        registrado.Documentos.Should().ContainSingle().Which.Should().Be(new DocumentoDivulgado(
            PapelDoDocumentoDivulgado.Principal, $"{identificador}-edital-abertura.pdf", TiposDeMidiaDeDocumento.Pdf, tamanhoDoPdf, edital.HashSha256!, endereco.OriginalString));
    }

    private async Task ConferirAnexoLidoAnonimamenteAsync(Guid processoId, Guid ato, AnexoSemeado semeado)
    {
        using HttpResponseMessage resposta = await AcervoPublicoDeTeste.LerAnonimamenteAsync(_acervo.EnderecoDoAnexo(processoId, ato, semeado.Anexo));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(semeado.Conteudo);
        SHA256.HashData(semeado.Conteudo).Should().Equal(Convert.FromHexString(semeado.Anexo.HashSha256));
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(semeado.Anexo.TipoDeMidia);
        AcervoPublicoDeTeste.ConferirNomeDeDownload(resposta, $"filename*=UTF-8''{Uri.EscapeDataString(semeado.Anexo.NomeArquivo)}");
    }

    private async Task ConferirDocumentosDivulgadosAsync(
        Guid processoId, Guid ato, string identificador, DocumentoAPublicar edital, AnexoSemeado[] anexos)
    {
        IReadOnlyList<DocumentoDivulgado> documentos = (await _acervo.AtosDivulgadosAsync(processoId)).Single().Documentos!;
        documentos.Select(static d => (d.Papel, d.Nome, d.TamanhoBytes)).Should().Equal(
            (PapelDoDocumentoDivulgado.Principal, $"{identificador}-edital-abertura.pdf", edital.TamanhoBytes!.Value),
            (PapelDoDocumentoDivulgado.Anexo, "Quadro de vagas.pdf", anexos[0].Conteudo.LongLength),
            (PapelDoDocumentoDivulgado.Anexo, "Formulário de recurso.docx", anexos[1].Conteudo.LongLength));
        documentos[1].UrlDownload.Should().Be(
            AcervoDeTeste.Endereco.De(ChaveNoAcervoPublico.DoAnexo(processoId, ato, anexos[0].Anexo)).OriginalString);
    }

    private async Task ConferirAnexosDeCadaAtoAsync(
        Guid processoId, (Guid Abertura, Guid Retificacao) atos, AnexoSemeado mantido, AnexoSemeado trocado, AnexoSemeado novo)
    {
        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDoAnexo(processoId, atos.Retificacao, novo.Anexo))).Should().Equal(novo.Conteudo);
        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDoAnexo(processoId, atos.Retificacao, mantido.Anexo))).Should().Equal(mantido.Conteudo,
            "o anexo herdado também é copiado para o ato da nova versão: cada versão tem os endereços dos próprios arquivos");
        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDoAnexo(processoId, atos.Abertura, trocado.Anexo))).Should().Equal(trocado.Conteudo,
            "o ato retificado continua existindo, e o anexo que ele divulgou não some");
        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDoAnexo(processoId, atos.Abertura, mantido.Anexo))).Should().Equal(mantido.Conteudo);
        IReadOnlyList<AtoDivulgado> registrados = await _acervo.AtosDivulgadosAsync(processoId);
        registrados[0].Documentos!.Count(static d => d.Papel == PapelDoDocumentoDivulgado.Anexo).Should().Be(2);
        registrados[1].Documentos!.Where(static d => d.Papel == PapelDoDocumentoDivulgado.Anexo).Select(static d => d.Nome)
            .Should().Equal("Cronograma.pdf", "Quadro de vagas.pdf");
    }

    private async Task ConferirUmObjetoPorAtoAsync(string identificador, DocumentoAPublicar edital, byte[] pdf, Guid abertura, Guid[] retificacoes)
    {
        foreach (Guid ato in retificacoes)
        {
            using HttpResponseMessage resposta = await AcervoPublicoDeTeste.LerAnonimamenteAsync(
                _acervo.EnderecoDoEdital(identificador, ato, edital, AcervoPublicoDeTeste.Retificacao));
            (await resposta.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);
            AcervoPublicoDeTeste.ConferirProcedencia(
                resposta, ato, "cada ato tem o próprio objeto, com a própria procedência — sem o ato na chave, a segunda retificação cairia no objeto da primeira");
        }

        (await AcervoPublicoDeTeste.BaixarAnonimamenteAsync(_acervo.EnderecoDoEdital(identificador, abertura, edital))).Should().Equal(
            pdf, "o ato retificado continua existindo, e o endereço que ele divulgou não some");
    }

    private async Task ConferirUmEnderecoPorAtoAsync(Guid processoId, string identificador, DocumentoAPublicar edital, Guid[] atos)
    {
        IReadOnlyList<AtoDivulgado> registrados = await _acervo.AtosDivulgadosAsync(processoId);
        registrados.Select(static e => e.AtoId).Should().Equal(atos);
        registrados.Select(static e => e.UrlDownload).Should().OnlyHaveUniqueItems(
            "o documento é o mesmo e o hash coincide: só o ato separa o endereço de cada versão");
        registrados[2].UrlDownload.Should().Be(AcervoDeTeste.Endereco.De(
            AcervoPublicoDeTeste.ChaveDoEdital(identificador, atos[2], edital, AcervoPublicoDeTeste.Retificacao)).OriginalString);
    }
}
