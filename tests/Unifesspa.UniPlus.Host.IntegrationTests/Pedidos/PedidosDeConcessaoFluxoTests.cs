namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O pedido de concessão pelo host real: Postgres, RabbitMQ, MinIO e Mailpit em contêineres. Cobre os cenários da
/// issue (perfil interno vale com o gestor, sensível passa pelas duas etapas, pessoas distintas, recusa, cancelamento),
/// o aceite do beneficiário, as filas e a visibilidade.
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
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_beneficiario");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");

        JsonElement aceito = await CorpoAsync(await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite"), HttpStatusCode.OK);
        aceito.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        aceito.GetProperty("beneficiario").GetProperty("nome").GetString().Should().Be(Leandro.Nome);
        aceito.GetProperty("beneficiario").GetProperty("identidade").GetProperty("sub").GetString().Should().Be(Leandro.Sub.ToString());
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();

        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        aprovado.GetProperty("situacao").GetString().Should().Be("aprovado");
        Concessao concessao = (await ConcessoesAsync(Leandro, perfil)).Single();
        concessao.OrigemTipo.Should().Be(Autorizacao.Domain.Enums.TipoDeOrigem.Pedido);
        concessao.OrigemId.Should().Be(id);
        concessao.UnidadeId.Should().Be(unidade);
        concessao.ConcedidaPorSub.Should().Be(Diogo.Sub.ToString());
        concessao.BeneficiarioNome.Should().Be(Leandro.Nome);
        concessao.ConcedidaPorNome.Should().Be(Diogo.Nome);
        aprovado.GetProperty("decisoes").EnumerateArray().Single().GetProperty("decisor").GetProperty("nome").GetString().Should().Be(Diogo.Nome);
        aprovado.GetProperty("solicitante").GetProperty("nome").GetString().Should().Be(Betina.Nome);
        aprovado.GetProperty("concessaoId").GetGuid().Should().Be(concessao.Id);

        string[] tipos = await TiposDaTrilhaAsync(diogo, id);
        tipos.Should().Equal("pedido_criado", "beneficiario_aceitou", "aprovado");
        JsonElement trilhaDoPedido = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        trilhaDoPedido.EnumerateArray().Select(e => e.GetProperty("autor").GetProperty("nome").GetString()).Should()
            .Equal(Betina.Nome, Leandro.Nome, Diogo.Nome);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        JsonElement daConcessao = await LerAsync(administrador, $"/api/autorizacao/concessoes/{concessao.Id}/trilha");
        daConcessao.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).Should()
            .Contain(["concedida", "pedido_criado", "beneficiario_aceitou", "aprovado"]);
        LerLog().Should().NotContain(Leandro.Email).And.NotContain(Betina.Email);
    }

    [Fact(DisplayName = "Pedido de perfil sensível passa pelas duas aprovações, e a concessão só nasce na segunda")]
    public async Task PerfilSensivel_PassaPelasDuasEtapas()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite")).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement gestor = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        gestor.GetProperty("situacao").GetString().Should().Be("aguardando_encarregada");
        gestor.GetProperty("etapaAtual").GetString().Should().Be("encarregada");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty("Leandro ainda não tem a concessão");
        (await ListarFilaAsync(marlene, "encarregada")).Should().Contain(id);
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(id);

        JsonElement encarregada = await CorpoAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        encarregada.GetProperty("situacao").GetString().Should().Be("aprovado");
        (await ConcessoesAsync(Leandro, perfil)).Should().ContainSingle().Which.ConcedidaPorSub.Should().Be(Marlene.Sub.ToString());
        JsonElement[] decisoes = [.. encarregada.GetProperty("decisoes").EnumerateArray()];
        decisoes.Select(d => d.GetProperty("etapa").GetString()).Should().Equal("gestor", "encarregada");
        decisoes[0].GetProperty("sensibilidadeAvaliada").GetString().Should().Be("sensivel");
        (await ListarFilaAsync(marlene, "encarregada")).Should().NotContain(id);
    }

    [Fact(DisplayName = "A encarregada não decide antes do gestor, o gestor não decide a etapa dela, e sem permissão a resposta é 403")]
    public async Task EtapaDaPermissao_ConferidaPelaSituacao()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "etapa_invalida");
        await ConferirRecusaAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "Cedo demais." }), HttpStatusCode.UnprocessableEntity, "etapa_invalida");
        using HttpResponseMessage semPermissao = await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/aprovacoes");
        semPermissao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await semPermissao.Content.ReadAsStringAsync()).Should().Contain("uniplus.auth.forbidden");
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");

        await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "etapa_invalida");
    }

    [Fact(DisplayName = "Token sem o que identifica o solicitante responde 401 antes de qualquer decisão de acesso")]
    public async Task IdentidadeIncompleta_Devolve401()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
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
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient solicitante = parte == "solicitante" ? await ClienteComPermissoesAsync(Diogo, AprovarFuncional) : Cliente(Betina);
        using HttpClient beneficiario = parte == "solicitante" ? Cliente(Leandro) : await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Persona quemRecebe = parte == "solicitante" ? Leandro : Diogo;
        Guid id = await CriarAsync(solicitante, perfil, unidade, quemRecebe);
        (await EnviarAsync(beneficiario, HttpMethod.Post, $"{Rota}/{id}/aceite")).StatusCode.Should().Be(HttpStatusCode.OK);
        using HttpClient aprovador = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        await ConferirRecusaAsync(await EnviarAsync(aprovador, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "decisor_e_parte");
        await ConferirRecusaAsync(await EnviarAsync(aprovador, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "Sou parte." }), HttpStatusCode.UnprocessableEntity, "decisor_e_parte");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A mesma pessoa não aprova as duas etapas, mesmo com as duas permissões")]
    public async Task MesmaPessoaNasDuasEtapas_Recusa()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional, AprovarProtecaoDeDados);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirRecusaAsync(await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");
        await ConferirRecusaAsync(await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "De novo." }), HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_encarregada");
    }

    [Fact(DisplayName = "Recusa encerra o pedido com o motivo, sem concessão, e a trilha a registra; sem motivo é recusada")]
    public async Task Recusa_EncerraSemConcessao()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(betina, perfil, unidade, null);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "  " }), HttpStatusCode.UnprocessableEntity, "motivo_invalido");
        JsonElement recusado = await CorpoAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = "função não exige acesso a laudos" }), HttpStatusCode.OK);

        recusado.GetProperty("situacao").GetString().Should().Be("recusado");
        recusado.GetProperty("concessaoId").ValueKind.Should().Be(JsonValueKind.Null);
        (await ConcessoesAsync(Betina, perfil)).Should().BeEmpty();
        JsonElement trilha = await LerAsync(diogo, $"{Rota}/{id}/trilha");
        JsonElement evento = trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "recusado");
        evento.GetProperty("detalhe").GetString().Should().Contain("função não exige acesso a laudos");
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(id);
        (await ListarFilaAsync(diogo, "todos")).Should().Contain(id);
        (await ListarFilaAsync(diogo, "todos", "recusado")).Should().Equal(id);
        (await EnviarAsync(betina, HttpMethod.Get, $"{Rota}?fila=todos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "O solicitante cancela antes da primeira decisão e o pedido sai das filas; outra pessoa recebe 404, e depois da decisão não cancela")]
    public async Task Cancelamento_SoDoSolicitanteAntesDaDecisao()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid cancelado = await CriarAsync(betina, perfil, unidade, null);
        (await ListarFilaAsync(diogo, "gestor")).Should().Contain(cancelado);

        (await EnviarAsync(leandro, HttpMethod.Delete, $"{Rota}/{cancelado}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{cancelado}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SituacaoAsync(betina, cancelado)).Should().Be("cancelado");
        (await ListarFilaAsync(diogo, "gestor")).Should().NotContain(cancelado);
        (await TiposDaTrilhaAsync(betina, cancelado)).Should().Equal("pedido_criado", "cancelado");
        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{cancelado}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");

        Guid decidido = await CriarAsync(betina, perfil, unidade, Isabella);
        using HttpClient isabella = Cliente(Isabella);
        await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{decidido}/aceite");
        await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{decidido}/aprovacoes");
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{decidido}"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "Só o beneficiário, com o e-mail institucional verificado, aceita o pedido")]
    public async Task Aceite_SoDoBeneficiario()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        using HttpClient outraPessoa = Cliente(Diogo);
        using HttpClient semVerificar = Cliente(Leandro, null, "false");

        await ConferirRecusaAsync(await EnviarAsync(outraPessoa, HttpMethod.Post, $"{Rota}/{id}/aceite"), HttpStatusCode.Forbidden, "nao_e_o_beneficiario");
        await ConferirRecusaAsync(await EnviarAsync(semVerificar, HttpMethod.Post, $"{Rota}/{id}/aceite"), HttpStatusCode.Forbidden, "nao_e_o_beneficiario");
        await ConferirRecusaAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/aceite"), HttpStatusCode.Forbidden, "nao_e_o_beneficiario");
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_beneficiario");

        using HttpClient leandro = Cliente(Leandro);
        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite")).StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirRecusaAsync(await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite"), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "Pedido para si mesmo nasce aguardando o gestor, já com a identidade do token")]
    public async Task ParaSiMesmo_NasceAguardandoOGestor()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient leandro = Cliente(Leandro);

        using HttpResponseMessage resposta = await CriarBrutoAsync(leandro, perfil, unidade, null);

        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.Created);
        corpo.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        corpo.GetProperty("beneficiario").GetProperty("identidade").GetProperty("sub").GetString().Should().Be(Leandro.Sub.ToString());
        corpo.GetProperty("solicitante").GetProperty("sub").GetString().Should().Be(Leandro.Sub.ToString());
    }

    [Fact(DisplayName = "Pedido igual ao ativo é recusado como duplicado, e volta a valer depois do cancelamento")]
    public async Task PedidoAtivoIgual_EDuplicado()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        Guid primeiro = await CriarAsync(betina, perfil, unidade, Leandro);

        await ConferirRecusaAsync(await CriarBrutoAsync(betina, perfil, unidade, Leandro.Email), HttpStatusCode.Conflict, "pedido_duplicado");

        (await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{primeiro}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CriarBrutoAsync(betina, perfil, unidade, Leandro.Email)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "A criação recusa perfil inativo, unidade inexistente, escopo incoerente, justificativa curta e beneficiário fora do domínio")]
    public async Task Criar_RecusasDeCampo()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid inativo = await SemearPerfilInternoAsync();
        await ExecutarSqlAsync("UPDATE autorizacao.perfis SET ativo = false WHERE id = {0}", inativo);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);

        await ConferirRecusaAsync(await CriarBrutoAsync(betina, inativo, unidade, Leandro.Email), HttpStatusCode.UnprocessableEntity, "perfil_inativo");
        await ConferirRecusaAsync(await CriarBrutoAsync(betina, perfil, Guid.NewGuid(), Leandro.Email), HttpStatusCode.UnprocessableEntity, "unidade_inexistente");
        await ConferirRecusaAsync(await CriarBrutoAsync(betina, perfil, null, Leandro.Email), HttpStatusCode.UnprocessableEntity, "escopo_invalido");
        await ConferirRecusaAsync(await CriarBrutoAsync(betina, perfil, unidade, Leandro.Email, "curta"), HttpStatusCode.UnprocessableEntity, "justificativa_invalida");
        await ConferirRecusaAsync(await CriarBrutoAsync(betina, perfil, unidade, "leandro@gmail.com"), HttpStatusCode.UnprocessableEntity, "beneficiario_invalido");
        using HttpResponseMessage semPerfil = await CriarBrutoAsync(betina, Guid.NewGuid(), unidade, Leandro.Email);
        semPerfil.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Quem tem só e-mail externo não pede acesso para outra pessoa, mas o institucional pede")]
    public async Task Criar_ParaOutraPessoa_ExigeSolicitanteInstitucional()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient externa = Cliente(Betina, "betina@gmail.com");
        using HttpClient semVerificar = Cliente(Betina, null, "false");
        using HttpClient institucional = Cliente(Betina);

        await ConferirRecusaAsync(await CriarBrutoAsync(externa, perfil, unidade, Leandro.Email), HttpStatusCode.Forbidden, "solicitante_nao_institucional");
        await ConferirRecusaAsync(await CriarBrutoAsync(semVerificar, perfil, unidade, Leandro.Email), HttpStatusCode.Forbidden, "solicitante_nao_institucional");
        (await CriarBrutoAsync(institucional, perfil, unidade, Leandro.Email)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "A lista de perfis para pedir é do institucional ou de quem mantém perfis, só ativos e sem a composição administrativa")]
    public async Task PerfisDisponiveis_ExigemEmailInstitucionalOuPermissao()
    {
        await ResetarAsync();
        Guid ativo = await SemearPerfilInternoAsync();
        Guid inativo = await SemearPerfilInternoAsync();
        await ExecutarSqlAsync("UPDATE autorizacao.perfis SET ativo = false WHERE id = {0}", inativo);
        const string Rota = "/api/autorizacao/perfis-para-pedido";
        using HttpClient externa = Cliente(Betina, "betina@gmail.com");
        using HttpClient institucional = Cliente(Betina);
        using HttpClient gestorExterno = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient gestorComEmailExterno = Cliente(Diogo, "diogo@gmail.com");

        await ConferirRecusaAsync(await externa.GetAsync(new Uri(Rota, UriKind.Relative)), HttpStatusCode.Forbidden, "solicitante_nao_institucional");

        foreach (HttpClient cliente in new[] { institucional, gestorComEmailExterno })
        {
            JsonElement lista = await LerAsync(cliente, Rota + "?limit=100");
            JsonElement[] itens = [.. lista.EnumerateArray()];
            itens.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(ativo).And.NotContain(inativo);
            JsonElement item = itens.Single(i => i.GetProperty("id").GetGuid() == ativo);
            item.EnumerateObject().Select(p => p.Name).Should()
                .BeEquivalentTo("id", "nome", "descricao", "escopo", "exigeEncarregada", "governanca");
        }
    }

    [Fact(DisplayName = "As filas listam só a etapa de cada aprovador e exigem a permissão dela; a lista de quem pergunta mostra os próprios pedidos")]
    public async Task Filas_ExigemAPermissaoDaEtapa()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid esperando = await CriarAsync(betina, perfil, unidade, Leandro);
        Guid noGestor = await CriarAsync(leandro, perfil, await SemearUnidadeAsync(), null);

        (await ListarFilaAsync(diogo, "gestor")).Should().Contain(noGestor).And.NotContain(esperando);
        (await ListarFilaAsync(marlene, "encarregada")).Should().BeEmpty();
        (await ListarFilaAsync(diogo, "todos")).Should().Equal([noGestor, esperando], "o histórico lista do pedido mais recente ao mais antigo");
        (await EnviarAsync(leandro, HttpMethod.Get, $"{Rota}?fila=gestor")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await EnviarAsync(diogo, HttpMethod.Get, $"{Rota}?fila=encarregada")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpResponseMessage filaInvalida = await EnviarAsync(diogo, HttpMethod.Get, $"{Rota}?fila=outra");
        filaInvalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await filaInvalida.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.pedido.fila_invalida");

        (await ListarFilaAsync(betina, "meus")).Should().Equal(esperando);
        (await ListarFilaAsync(leandro, "meus")).Should().BeEquivalentTo([esperando, noGestor]);
        (await ListarFilaAsync(leandro, "meus", "aguardando_gestor")).Should().Equal(noGestor);
    }

    [Fact(DisplayName = "Depois do aceite a lista e o detalhe seguem a identidade, e não mais o e-mail: quem herda o e-mail não vê o pedido")]
    public async Task Meus_DepoisDoAceite_SegueAIdentidade()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        using HttpClient comOEmailDele = Cliente(Diogo, Leandro.Email);

        (await ListarFilaAsync(comOEmailDele, "meus")).Should().Contain(id, "antes do aceite o e-mail verificado identifica o beneficiário");
        await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite");

        (await ListarFilaAsync(comOEmailDele, "meus")).Should().NotContain(id);
        (await comOEmailDele.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpClient leandroComOutroEmail = Cliente(Leandro, "leandro.novo@uniplus-test.local");
        (await ListarFilaAsync(leandroComOutroEmail, "meus")).Should().Contain(id);
        (await leandroComOutroEmail.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O pedido só é visto pelas partes, por quem decide uma etapa e por quem administra concessões; os demais recebem 404")]
    public async Task Detalhe_VisivelSoAQuemTemPapel()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);

        foreach (HttpClient quemVe in new[] { Cliente(Betina), Cliente(Leandro), await ClienteComPermissoesAsync(Diogo, AprovarFuncional), await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados), await ClienteComPermissoesAsync(Isabella, Administrar) })
        {
            using (quemVe)
            {
                (await quemVe.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
                (await quemVe.GetAsync(new Uri($"{Rota}/{id}/trilha", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        using HttpClient estranho = Cliente(new Persona("paulo.conceicao", "Paulo Conceição", "paulo-daconceicao93@uniplus-test.local"));
        using HttpClient comEmailNaoVerificado = Cliente(Leandro, null, "false");
        foreach (HttpClient semAcesso in new[] { estranho, comEmailNaoVerificado })
        {
            (await semAcesso.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await semAcesso.GetAsync(new Uri($"{Rota}/{id}/trilha", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        (await estranho.GetAsync(new Uri($"{Rota}/{Guid.NewGuid()}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
