namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O pedido de concessão pelo host real, na criação e na leitura: o aceite só do beneficiário, o pedido para si mesmo, o
/// duplicado, as recusas de campo, os perfis que se podem pedir, as filas e a visibilidade.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PedidosDeConcessaoCriacaoEVisibilidadeTests : PedidosDeConcessaoTestBase
{
    private const string RotaDosPerfis = "/api/autorizacao/perfis-para-pedido";

    public PedidosDeConcessaoCriacaoEVisibilidadeTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Só o beneficiário, com o e-mail institucional verificado, aceita o pedido")]
    public async Task Aceite_SoDoBeneficiario()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        using HttpClient outraPessoa = Cliente(Diogo);
        using HttpClient semVerificar = Cliente(Leandro, null, "false");

        await ConferirRecusaAsync(await AceitarAsync(outraPessoa, id), HttpStatusCode.Forbidden, "nao_e_o_beneficiario");
        await ConferirRecusaAsync(await AceitarAsync(semVerificar, id), HttpStatusCode.Forbidden, "nao_e_o_beneficiario");
        await ConferirRecusaAsync(await AceitarAsync(betina, id), HttpStatusCode.Forbidden, "nao_e_o_beneficiario");
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_beneficiario");

        using HttpClient leandro = Cliente(Leandro);
        (await AceitarAsync(leandro, id)).StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirRecusaAsync(await AceitarAsync(leandro, id), HttpStatusCode.UnprocessableEntity, "situacao_invalida");
    }

    [Fact(DisplayName = "Pedido para si mesmo nasce aguardando o gestor, já com a identidade do token")]
    public async Task ParaSiMesmo_NasceAguardandoOGestor()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
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
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        Guid primeiro = await CriarAsync(betina, perfil, unidade, Leandro);

        await ConferirRecusaAsync(await CriarBrutoAsync(betina, perfil, unidade, Leandro.Email), HttpStatusCode.Conflict, "pedido_duplicado");

        (await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{primeiro}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CriarBrutoAsync(betina, perfil, unidade, Leandro.Email)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "A criação recusa perfil inativo, unidade inexistente, escopo incoerente, justificativa curta e beneficiário fora do domínio")]
    public async Task Criar_RecusasDeCampo()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        Guid inativo = await PerfilInativoAsync();
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
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
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
        Guid inativo = await PerfilInativoAsync();
        using HttpClient externa = Cliente(Betina, "betina@gmail.com");
        using HttpClient institucional = Cliente(Betina);
        using HttpClient gestorComEmailExterno = Cliente(Diogo, "diogo@gmail.com");
        using HttpClient gestorExterno = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        await ConferirRecusaAsync(await externa.GetAsync(new Uri(RotaDosPerfis, UriKind.Relative)), HttpStatusCode.Forbidden, "solicitante_nao_institucional");
        await ConferirPerfisParaPedirAsync(institucional, ativo, inativo);
        await ConferirPerfisParaPedirAsync(gestorComEmailExterno, ativo, inativo);
    }

    [Fact(DisplayName = "As filas listam só a etapa de cada aprovador e exigem a permissão dela; a lista de quem pergunta mostra os próprios pedidos")]
    public async Task Filas_ExigemAPermissaoDaEtapa()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid esperando = await CriarAsync(betina, perfil, unidade, Leandro);
        Guid noGestor = await CriarAsync(leandro, perfil, await SemearUnidadeAsync(), null);

        await ConferirFilasDosAprovadoresAsync(diogo, leandro, esperando, noGestor);

        (await ListarFilaAsync(betina, "meus")).Should().Equal(esperando);
        (await ListarFilaAsync(leandro, "meus")).Should().BeEquivalentTo([esperando, noGestor]);
        (await ListarFilaAsync(leandro, "meus", "aguardando_gestor")).Should().Equal(noGestor);
    }

    [Fact(DisplayName = "Depois do aceite a lista e o detalhe seguem a identidade, e não mais o e-mail: quem herda o e-mail não vê o pedido")]
    public async Task Meus_DepoisDoAceite_SegueAIdentidade()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        using HttpClient comOEmailDele = Cliente(Diogo, Leandro.Email);

        (await ListarFilaAsync(comOEmailDele, "meus")).Should().Contain(id, "antes do aceite o e-mail verificado identifica o beneficiário");
        await AceitarAsync(leandro, id);

        (await ListarFilaAsync(comOEmailDele, "meus")).Should().NotContain(id);
        (await comOEmailDele.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpClient leandroComOutroEmail = Cliente(Leandro, "leandro.novo@uniplus-test.local");
        (await ListarFilaAsync(leandroComOutroEmail, "meus")).Should().Contain(id);
        (await leandroComOutroEmail.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O pedido só é visto pelas partes, por quem decide uma etapa e por quem administra concessões; os demais recebem 404")]
    public async Task Detalhe_VisivelSoAQuemTemPapel()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);

        await ConferirQuemVeAsync(id);

        using HttpClient estranho = Cliente(new Persona("paulo.conceicao", "Paulo Conceição", "paulo-daconceicao93@uniplus-test.local"));
        using HttpClient comEmailNaoVerificado = Cliente(Leandro, null, "false");
        await ConferirLeituraAsync(estranho, id, HttpStatusCode.NotFound);
        await ConferirLeituraAsync(comEmailNaoVerificado, id, HttpStatusCode.NotFound);
        (await estranho.GetAsync(new Uri($"{Rota}/{Guid.NewGuid()}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> PerfilInativoAsync()
    {
        Guid inativo = await SemearPerfilInternoAsync();
        await ExecutarSqlAsync("UPDATE autorizacao.perfis SET ativo = false WHERE id = {0}", inativo);

        return inativo;
    }

    private static async Task ConferirPerfisParaPedirAsync(HttpClient cliente, Guid ativo, Guid inativo)
    {
        JsonElement[] itens = [.. (await LerAsync(cliente, RotaDosPerfis + "?limit=100")).EnumerateArray()];
        itens.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(ativo).And.NotContain(inativo);
        JsonElement item = itens.Single(i => i.GetProperty("id").GetGuid() == ativo);
        item.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("id", "nome", "descricao", "escopo", "exigeAprovacaoDeProtecaoDeDados", "governanca");
    }

    private async Task ConferirFilasDosAprovadoresAsync(HttpClient diogo, HttpClient leandro, Guid esperando, Guid noGestor)
    {
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        (await ListarFilaAsync(diogo, "gestor")).Should().Contain(noGestor).And.NotContain(esperando);
        (await ListarFilaAsync(marlene, "protecao_de_dados")).Should().BeEmpty();
        (await ListarFilaAsync(diogo, "todos")).Should().Equal([noGestor, esperando], "o histórico lista do pedido mais recente ao mais antigo");
        (await EnviarAsync(leandro, HttpMethod.Get, $"{Rota}?fila=gestor")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await EnviarAsync(diogo, HttpMethod.Get, $"{Rota}?fila=protecao_de_dados")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpResponseMessage filaInvalida = await EnviarAsync(diogo, HttpMethod.Get, $"{Rota}?fila=outra");
        filaInvalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await filaInvalida.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.pedido.fila_invalida");
    }

    private async Task ConferirQuemVeAsync(Guid id)
    {
        foreach (HttpClient quemVe in new[] { Cliente(Betina), Cliente(Leandro), await ClienteComPermissoesAsync(Diogo, AprovarFuncional), await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados), await ClienteComPermissoesAsync(Isabella, Administrar) })
        {
            using (quemVe)
            {
                await ConferirLeituraAsync(quemVe, id, HttpStatusCode.OK);
            }
        }
    }

    private static async Task ConferirLeituraAsync(HttpClient cliente, Guid id, HttpStatusCode esperado)
    {
        (await cliente.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative))).StatusCode.Should().Be(esperado);
        (await cliente.GetAsync(new Uri($"{Rota}/{id}/trilha", UriKind.Relative))).StatusCode.Should().Be(esperado);
    }
}
