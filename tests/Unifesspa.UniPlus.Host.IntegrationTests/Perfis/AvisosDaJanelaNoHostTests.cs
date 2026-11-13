namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Os avisos da janela de migração entregues pelo outbox do host real, lidos na caixa de e-mail de teste, com o relógio
/// controlável: a decisão, o lote com recusas, o fim do prazo, o lembrete que não se repete e a advertência ao dono da
/// concessão retida.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AvisosDaJanelaNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private const string Migrada = "Seu acesso foi migrado";
    private const string Revogada = "Seu acesso a um perfil de acesso foi revogado";
    private const string Advertencia = "Seu acesso a um perfil de acesso será perdido";
    private const string ComRecusas = "Lote de migração de perfil de acesso enviado com recusas";
    private const string Encerrada = "Versão anterior de perfil de acesso encerrada";
    private const string Retida = "Concessão retida na migração";
    private const string Faltam = "Faltam ";
    private const string Devolvido = "Pedido de acesso devolvido";
    private const string Aguardando = "Migrações de perfil de acesso aguardam";

    public AvisosDaJanelaNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "O lote avisa quem foi recusado e os grupos, e a proteção de dados avisa quem migrou, sem nome nem e-mail de beneficiário")]
    public async Task Decisoes_AvisamOsInteressadosSemDadoPessoal()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();

        Caixa[] doLote = await AvisosDeAsync(() => EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "recusar")], confirmacaoDeExclusao: true));
        await EsvaziarCaixaAsync();
        Caixa[] daEtapa = await AvisosDeAsync(() => EnviarAsync(
            protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(cenario.Concessoes[Leandro], "migrar") } }));

        Para(doLote, Revogada).Should().Equal(Betina.Email);
        Para(daEtapa, Migrada).Should().Equal(Leandro.Email);
        Para(doLote, Aguardando).Should().Contain(Marlene.Email).And.Contain(Diogo.Email, "os gestores também são avisados").And.NotContain(Isabella.Email, "quem enviou o lote não é avisado");
        Mensagens(doLote, ComRecusas).Should().OnlyContain(m => SemDadoDosBeneficiarios(m));
    }

    [Fact(DisplayName = "O fim do prazo pela rotina avisa quem perdeu o acesso e os grupos, uma vez, mesmo com a rotina rodando de novo")]
    public async Task FimDoPrazo_AvisaUmaVez()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(73));

        Caixa[] caixa = await CaixaDepoisDeAsync(RodarARotinaDuasVezesAsync);

        Para(caixa, Revogada).Should().Equal(Leandro.Email);
        Para(caixa, Encerrada).Should().OnlyHaveUniqueItems();
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }

    [Fact(DisplayName = "O lembrete de 48 horas advertiu o beneficiário pendente uma vez só, ainda que a rotina rode de novo")]
    public async Task Lembrete_NaoRepete()
    {
        await AbrirEEntregarAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(25));

        Caixa[] caixa = await CaixaDepoisDeAsync(RodarARotinaDuasVezesAsync);

        Para(caixa, Advertencia).Should().Equal(Leandro.Email);
    }

    [Fact(DisplayName = "O dono da concessão retida recebe a advertência do prazo e os outros gestores, a mensagem de agir")]
    public async Task Retida_AvisaODonoEOsOutrosGestores()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Isabella, Leandro);
        using HttpClient isabella = await GestaoAsync();
        await EntregarComSucessoAsync(() => EnviarLoteAsync(isabella, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]));
        await EsvaziarCaixaAsync();
        Relogio.Avancar(TimeSpan.FromHours(49));

        Caixa[] caixa = await CaixaDepoisDeAsync(ProcessarJanelasAsync);

        Para(caixa, Advertencia).Should().Contain(Isabella.Email);
        caixa.Where(m => m.Para == Isabella.Email).Should().NotContain(m => m.Assunto.Contains(Retida, StringComparison.Ordinal) || m.Assunto.StartsWith(Faltam, StringComparison.Ordinal));
        caixa.Where(m => m.Para == Diogo.Email).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O encerramento manual avisa quem perdeu o acesso e os grupos, menos quem encerrou")]
    public async Task EncerramentoManual_AvisaOsInteressados()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        HttpResponseMessage? resposta = null;

        await EntregarAsync(async () => resposta = await EnviarAsync(
            gestao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/encerramento"), new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true }));
        Caixa[] caixa = await LerCaixaAsync();

        resposta!.IsSuccessStatusCode.Should().BeTrue();
        Para(caixa, Revogada).Should().Equal(Leandro.Email);
        Para(caixa, Encerrada).Should().NotContain(Isabella.Email);
    }

    [Fact(DisplayName = "A remoção de permissão dentro da janela, seguida do fim do prazo, avisa o encerramento da versão anterior uma vez só")]
    public async Task RemocaoNaJanela_SeguidaDoFim_AvisaOEncerramentoUmaVez()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro);
        using HttpClient gestor = await GestorAsync();

        Caixa[] caixa = await CaixaDepoisDeAsync(async () =>
        {
            using HttpResponseMessage remocao = await RemoverBrutoAsync(gestor, cenario.PerfilId, [Sensivel]);
            remocao.IsSuccessStatusCode.Should().BeTrue();
            Relogio.Avancar(TimeSpan.FromHours(73));
            await RodarARotinaDuasVezesAsync();
        });

        Tem(caixa, Encerrada).Should().BeTrue();
        Para(caixa, Encerrada).Should().OnlyHaveUniqueItems();
        Para(caixa, Revogada).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "O replay do lote com a mesma chave não envia aviso novo")]
    public async Task ReplayDoLote_NaoAvisaDeNovo()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        object[] decisoes = [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "recusar")];
        string chave = Guid.NewGuid().ToString();

        Caixa[] aposAPrimeira = await AvisosDeAsync(() => EnviarLoteAsync(gestao, cenario.PerfilId, decisoes, confirmacaoDeExclusao: true, chave: chave));
        Caixa[] aposOReplay = await AvisosDeAsync(() => EnviarLoteAsync(gestao, cenario.PerfilId, decisoes, confirmacaoDeExclusao: true, chave: chave));

        Tem(aposAPrimeira, Revogada).Should().BeTrue();
        aposOReplay.Select(m => (m.Para, m.Assunto)).Should().BeEquivalentTo(aposAPrimeira.Select(m => (m.Para, m.Assunto)));
    }

    [Fact(DisplayName = "O pedido ativo devolvido pela abertura da janela avisa o solicitante e o beneficiário uma vez, ainda que a rotina rode")]
    public async Task PedidoDevolvidoPelaJanela_AvisaUmaVez()
    {
        (Envio vigente, Guid pedido) = await PerfilComPedidoAtivoAsync();
        using HttpClient gestor = await GestorAsync();
        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel);

        Caixa[] caixa = await CaixaDepoisDeAsync(async () =>
        {
            await EnviarEVerificarAsync(gestor, nova);
            await ProcessarJanelasAsync();
        });

        pedido.Should().NotBeEmpty();
        Para(caixa, Devolvido).Should().Contain(Betina.Email).And.OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "A revogação manual depois do corte e antes da rotina não vira aviso de revogação pelo fim")]
    public async Task RevogacaoManualDepoisDoCorte_NaoViraAvisoDoFim()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro, Betina);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        Relogio.Avancar(TimeSpan.FromHours(73));

        Caixa[] caixa = await CaixaDepoisDeAsync(async () =>
        {
            using HttpResponseMessage revogacao = await RevogarAsync(administrador, cenario.Concessoes[Leandro], MotivoDaRevogacao, Leandro.Email);
            revogacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await revogacao.Content.ReadAsStringAsync());
            await ProcessarJanelasAsync();
        });

        Tem(caixa, Encerrada).Should().BeTrue();
        Para(caixa, Revogada).Should().NotContain(Leandro.Email);
    }

    private static bool Tem(Caixa[] caixa, string assunto) => Mensagens(caixa, assunto).Length > 0;

    private static string[] Para(Caixa[] caixa, string assunto) => [.. Mensagens(caixa, assunto).Select(m => m.Para)];

    private static Caixa[] Mensagens(Caixa[] caixa, string assunto) =>
        [.. caixa.Where(m => m.Assunto.Contains(assunto, StringComparison.Ordinal))];

    // O aviso aos grupos não leva o nome nem o e-mail dos beneficiários do lote.
    private static bool SemDadoDosBeneficiarios(Caixa mensagem) =>
        new[] { Leandro.Email, Betina.Email, Leandro.Nome, Betina.Nome }.All(dado => !mensagem.Texto.Contains(dado, StringComparison.Ordinal));

    /// <summary>Entrega o que a chamada publica no outbox e confere que ela foi aceita.</summary>
    private async Task EntregarComSucessoAsync(Func<Task<HttpResponseMessage>> chamada) =>
        await EntregarAsync(async () =>
        {
            using HttpResponseMessage resposta = await chamada();
            resposta.IsSuccessStatusCode.Should().BeTrue(await resposta.Content.ReadAsStringAsync());
        });

    /// <summary>Os avisos que a chamada aceita deixou na caixa de e-mail.</summary>
    private async Task<Caixa[]> AvisosDeAsync(Func<Task<HttpResponseMessage>> chamada)
    {
        await EntregarComSucessoAsync(chamada);

        return await LerCaixaAsync();
    }

    /// <summary>A caixa de e-mail depois de entregar o que a ação publicou no outbox.</summary>
    private async Task<Caixa[]> CaixaDepoisDeAsync(Func<Task> acao)
    {
        await EntregarAsync(acao);

        return await LerCaixaAsync();
    }

    // A rotina rodando de novo não repete o que já avisou.
    private async Task RodarARotinaDuasVezesAsync()
    {
        await ProcessarJanelasAsync();
        await ProcessarJanelasAsync();
    }

    // O perfil vigente com um pedido ativo de Betina, cujos avisos já foram entregues; a caixa fica vazia.
    private async Task<(Envio Vigente, Guid Pedido)> PerfilComPedidoAtivoAsync()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        using HttpClient betina = Cliente(Betina);
        Guid pedido = default;
        await EntregarAsync(async () => pedido = await CriarAsync(betina, vigente.PerfilId, await SemearUnidadeAsync(), null));
        await EsvaziarCaixaAsync();

        return (vigente, pedido);
    }

    private HttpClient Mailpit() => new() { BaseAddress = Fixture.ApiDoMailpit };

    private async Task EsvaziarCaixaAsync()
    {
        using HttpClient mailpit = Mailpit();
        (await mailpit.DeleteAsync(new Uri("/api/v1/messages", UriKind.Relative))).EnsureSuccessStatusCode();
    }

    private async Task<JanelaAberta> AbrirEEntregarAsync(params Persona[] beneficiarios)
    {
        JanelaAberta? janela = null;
        await EntregarAsync(async () => janela = await AbrirJanelaAsync(beneficiarios));
        await EsvaziarCaixaAsync();

        return janela!;
    }

    private async Task<Caixa[]> LerCaixaAsync()
    {
        using HttpClient mailpit = Mailpit();
        JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages?limit=500");
        List<Caixa> caixa = [];
        foreach (JsonElement resumo in lista.GetProperty("messages").EnumerateArray())
        {
            JsonElement completa = await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{resumo.GetProperty("ID").GetString()}");
            string texto = completa.GetProperty("Text").GetString() ?? string.Empty;
            foreach (JsonElement para in resumo.GetProperty("To").EnumerateArray())
            {
                caixa.Add(new Caixa(para.GetProperty("Address").GetString()!, resumo.GetProperty("Subject").GetString()!, texto));
            }
        }

        return [.. caixa];
    }

    private sealed record Caixa(string Para, string Assunto, string Texto);
}
