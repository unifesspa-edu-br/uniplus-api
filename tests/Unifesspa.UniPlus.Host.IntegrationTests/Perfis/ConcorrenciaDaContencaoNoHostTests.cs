namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A contenção segue a ordem de bloqueios das demais operações sobre o perfil: o teste segura o bloqueio, enfileira as
/// chamadas na ordem que quer e solta. A chave de idempotência repete a resposta sem repetir o efeito, e o conflito de
/// concorrência de uma operação com chave vira recusa, sem aviso (ADR-0119).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConcorrenciaDaContencaoNoHostTests : ContencaoDoPerfilNoHostTestBase, IClassFixture<HostComConflitoNaGravacao>
{
    private readonly HostComConflitoNaGravacao _hostDaClasse;

    public ConcorrenciaDaContencaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostComConflitoNaGravacao hostDaClasse)
        : base(fixture)
    {
        _hostDaClasse = hostDaClasse;
    }

    [Fact(DisplayName = "Duas suspensões em fila: a primeira suspende e a segunda encontra o perfil suspenso")]
    public async Task DuasSuspensoes_ASegundaEncontraSuspenso()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);

        HttpResponseMessage[] respostas = await EmFilaSobOBloqueioAsync(
            perfilId, () => SuspenderAsync(diogo, perfilId), () => SuspenderAsync(isabella, perfilId));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.UnprocessableEntity, "ja_suspenso");
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao");
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "As duas etapas da retomada em fila fecham a suspensão uma única vez")]
    public async Task DuasEtapas_FechamUmaVez()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Guid perfilId = await PerfilProtegidoComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);

        HttpResponseMessage[] respostas = await EmFilaSobOBloqueioAsync(
            perfilId, () => RetomarAsync(isabella, perfilId, EtapaGestao), () => RetomarAsync(marlene, perfilId, EtapaProtecao));

        respostas.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao", "decisao_da_retomada", "decisao_da_retomada", "retomada");
        (await PermissoesDaConcessaoAsync(perfilId)).Should().NotBeEmpty();
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "Duas decisões da mesma etapa em fila: a primeira vale e a segunda recebe a recusa de etapa já decidida")]
    public async Task MesmaEtapa_ASegundaEhRecusada()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Guid perfilId = await PerfilProtegidoComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);

        HttpResponseMessage[] respostas = await EmFilaSobOBloqueioAsync(
            perfilId, () => RetomarAsync(isabella, perfilId, EtapaGestao), () => RetomarAsync(betina, perfilId, EtapaGestao));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.Conflict, "etapa_da_retomada_ja_decidida");
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "Duas decisões da gestão num perfil não protegido em fila: a primeira fecha a suspensão e a segunda recebe a recusa de não suspenso")]
    public async Task DuasDecisoesDaGestao_ASegundaEncontraNaoSuspenso()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);

        HttpResponseMessage[] respostas = await EmFilaSobOBloqueioAsync(
            perfilId, () => RetomarAsync(isabella, perfilId, EtapaGestao), () => RetomarAsync(betina, perfilId, EtapaGestao));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.UnprocessableEntity, "nao_suspenso");
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao", "decisao_da_retomada", "retomada");
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "A concessão aprovada depois da suspensão nasce sem permissões")]
    public async Task ConcessaoEsperaASuspensao_NasceSemPermissoes()
    {
        using var cenario = new CenarioDeSuspensaoEAprovacao(this);
        await cenario.ExecutarAsync();
        cenario.Respostas[0].StatusCode.Should().Be(HttpStatusCode.NoContent);
        cenario.Respostas[1].StatusCode.Should().Be(HttpStatusCode.OK, await cenario.Respostas[1].Content.ReadAsStringAsync());
        (await ConcessoesAsync(Betina, cenario.PerfilId)).Should().ContainSingle();
        (await PermissoesDaConcessaoAsync(cenario.PerfilId, Betina)).Should().BeEmpty();
    }

    private sealed class CenarioDeSuspensaoEAprovacao(ConcorrenciaDaContencaoNoHostTests teste) : IDisposable
    {
        public Guid PerfilId { get; private set; }
        public HttpResponseMessage[] Respostas { get; private set; } = [];

        public async Task ExecutarAsync()
        {
            await teste.PrepararAsync();
            PerfilId = await teste.SemearPerfilAsync("Acompanhamento de números", Consultar);
            using HttpClient diogo = await teste.GestorAsync();
            Guid unidade = await teste.SemearUnidadeAsync();
            using HttpClient betina = teste.Cliente(Betina);
            Guid pedido = await teste.CriarAsync(betina, PerfilId, unidade, null);
            Respostas = await teste.EmFilaSobOBloqueioAsync(
                PerfilId, () => SuspenderAsync(diogo, PerfilId),
                () => EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes"));
        }

        public void Dispose() => DescartarTodas(Respostas);
    }

    [Fact(DisplayName = "A mesma chave de idempotência repete a resposta e a suspensão acontece uma vez")]
    public async Task MesmaChave_SuspendeUmaVez()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage primeira = await SuspenderAsync(diogo, perfilId, chave: chave);
        using HttpResponseMessage repetida = await SuspenderAsync(diogo, perfilId, chave: chave);

        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);
        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao");
    }

    [Fact(DisplayName = "O conflito de concorrência na retomada, com chave de idempotência, vira 409 e nada fica gravado")]
    public async Task ConflitoNaRetomada_Responde409SemGravar()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);
        _hostDaClasse.Conflito.Armar("UPDATE autorizacao.suspensoes_do_perfil SET updated_at = now() WHERE perfil_id = {0}", perfilId);

        using HttpResponseMessage resposta = await RetomarAsync(isabella, perfilId, EtapaGestao);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, corpo);
        corpo.Should().Contain("uniplus.concorrencia.conflito");
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao");
    }

    private protected override HttpClient NovoCliente() => _hostDaClasse.Obter(Fixture).CreateClient();
}
