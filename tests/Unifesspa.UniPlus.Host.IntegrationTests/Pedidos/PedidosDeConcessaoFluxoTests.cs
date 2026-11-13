namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O pedido de concessão pelo host real: Postgres, RabbitMQ, MinIO e Mailpit em contêineres. Cobre as decisões (perfil
/// interno vale com o gestor, sensível passa pelas duas etapas, pessoas distintas, recusa e cancelamento) e o fim da
/// vigência definido pelo gestor; a criação, o aceite, as filas e a visibilidade ficam em
/// <see cref="PedidosDeConcessaoCriacaoEVisibilidadeTests"/>.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PedidosDeConcessaoFluxoTests : PedidosDeConcessaoTestBase
{
    public PedidosDeConcessaoFluxoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Pedido de perfil só interno vale com a aprovação do gestor e a trilha da concessão traz o pedido, sem e-mail no log")]
    public async Task PerfilInterno_ValeComOGestor()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await AceitoAposARecusaDoGestorAsync(diogo, perfil, unidade);

        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        aprovado.GetProperty("situacao").GetString().Should().Be("aprovado");
        Concessao concessao = (await ConcessoesAsync(Leandro, perfil)).Single();
        ConferirConcessaoDoPedido(concessao, id, unidade);
        aprovado.GetProperty("decisoes").EnumerateArray().Single().GetProperty("decisor").GetProperty("nome").GetString().Should().Be(Diogo.Nome);
        aprovado.GetProperty("solicitante").GetProperty("nome").GetString().Should().Be(Betina.Nome);
        aprovado.GetProperty("concessaoId").GetGuid().Should().Be(concessao.Id);
        await ConferirTrilhasAsync(diogo, id, concessao.Id);
        LerLog().Should().NotContain(Leandro.Email).And.NotContain(Betina.Email);
    }

    [Fact(DisplayName = "O gestor define o fim da vigência ao aprovar, e a concessão nasce com ele; a sugestão do solicitante só informa")]
    public async Task FimDeVigencia_DefinidoPeloGestor_VaiParaAConcessao()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        DateTimeOffset definida = DateTimeOffset.UtcNow.AddMonths(3);
        Guid id = await CriarComSugestaoAsync(perfil, unidade, DateTimeOffset.UtcNow.AddMonths(6));

        JsonElement aprovado = await CorpoAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", new { validaAte = definida }), HttpStatusCode.OK);

        aprovado.GetProperty("validaAte").GetDateTimeOffset().Should().BeCloseTo(definida, TimeSpan.FromSeconds(1));
        (await ConcessoesAsync(Betina, perfil)).Single().ValidaAte.Should().BeCloseTo(definida, TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "Fim da vigência com offset diferente de UTC é aceito e a concessão guarda o mesmo instante")]
    public async Task FimDeVigencia_ComOffset_GravaOMesmoInstante()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        DateTimeOffset definida = DateTimeOffset.UtcNow.AddMonths(3).ToOffset(TimeSpan.FromHours(-3));
        Guid id = await CriarAsync(betina, perfil, unidade, null);

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", new { validaAte = definida });

        await CorpoAsync(resposta, HttpStatusCode.OK);
        (await ConcessoesAsync(Betina, perfil)).Single().ValidaAte.Should().BeCloseTo(definida, TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "Fim da vigência que não é posterior ao instante recusa a aprovação com 422 e o pedido segue aguardando o gestor")]
    public async Task FimDeVigencia_NoPassado_RecusaAAprovacao()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, null);

        await ConferirRecusaAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", new { validaAte = DateTimeOffset.UtcNow.AddDays(-1) }),
            HttpStatusCode.UnprocessableEntity,
            "validade_invalida");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
    }

    [Fact(DisplayName = "Pedido de perfil sensível passa pelas duas aprovações, e a concessão só nasce na segunda")]
    public async Task PerfilSensivel_PassaPelasDuasEtapas()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await AceitoPorLeandroAsync(perfil, unidade);

        await ConferirPrimeiraEtapaAsync(diogo, marlene, id, perfil);
        JsonElement protecao = await CorpoAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        protecao.GetProperty("situacao").GetString().Should().Be("aprovado");
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle().Which.ConcedidaPorSub.Should().Be(Marlene.Sub.ToString());
        JsonElement[] decisoes = [.. protecao.GetProperty("decisoes").EnumerateArray()];
        decisoes.Select(d => d.GetProperty("etapa").GetString()).Should().Equal("gestor", "protecao_de_dados");
        decisoes[0].GetProperty("sensibilidadeAvaliada").GetString().Should().Be("sensivel");
        (await ListarFilaAsync(marlene, "protecao_de_dados")).Should().NotContain(id);
    }

    [Fact(DisplayName = "A proteção de dados não decide antes do gestor, o gestor não decide a etapa dela, e sem permissão a resposta é 403")]
    public async Task EtapaDaPermissao_ConferidaPelaSituacao()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await AceitoPorLeandroAsync(perfil, unidade);

        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "etapa_invalida");
        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "Cedo demais." }), HttpStatusCode.UnprocessableEntity, "etapa_invalida");
        await ConferirSemPermissaoAsync(betina, id);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");

        await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "etapa_invalida");
    }

    [Fact(DisplayName = "Token sem o que identifica o solicitante responde 401 antes de qualquer decisão de acesso")]
    public async Task IdentidadeIncompleta_Devolve401()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, null);
        using HttpClient semJti = await ClienteSemJtiAsync(Diogo, AprovarFuncional);

        using HttpResponseMessage resposta = await EnviarAsync(semJti, HttpMethod.Post, $"{Rota}/{id}/aprovacoes");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
    }

    [Theory(DisplayName = "Ninguém aprova pedido em que é solicitante ou beneficiário")]
    [InlineData("solicitante")]
    [InlineData("beneficiario")]
    public async Task Parte_NaoAprova(string parte)
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient solicitante = parte == "solicitante" ? await ClienteComPermissoesAsync(Diogo, AprovarFuncional) : Cliente(Betina);
        using HttpClient beneficiario = parte == "solicitante" ? Cliente(Leandro) : await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(solicitante, perfil, unidade, parte == "solicitante" ? Leandro : Diogo);
        (await AceitarAsync(beneficiario, id)).StatusCode.Should().Be(HttpStatusCode.OK);
        using HttpClient aprovador = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        await ConferirRecusaAsync(await EnviarAsync(aprovador, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "decisor_e_parte");
        await ConferirRecusaAsync(await EnviarAsync(aprovador, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "Sou parte." }), HttpStatusCode.UnprocessableEntity, "decisor_e_parte");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A mesma pessoa não aprova as duas etapas, mesmo com as duas permissões")]
    public async Task MesmaPessoaNasDuasEtapas_Recusa()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient betina = Cliente(Betina);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional, AprovarProtecaoDeDados);
        Guid id = await AceitoPorLeandroAsync(perfil, unidade);
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirRecusaAsync(await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");
        await ConferirRecusaAsync(await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "De novo." }), HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_protecao_de_dados");
    }

    [Fact(DisplayName = "Recusa encerra o pedido com o motivo, sem concessão, e a trilha a registra; sem motivo é recusada")]
    public async Task Recusa_EncerraSemConcessao()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, null);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "  " }), HttpStatusCode.UnprocessableEntity, "motivo_invalido");
        JsonElement recusado = await CorpoAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "função não exige acesso a laudos" }), HttpStatusCode.OK);

        recusado.GetProperty("situacao").GetString().Should().Be("recusado");
        recusado.GetProperty("concessaoId").ValueKind.Should().Be(JsonValueKind.Null);
        (await ConcessoesAsync(Betina, perfil)).Should().BeEmpty();
        await ConferirRecusaNaTrilhaENasFilasAsync(betina, diogo, id);
    }

    [Fact(DisplayName = "O solicitante cancela antes da primeira decisão e o pedido sai das filas; outra pessoa recebe 404, e depois da decisão não cancela")]
    public async Task Cancelamento_SoDoSolicitanteAntesDaDecisao()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid cancelado = await CriarAsync(betina, perfil, unidade, null);
        (await ListarFilaAsync(diogo, "gestor")).Should().Contain(cancelado);

        await CancelarSoPeloSolicitanteAsync(betina, cancelado);

        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(cancelado);
        (await TiposDaTrilhaAsync(betina, cancelado)).Should().Equal("pedido_criado", "termo_aceito", "cancelado");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{cancelado}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");

        Guid decidido = await DecididoPeloGestorAsync(diogo, perfil, unidade);
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{decidido}"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    // O pedido de Betina para Leandro: antes do aceite o gestor não decide, e o aceite grava a identidade e o nome dele.
    private async Task<Guid> AceitoAposARecusaDoGestorAsync(HttpClient diogo, Guid perfil, Guid unidade)
    {
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_beneficiario");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");

        JsonElement aceito = await CorpoAsync(await AceitarAsync(leandro, id), HttpStatusCode.OK);
        aceito.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        aceito.GetProperty("beneficiario").GetProperty("nome").GetString().Should().Be(Leandro.Nome);
        aceito.GetProperty("beneficiario").GetProperty("identidade").GetProperty("sub").GetString().Should().Be(Leandro.Sub.ToString());
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();

        return id;
    }

    private static void ConferirConcessaoDoPedido(Concessao concessao, Guid pedidoId, Guid unidade)
    {
        concessao.OrigemTipo.Should().Be(Autorizacao.Domain.Enums.TipoDeOrigem.Pedido);
        concessao.OrigemId.Should().Be(pedidoId);
        concessao.UnidadeId.Should().Be(unidade);
        concessao.ConcedidaPorSub.Should().Be(Diogo.Sub.ToString());
        concessao.BeneficiarioNome.Should().Be(Leandro.Nome);
        concessao.ConcedidaPorNome.Should().Be(Diogo.Nome);
    }

    private async Task ConferirTrilhasAsync(HttpClient diogo, Guid pedidoId, Guid concessaoId)
    {
        (await TiposDaTrilhaAsync(diogo, pedidoId)).Should().Equal("pedido_criado", "beneficiario_aceitou", "aprovado");
        JsonElement trilhaDoPedido = await LerAsync(diogo, $"{Rota}/{pedidoId}/trilha");
        trilhaDoPedido.EnumerateArray().Select(e => e.GetProperty("autor").GetProperty("nome").GetString()).Should()
            .Equal(Betina.Nome, Leandro.Nome, Diogo.Nome);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        JsonElement daConcessao = await LerAsync(administrador, $"/api/autorizacao/concessoes/{concessaoId}/trilha");
        daConcessao.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).Should()
            .Contain(["concedida", "pedido_criado", "beneficiario_aceitou", "aprovado"]);
    }

    private async Task<Guid> CriarComSugestaoAsync(Guid perfil, Guid unidade, DateTimeOffset sugerida)
    {
        using HttpClient betina = Cliente(Betina);
        using HttpResponseMessage criacao = await EnviarAsync(
            betina, HttpMethod.Post, Rota, new { perfilId = perfil, unidadeId = unidade, justificativa = Justificativa, validaAteSugerida = sugerida });
        JsonElement criado = await CorpoAsync(criacao, HttpStatusCode.Created);
        criado.GetProperty("validaAteSugerida").GetDateTimeOffset().Should().BeCloseTo(sugerida, TimeSpan.FromSeconds(1));
        criado.GetProperty("admitePrazo").GetBoolean().Should().BeTrue();

        return criado.GetProperty("id").GetGuid();
    }

    private async Task<Guid> AceitoPorLeandroAsync(Guid perfil, Guid unidade)
    {
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        (await AceitarAsync(leandro, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        return id;
    }

    private async Task ConferirPrimeiraEtapaAsync(HttpClient diogo, HttpClient marlene, Guid id, Guid perfil)
    {
        JsonElement gestor = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        gestor.GetProperty("situacao").GetString().Should().Be("aguardando_protecao_de_dados");
        gestor.GetProperty("etapaAtual").GetString().Should().Be("protecao_de_dados");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty("Leandro ainda não tem a concessão");
        (await ListarFilaAsync(marlene, "protecao_de_dados")).Should().Contain(id);
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(id);
    }

    private static async Task ConferirSemPermissaoAsync(HttpClient semPermissao, Guid id)
    {
        using HttpResponseMessage resposta = await EnviarAsync(semPermissao, HttpMethod.Post, $"{Rota}/{id}/aprovacoes");
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.auth.forbidden");
    }

    private static async Task ConferirRecusaNaTrilhaENasFilasAsync(HttpClient betina, HttpClient diogo, Guid id)
    {
        JsonElement trilha = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        JsonElement evento = trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "recusado");
        evento.GetProperty("detalhe").GetString().Should().Contain("função não exige acesso a laudos");
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(id);
        (await ListarFilaAsync(diogo, "todos")).Should().Contain(id);
        (await ListarFilaAsync(diogo, "todos", "recusado")).Should().Equal(id);
        (await EnviarAsync(betina, HttpMethod.Get, $"{Rota}?fila=todos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    private async Task CancelarSoPeloSolicitanteAsync(HttpClient betina, Guid id)
    {
        using HttpClient leandro = Cliente(Leandro);
        (await EnviarAsync(leandro, HttpMethod.Delete, $"{Rota}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SituacaoAsync(betina, id)).Should().Be("cancelado");
    }

    private async Task<Guid> DecididoPeloGestorAsync(HttpClient diogo, Guid perfil, Guid unidade)
    {
        using HttpClient betina = Cliente(Betina);
        using HttpClient isabella = Cliente(Isabella);
        Guid decidido = await CriarAsync(betina, perfil, unidade, Isabella);
        await AceitarAsync(isabella, decidido);
        await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{decidido}/aprovacoes");

        return decidido;
    }
}
