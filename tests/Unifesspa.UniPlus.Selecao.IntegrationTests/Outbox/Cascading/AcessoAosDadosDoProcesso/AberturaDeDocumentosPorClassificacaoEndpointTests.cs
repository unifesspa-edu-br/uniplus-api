namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// A abertura dos documentos de uma inscrição enviada, decidida pela permissão da classificação que a publicação
/// congelou na exigência: cada classificação abre só com a sua permissão, sem hierarquia; o documento de outra inscrição
/// ou de inscrição em rascunho responde o mesmo 404 de quem não tem a permissão; e cada abertura grava uma única decisão.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AberturaDeDocumentosPorClassificacaoEndpointTests : IAsyncLifetime
{
    private readonly ConcessoesDasPersonas _concessoes;
    private readonly RequisicoesAutenticadas _http;
    private readonly DecisoesDeAcessoRegistradas _decisoes;
    private readonly SemeadorDoCenarioDeAcesso _cenarios;

    public AberturaDeDocumentosPorClassificacaoEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _concessoes = new ConcessoesDasPersonas(fixture);
        _http = new RequisicoesAutenticadas(fixture);
        _decisoes = new DecisoesDeAcessoRegistradas(fixture);
        _cenarios = new SemeadorDoCenarioDeAcesso(fixture);
    }

    public Task InitializeAsync() => _concessoes.RemoverDeTodasAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Documento pessoal exige documentos pessoais; o laudo sensível exige documentos sensíveis")]
    public async Task Documentos_PermissaoPelaClassificacaoCongeladaDaExigencia()
    {
        CenarioDeAcessoAosDados cenario = await SemearComPessoaisParaLeandroESensiveisParaBetinaAsync(nameof(Documentos_PermissaoPelaClassificacaoCongeladaDaExigencia));

        using HttpResponseMessage pessoalComPessoais = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(cenario.Arquivos.Pessoal));
        using HttpResponseMessage laudoComPessoais = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(cenario.Arquivos.Sensivel));
        using HttpResponseMessage laudoComSensiveis = await _http.GetAsync(PersonasDoAcesso.Betina, cenario.RotaDoAcesso(cenario.Arquivos.Sensivel));

        pessoalComPessoais.StatusCode.Should().Be(HttpStatusCode.OK, await pessoalComPessoais.Content.ReadAsStringAsync());
        laudoComPessoais.StatusCode.Should().Be(HttpStatusCode.NotFound, "documentos pessoais não abrem o laudo sensível");
        _decisoes.DoDocumento(cenario).Should().ContainSingle(r => r.AtorSubject == PersonasDoAcesso.Leandro && !r.Permitido && r.ResourceId == cenario.Arquivos.Sensivel)
            .Which.Permissao.Should().Be(PermissaoSelecao.DocumentosSensiveisConsultar.Codigo, "a recusa vem da permissão da classe, e não da falta de consultar as inscrições");
        laudoComSensiveis.StatusCode.Should().Be(HttpStatusCode.OK, await laudoComSensiveis.Content.ReadAsStringAsync());
        laudoComSensiveis.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact(DisplayName = "Cada classificação do arquivo abre só com a sua permissão, sem hierarquia; a não resolvida exige a de sensíveis")]
    public async Task Documentos_CadaClassificacaoExigeASuaPermissao()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Documentos_CadaClassificacaoExigeASuaPermissao));
        (Guid Arquivo, PermissaoSelecao Exigida)[] arquivos = ArquivosComAPermissaoQueExigem(cenario);

        foreach (PermissaoSelecao concedida in arquivos.Select(static a => a.Exigida).Distinct())
        {
            await _concessoes.RemoverDaAsync(PersonasDoAcesso.Leandro);
            await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, concedida);

            await ConferirAberturaApenasDoArquivoQueExigeAsync(cenario, arquivos, concedida);
        }
    }

    [Fact(DisplayName = "Arquivo de outra inscrição ou de outro processo responde o mesmo 404 de quem não tem a permissão")]
    public async Task Documento_DeOutraInscricaoOuProcesso_404()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Documento_DeOutraInscricaoOuProcesso_404));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage outraInscricao = await _http.GetAsync(
            PersonasDoAcesso.Leandro, cenario.RotaDoAcessoDoArquivo(Guid.CreateVersion7(), cenario.Arquivos.Sensivel));
        using HttpResponseMessage inexistente = await _http.GetAsync(
            PersonasDoAcesso.Leandro, cenario.RotaDoAcessoDoArquivo(cenario.InscricaoId, Guid.CreateVersion7()));

        outraInscricao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Quem tem só documentos pessoais abre o documento pessoal com uma única decisão permitida e nenhuma negativa de documento sensível")]
    public async Task Documento_GravaUmaUnicaDecisaoDaPermissaoExigida()
    {
        CenarioDeAcessoAosDados cenario = await SemearComPessoaisParaLeandroESensiveisParaBetinaAsync(nameof(Documento_GravaUmaUnicaDecisaoDaPermissaoExigida));

        using HttpResponseMessage pessoalAberto = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(cenario.Arquivos.Pessoal));
        IReadOnlyList<RegistroDecisaoAcesso> doPessoal = _decisoes.DoDocumento(cenario);
        using HttpResponseMessage laudo = await _http.GetAsync(PersonasDoAcesso.Betina, cenario.RotaDoAcesso(cenario.Arquivos.Sensivel));
        IReadOnlyList<RegistroDecisaoAcesso> todas = _decisoes.DoDocumento(cenario);

        pessoalAberto.StatusCode.Should().Be(HttpStatusCode.OK);
        ConferirDecisaoDoDocumentoPessoal(doPessoal, cenario);
        laudo.StatusCode.Should().Be(HttpStatusCode.OK);
        ConferirDecisaoDoLaudo(todas.Skip(1));
    }

    [Fact(DisplayName = "Documento de inscrição em rascunho responde o mesmo 404, ainda que a pessoa tenha as duas permissões")]
    public async Task Documento_DeInscricaoEmRascunho_404()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Documento_DeInscricaoEmRascunho_404));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage rascunho = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcessoDoRascunho());
        using HttpResponseMessage enviada = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(cenario.Arquivos.Pessoal));

        rascunho.StatusCode.Should().Be(HttpStatusCode.NotFound);
        enviada.StatusCode.Should().Be(HttpStatusCode.OK, "a inscrição enviada do mesmo processo continua aberta");
    }

    [Fact(DisplayName = "Sem a permissão de consultar as inscrições, o documento é recusado como o inexistente, antes de decidir a classe, e só a negativa dessa permissão fica registrada")]
    public async Task Documento_SemConsultarInscricoes_RecusadoAntesDaClasse()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Documento_SemConsultarInscricoes_RecusadoAntesDaClasse));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage existente = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(cenario.Arquivos.Pessoal));
        using HttpResponseMessage inexistente = await _http.GetAsync(
            PersonasDoAcesso.Leandro, cenario.RotaDoAcessoDoArquivo(cenario.InscricaoId, Guid.CreateVersion7()));

        existente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(existente.StatusCode);
        _decisoes.DoDocumento(cenario).Should().BeEmpty("nada foi decidido sobre o documento");
        RegistroDecisaoAcesso negativa = _decisoes.DoAtor(PersonasDoAcesso.Leandro).Where(r => r.ProcessoId == cenario.Processo.Id).Should().ContainSingle().Subject;
        negativa.Permissao.Should().Be(PermissaoSelecao.InscricoesConsultar.Codigo);
        negativa.MotivoNegativa.Should().Be(ValoresCanonicos.De(MotivoNegativa.SemConcessaoAplicavel));
    }

    private async Task<CenarioDeAcessoAosDados> SemearComPessoaisParaLeandroESensiveisParaBetinaAsync(string nome)
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nome);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosPessoaisConsultar);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);
        return cenario;
    }

    private static (Guid Arquivo, PermissaoSelecao Exigida)[] ArquivosComAPermissaoQueExigem(CenarioDeAcessoAosDados cenario) =>
    [
        (cenario.Arquivos.Publico, PermissaoSelecao.DocumentosPublicosConsultar),
        (cenario.Arquivos.Interno, PermissaoSelecao.DocumentosInternosConsultar),
        (cenario.Arquivos.Pessoal, PermissaoSelecao.DocumentosPessoaisConsultar),
        (cenario.Arquivos.Identificador, PermissaoSelecao.DocumentosIdentificadoresConsultar),
        (cenario.Arquivos.Sensivel, PermissaoSelecao.DocumentosSensiveisConsultar),
        (cenario.Arquivos.NaoResolvido, PermissaoSelecao.DocumentosSensiveisConsultar),
    ];

    private async Task ConferirAberturaApenasDoArquivoQueExigeAsync(
        CenarioDeAcessoAosDados cenario, (Guid Arquivo, PermissaoSelecao Exigida)[] arquivos, PermissaoSelecao concedida)
    {
        foreach ((Guid arquivo, PermissaoSelecao exigida) in arquivos)
        {
            using HttpResponseMessage resposta = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(arquivo));

            resposta.StatusCode.Should().Be(
                exigida == concedida ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                $"o arquivo que exige {exigida.Codigo} com só {concedida.Codigo} concedida");
        }
    }

    private static void ConferirDecisaoDoDocumentoPessoal(IReadOnlyList<RegistroDecisaoAcesso> decisoes, CenarioDeAcessoAosDados cenario)
    {
        RegistroDecisaoAcesso pessoal = decisoes.Should().ContainSingle().Subject;
        pessoal.Permissao.Should().Be(PermissaoSelecao.DocumentosPessoaisConsultar.Codigo);
        pessoal.Permitido.Should().BeTrue();
        pessoal.InscricaoId.Should().Be(cenario.InscricaoId, "o registro do arquivo nomeia a inscrição, para a consulta por inscrição achá-lo");
    }

    private static void ConferirDecisaoDoLaudo(IEnumerable<RegistroDecisaoAcesso> decisoesPosteriores)
    {
        RegistroDecisaoAcesso sensivel = decisoesPosteriores.Should().ContainSingle().Subject;
        sensivel.Permissao.Should().Be(PermissaoSelecao.DocumentosSensiveisConsultar.Codigo);
        sensivel.Permitido.Should().BeTrue();
        sensivel.Sensibilidade.Should().BeEquivalentTo("sensivel", "o laudo é registrado com a sensibilidade real, e não como pessoal");
    }
}
