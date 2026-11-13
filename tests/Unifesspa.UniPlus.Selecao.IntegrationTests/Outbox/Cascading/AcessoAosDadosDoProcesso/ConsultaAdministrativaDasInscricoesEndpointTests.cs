namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A listagem e o detalhe administrativos das inscrições enviadas, decididos pela permissão de consultar as inscrições na
/// unidade dona do processo e projetados pela permissão de cada classe de dado: o que cada persona vê, o CPF
/// mascarado, os registros de acesso que a consulta grava, a recusa que não distingue a inscrição inexistente da que não
/// se pode ver e o 503 quando o registro de acesso está fora do ar.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ConsultaAdministrativaDasInscricoesEndpointTests : IAsyncLifetime
{
    private readonly ConcessoesDasPersonas _concessoes;
    private readonly RequisicoesAutenticadas _http;
    private readonly DecisoesDeAcessoRegistradas _decisoes;
    private readonly SemeadorDoCenarioDeAcesso _cenarios;

    public ConsultaAdministrativaDasInscricoesEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _concessoes = new ConcessoesDasPersonas(fixture);
        _http = new RequisicoesAutenticadas(fixture);
        _decisoes = new DecisoesDeAcessoRegistradas(fixture);
        _cenarios = new SemeadorDoCenarioDeAcesso(fixture);
    }

    public Task InitializeAsync() => _concessoes.RemoverDeTodasAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Com os dados públicos, pessoais e identificadores a lista traz nome e CPF mascarado, só na unidade dona, e não abre documento")]
    public async Task Inscricoes_ListaComCpfMascarado()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Inscricoes_ListaComCpfMascarado));
        await _concessoes.ConcederAsync(
            PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar,
            PermissaoSelecao.DadosPessoaisConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage lista = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDaLista());
        using HttpResponseMessage outraUnidade = await _http.GetAsync(PersonasDoAcesso.Betina, cenario.RotaDaLista());
        using HttpResponseMessage documento = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoAcesso(cenario.Arquivos.Pessoal));

        await ConferirListaComCpfMascaradoAsync(lista, cenario);
        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        documento.StatusCode.Should().Be(HttpStatusCode.NotFound, "consultar as inscrições não abre documento");
    }

    [Fact(DisplayName = "A listagem grava um registro por inscrição enviada exibida, com a decisão de dados pessoais, e quem só tem o acesso vê a linha vazia")]
    public async Task Lista_GravaUmRegistroPorInscricaoExibida()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Lista_GravaUmRegistroPorInscricaoExibida));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);

        using HttpResponseMessage comDadosPessoais = await _http.GetAsync(PersonasDoAcesso.Diogo, cenario.RotaDaLista());
        IReadOnlyList<RegistroDecisaoAcesso> doDiogo = _decisoes.DosDados(cenario.Processo.Id, PermissaoSelecao.DadosPessoaisConsultar);
        using HttpResponseMessage semDados = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDaLista());

        await ConferirListaComDadosPessoaisAsync(comDadosPessoais, doDiogo, cenario);
        await ConferirListaSemDadosAsync(semDados);
    }

    [Fact(DisplayName = "Detalhe: o arquivo e o dado de cada classe saem pela permissão dela, e cada acesso permitido fica registrado com a inscrição")]
    public async Task Detalhe_ProjetaPelaPermissaoDaClasse()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Detalhe_ProjetaPelaPermissaoDaClasse));
        await _concessoes.ConcederAsync(
            PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar,
            PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosPublicosConsultar);

        using HttpResponseMessage resposta = await _http.GetAsync(PersonasDoAcesso.Diogo, cenario.RotaDoDetalhe(cenario.InscricaoId));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        ConferirCabecalhosDoDetalhe(resposta);
        using JsonDocument json = JsonDocument.Parse(corpo);
        ConferirArquivosDoDetalhe(json.RootElement, cenario);
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain(PersonasDeTeste.NomeFernandaDrumond);
        ConferirDecisoesDoDetalhe(cenario);
    }

    [Fact(DisplayName = "Detalhe: a inscrição que não existe, a de outro processo e o rascunho, pedidos por quem consulta inscrições, respondem 404 sem nenhuma decisão registrada")]
    public async Task Detalhe_SemResourcePedidoPorQuemConsulta_404SemDecisao()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Detalhe_SemResourcePedidoPorQuemConsulta_404SemDecisao));
        CenarioDeAcessoAosDados outroProcesso = await _cenarios.SemearAsync(nameof(Detalhe_SemResourcePedidoPorQuemConsulta_404SemDecisao) + "-outro");
        await _concessoes.ConcederAsync(
            PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Guid inscricaoInexistente = Guid.CreateVersion7();

        using HttpResponseMessage inexistente = await _http.GetAsync(PersonasDoAcesso.Diogo, cenario.RotaDoDetalhe(inscricaoInexistente));
        using HttpResponseMessage deOutroProcesso = await _http.GetAsync(PersonasDoAcesso.Diogo, cenario.RotaDoDetalhe(outroProcesso.InscricaoId));
        using HttpResponseMessage emRascunho = await _http.GetAsync(PersonasDoAcesso.Diogo, cenario.RotaDoDetalhe(cenario.Rascunho.Id));

        ConferirRecusasSemCache(inexistente, deOutroProcesso, emRascunho);
        ConferirQueNenhumaDecisaoNomeia(PersonasDoAcesso.Diogo, cenario.InscricaoId, inscricaoInexistente, outroProcesso.InscricaoId, cenario.Rascunho.Id);
    }

    [Fact(DisplayName = "Detalhe: sem consultar inscrições na unidade dona responde o mesmo 404, com a negativa registrada, e a da inscrição inexistente leva a negativa sem o id")]
    public async Task Detalhe_SemAcessoAoResource_404ComNegativaRegistrada()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Detalhe_SemAcessoAoResource_404ComNegativaRegistrada));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        using HttpResponseMessage resposta = await _http.GetAsync(PersonasDoAcesso.Betina, cenario.RotaDoDetalhe(cenario.InscricaoId));
        using HttpResponseMessage inexistente = await _http.GetAsync(PersonasDoAcesso.Betina, cenario.RotaDoDetalhe(Guid.CreateVersion7()));

        await ConferirRecusaIndistinguivelAsync(resposta, inexistente);
        ConferirNegativasDaConsulta(cenario);
    }

    [Fact(DisplayName = "Com o registro de acesso indisponível, a recusa do detalhe e a consulta permitida de dado pessoal respondem 503")]
    public async Task Detalhe_RegistroIndisponivel_503()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Detalhe_RegistroIndisponivel_503));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);

        using HttpResponseMessage recusada = await _http.GetComRegistroIndisponivelAsync(PersonasDoAcesso.Betina, cenario.RotaDoDetalhe(cenario.InscricaoId));
        using HttpResponseMessage inexistente = await _http.GetComRegistroIndisponivelAsync(PersonasDoAcesso.Betina, cenario.RotaDoDetalhe(Guid.CreateVersion7()));
        using HttpResponseMessage permitida = await _http.GetComRegistroIndisponivelAsync(PersonasDoAcesso.Diogo, cenario.RotaDoDetalhe(cenario.InscricaoId));

        recusada.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        inexistente.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "a inscrição inexistente e a existente respondem o mesmo");
        permitida.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "Detalhe: com o identificador mascarado o CPF de Fernanda sai mascarado, com o componente cpf, e nunca em claro")]
    public async Task Detalhe_CpfSaiMascaradoComOComponente()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Detalhe_CpfSaiMascaradoComOComponente));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);

        using HttpResponseMessage resposta = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoDetalhe(cenario.InscricaoId));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        ConferirCpfMascaradoComOComponente(ItensDasSecoes(json.RootElement).Single(static i => i.GetProperty("fatoCodigo").GetString() == "CPF"));
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Fact(DisplayName = "Só com o acesso às inscrições, a linha e o detalhe trazem o id e o processo, nenhum dado, e o detalhe diz que não pode emitir o comprovante e qual é o último")]
    public async Task SoComAcesso_TrazSoOId()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(SoComAcesso_TrazSoOId));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage lista = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDaLista());
        using HttpResponseMessage detalhe = await _http.GetAsync(PersonasDoAcesso.Leandro, cenario.RotaDoDetalhe(cenario.InscricaoId));

        using JsonDocument jsonLista = JsonDocument.Parse(await lista.Content.ReadAsStringAsync());
        using JsonDocument jsonDetalhe = JsonDocument.Parse(await detalhe.Content.ReadAsStringAsync());
        ConferirLinhasSoComIdEProcesso(jsonLista.RootElement);
        ConferirDetalheSoComIdEProcesso(jsonDetalhe.RootElement);
    }

    [Fact(DisplayName = "Detalhe: quem só pode revelar, sem ver o identificador mascarado, não recebe o item do CPF")]
    public async Task Detalhe_SoRevelar_SemItemDoCpf()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Detalhe_SoRevelar_SemItemDoCpf));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await _http.GetAsync(PersonasDoAcesso.Diogo, cenario.RotaDoDetalhe(cenario.InscricaoId));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        ItensDasSecoes(json.RootElement).Should().NotContain(static i => i.GetProperty("fatoCodigo").GetString() == "CPF");
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Mascarado);
    }

    [Fact(DisplayName = "Serialização defensiva: a resposta que não passou pelo projetor sai sem nenhum campo classificado")]
    public async Task Serializacao_OmiteCampoClassificadoSemDecisao()
    {
        CenarioDeAcessoAosDados cenario = await _cenarios.SemearAsync(nameof(Serializacao_OmiteCampoClassificadoSemDecisao));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Diogo, cenario.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        using HttpResponseMessage resposta = await _http.GetAsync(
            PersonasDoAcesso.Diogo, $"/api/selecao/_teste/processos-seletivos/{cenario.Processo.Id}/inscricoes/{cenario.InscricaoId}/esquecida");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.EnumerateObject().Select(static p => p.Name).Should().Equal(
            ["id", "processo"], "sem decisão permitida na requisição só o id e o processo, que não são classificados, saem");
    }

    private static IEnumerable<JsonElement> ItensDasSecoes(JsonElement detalhe) =>
        detalhe.GetProperty("secoes").EnumerateArray().SelectMany(static s => s.GetProperty("itens").EnumerateArray());

    private static async Task ConferirListaComCpfMascaradoAsync(HttpResponseMessage lista, CenarioDeAcessoAosDados cenario)
    {
        string corpo = await lista.Content.ReadAsStringAsync();
        lista.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        JsonElement item = json.RootElement.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == cenario.InscricaoId);
        item.GetProperty("cpf").GetProperty("valor").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        item.GetProperty("cpf").GetProperty("podeRevelar").GetBoolean().Should().BeFalse("a revelação é pedida no detalhe, por campo");
        item.TryGetProperty("numeroInscricao", out _).Should().BeFalse("o número é dado interno");
        item.GetProperty("nome").GetProperty("valor").GetString().Should().Be(PersonasDeTeste.NomeSocialFernandaDrumond);
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        json.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(cenario.Rascunho.Id, "o rascunho não é inscrição enviada");
    }

    private static async Task ConferirListaComDadosPessoaisAsync(
        HttpResponseMessage resposta, IReadOnlyList<RegistroDecisaoAcesso> registros, CenarioDeAcessoAosDados cenario)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue("a lista traz nome e CPF mascarado");
        registros.Select(static r => r.ResourceId).Should().BeEquivalentTo(
            [cenario.InscricaoId, cenario.InscricaoDeErick, cenario.InscricaoDaServidora, cenario.InscricaoComGrupo], "um registro por inscrição enviada exibida");
        registros.Should().OnlyContain(r => r.Permitido && r.AtorSubject == PersonasDoAcesso.Diogo && r.InscricaoId == r.ResourceId);
    }

    private static async Task ConferirListaSemDadosAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        corpo.Should().NotContain(PersonasDeTeste.NomeSocialFernandaDrumond).And.NotContain(PersonasDeTeste.FernandaDrumond.Mascarado);
    }

    private static void ConferirCabecalhosDoDetalhe(HttpResponseMessage resposta)
    {
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.inscricao-administrativa.v1+json");
    }

    private static void ConferirArquivosDoDetalhe(JsonElement detalhe, CenarioDeAcessoAosDados cenario)
    {
        detalhe.GetProperty("id").GetGuid().Should().Be(cenario.InscricaoId);
        detalhe.TryGetProperty("numeroInscricao", out _).Should().BeFalse("o número é dado interno");
        Guid[] arquivos = [.. detalhe.GetProperty("arquivos").EnumerateArray().Select(static a => a.GetProperty("arquivoId").GetGuid())];
        arquivos.Should().Contain([cenario.Arquivos.Pessoal, cenario.Arquivos.Publico])
            .And.NotContain([cenario.Arquivos.Sensivel, cenario.Arquivos.Interno, cenario.Arquivos.Identificador]);
        detalhe.GetProperty("arquivos").EnumerateArray()
            .ToDictionary(static a => a.GetProperty("arquivoId").GetGuid(), static a => a.GetProperty("classificacaoProtecao").GetString())
            .Should().Contain(new KeyValuePair<Guid, string?>(cenario.Arquivos.Pessoal, "PESSOAL"))
            .And.Contain(new KeyValuePair<Guid, string?>(cenario.Arquivos.Publico, "PUBLICO"));
    }

    private void ConferirDecisoesDoDetalhe(CenarioDeAcessoAosDados cenario)
    {
        string[] registradas = [.. _decisoes.DoAtor(PersonasDoAcesso.Diogo)
            .Where(r => r.ProcessoId == cenario.Processo.Id && r.InscricaoId == cenario.InscricaoId)
            .Select(static r => r.Permissao)];
        registradas.Should().Contain([PermissaoSelecao.InscricoesConsultar.Codigo, PermissaoSelecao.DocumentosPessoaisConsultar.Codigo])
            .And.NotContain(PermissaoSelecao.DocumentosSensiveisConsultar.Codigo, "a negativa do projetor é omissão silenciosa");
    }

    private static void ConferirRecusasSemCache(params HttpResponseMessage[] recusas)
    {
        foreach (HttpResponseMessage recusa in recusas)
        {
            recusa.StatusCode.Should().Be(HttpStatusCode.NotFound);
            recusa.Headers.CacheControl!.NoStore.Should().BeTrue("o erro do detalhe também não é guardado em cache");
        }
    }

    private void ConferirQueNenhumaDecisaoNomeia(string persona, params Guid[] envolvidas) =>
        _decisoes.DoAtor(persona)
            .Where(r => envolvidas.Contains(r.InscricaoId ?? Guid.Empty) || envolvidas.Contains(r.ResourceId ?? Guid.Empty))
            .Should().BeEmpty("sem resource não há sobre o que decidir");

    private static async Task ConferirRecusaIndistinguivelAsync(HttpResponseMessage recusada, HttpResponseMessage inexistente)
    {
        recusada.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await recusada.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.NomeFernandaDrumond);
        recusada.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await RecusaAsync(recusada)).Should().BeEquivalentTo(await RecusaAsync(inexistente), "a recusa não distingue a falta de permissão da inscrição que não existe");
    }

    private void ConferirNegativasDaConsulta(CenarioDeAcessoAosDados cenario)
    {
        RegistroDecisaoAcesso[] negativas = [.. _decisoes.DoAtor(PersonasDoAcesso.Betina).Where(r => r.ProcessoId == cenario.Processo.Id)];
        negativas.Should().HaveCount(2, "só o acesso ao resource é decidido, e o conteúdo não é carregado").And.OnlyContain(r => !r.Permitido);
        string consultar = PermissaoSelecao.InscricoesConsultar.Codigo;
        negativas.Should().OnlyContain(r => r.Permissao == consultar);
        negativas.Should().ContainSingle(r => r.InscricaoId == cenario.InscricaoId);
        negativas.Should().ContainSingle(r => r.InscricaoId == null && r.ResourceId == null, "o consumidor reconhece a sondagem pela ausência do id");
    }

    private static async Task<(int Status, string? Code, string? Type, string? Title, string? Detail)> RecusaAsync(HttpResponseMessage resposta)
    {
        using JsonDocument json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = json.RootElement;
        string? Texto(string nome) => raiz.TryGetProperty(nome, out JsonElement valor) ? valor.GetString() : null;
        return ((int)resposta.StatusCode, Texto("code"), Texto("type"), Texto("title"), Texto("detail"));
    }

    private static void ConferirCpfMascaradoComOComponente(JsonElement cpf)
    {
        cpf.GetProperty("componente").GetString().Should().Be("cpf");
        cpf.GetProperty("valor").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        cpf.GetProperty("podeRevelar").GetBoolean().Should().BeFalse();
        cpf.GetProperty("descricao").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static void ConferirLinhasSoComIdEProcesso(JsonElement lista) =>
        lista.EnumerateArray().Select(static i => string.Join(",", i.EnumerateObject().Select(static p => p.Name)))
            .Should().OnlyContain(static nomes => nomes == "id,processo");

    private static void ConferirDetalheSoComIdEProcesso(JsonElement detalhe)
    {
        // podeEmitirComprovante é capacidade de quem consulta e numeroDoUltimoComprovante é metadado da ordem de emissão, sem dado do candidato: saem ainda que nenhum dado seja permitido.
        detalhe.EnumerateObject().Select(static p => p.Name)
            .Should().BeEquivalentTo(["id", "processo", "secoes", "derivados", "arquivos", "podeEmitirComprovante", "numeroDoUltimoComprovante"]);
        detalhe.GetProperty("numeroDoUltimoComprovante").GetInt32().Should().Be(1, "o cenário emitiu um só comprovante");
        detalhe.GetProperty("podeEmitirComprovante").GetBoolean().Should().BeFalse("consultar as inscrições não dá a permissão de emitir");
        detalhe.GetProperty("processo").GetProperty("nome").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
