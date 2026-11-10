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
        HttpResponseMessage? lote = null;
        HttpResponseMessage? etapa = null;

        await EntregarAsync(async () => lote = await EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "recusar")], confirmacaoDeExclusao: true));
        Caixa[] doLote = await LerCaixaAsync();
        await EsvaziarCaixaAsync();
        await EntregarAsync(async () => etapa = await EnviarAsync(
            protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(cenario.Concessoes[Leandro], "migrar") } }));
        Caixa[] daEtapa = await LerCaixaAsync();

        lote!.IsSuccessStatusCode.Should().BeTrue();
        etapa!.IsSuccessStatusCode.Should().BeTrue();
        Para(doLote, Revogada).Should().Equal(Betina.Email);
        Para(daEtapa, Migrada).Should().Equal(Leandro.Email);
        Para(doLote, Aguardando).Should().Contain(Marlene.Email).And.NotContain(Isabella.Email);
        doLote.Where(m => m.Assunto.Contains(ComRecusas, StringComparison.Ordinal)).Should().OnlyContain(
            m => !m.Texto.Contains(Leandro.Email, StringComparison.Ordinal) && !m.Texto.Contains(Betina.Email, StringComparison.Ordinal)
                && !m.Texto.Contains(Leandro.Nome, StringComparison.Ordinal) && !m.Texto.Contains(Betina.Nome, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O fim do prazo pela rotina avisa quem perdeu o acesso e os grupos, uma vez, mesmo com a rotina rodando de novo")]
    public async Task FimDoPrazo_AvisaUmaVez()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(73));

        await EntregarAsync(async () =>
        {
            await ProcessarJanelasAsync();
            await ProcessarJanelasAsync();
        });
        Caixa[] caixa = await LerCaixaAsync();

        Para(caixa, Revogada).Should().Equal(Leandro.Email);
        caixa.Where(m => m.Assunto.Contains(Encerrada, StringComparison.Ordinal)).Select(m => m.Para).Should().OnlyHaveUniqueItems();
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }

    [Fact(DisplayName = "O lembrete de 48 horas advertiu o beneficiário pendente uma vez só, ainda que a rotina rode de novo")]
    public async Task Lembrete_NaoRepete()
    {
        await AbrirEEntregarAsync(Leandro);
        Relogio.Avancar(TimeSpan.FromHours(25));

        await EntregarAsync(async () =>
        {
            await ProcessarJanelasAsync();
            await ProcessarJanelasAsync();
        });
        Caixa[] caixa = await LerCaixaAsync();

        Para(caixa, Advertencia).Should().Equal(Leandro.Email);
    }

    [Fact(DisplayName = "O dono da concessão retida recebe a advertência do prazo e os outros gestores, a mensagem de agir")]
    public async Task Retida_AvisaODonoEOsOutrosGestores()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Isabella, Leandro);
        using HttpClient isabella = await GestaoAsync();
        await EntregarAsync(async () =>
        {
            using HttpResponseMessage lote = await EnviarLoteAsync(isabella, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);
            lote.IsSuccessStatusCode.Should().BeTrue();
        });
        await EsvaziarCaixaAsync();
        Relogio.Avancar(TimeSpan.FromHours(49));

        await EntregarAsync(ProcessarJanelasAsync);
        Caixa[] caixa = await LerCaixaAsync();

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
        HttpResponseMessage? remocao = null;

        await EntregarAsync(async () =>
        {
            remocao = await RemoverBrutoAsync(gestor, cenario.PerfilId, [Sensivel]);
            Relogio.Avancar(TimeSpan.FromHours(73));
            await ProcessarJanelasAsync();
            await ProcessarJanelasAsync();
        });
        Caixa[] caixa = await LerCaixaAsync();

        remocao!.IsSuccessStatusCode.Should().BeTrue();
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

        HttpResponseMessage? primeira = null;
        HttpResponseMessage? replay = null;

        await EntregarAsync(async () => primeira = await EnviarLoteAsync(gestao, cenario.PerfilId, decisoes, confirmacaoDeExclusao: true, chave: chave));
        Caixa[] aposAPrimeira = await LerCaixaAsync();
        await EntregarAsync(async () => replay = await EnviarLoteAsync(gestao, cenario.PerfilId, decisoes, confirmacaoDeExclusao: true, chave: chave));
        Caixa[] aposOReplay = await LerCaixaAsync();

        primeira!.IsSuccessStatusCode.Should().BeTrue();
        replay!.IsSuccessStatusCode.Should().BeTrue();
        Tem(aposAPrimeira, Revogada).Should().BeTrue();
        aposOReplay.Select(m => (m.Para, m.Assunto)).Should().BeEquivalentTo(aposAPrimeira.Select(m => (m.Para, m.Assunto)));
    }

    [Fact(DisplayName = "O pedido ativo devolvido pela abertura da janela avisa o solicitante e o beneficiário uma vez, ainda que a rotina rode")]
    public async Task PedidoDevolvidoPelaJanela_AvisaUmaVez()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        using HttpClient betina = Cliente(Betina);
        Guid pedido = default;
        await EntregarAsync(async () => pedido = await Pedidos.PedidosDeConcessaoTestBase.CriarAsync(betina, vigente.PerfilId, await SemearUnidadeAsync(), null));
        await EsvaziarCaixaAsync();
        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel);

        await EntregarAsync(async () =>
        {
            await EnviarEVerificarAsync(gestor, nova);
            await ProcessarJanelasAsync();
        });
        Caixa[] caixa = await LerCaixaAsync();

        pedido.Should().NotBeEmpty();
        Para(caixa, Devolvido).Should().Contain(Betina.Email).And.OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "A revogação manual depois do corte e antes da rotina não vira aviso de revogação pelo fim")]
    public async Task RevogacaoManualDepoisDoCorte_NaoViraAvisoDoFim()
    {
        JanelaAberta cenario = await AbrirEEntregarAsync(Leandro, Betina);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        HttpResponseMessage? revogacao = null;
        Relogio.Avancar(TimeSpan.FromHours(73));

        await EntregarAsync(async () =>
        {
            revogacao = await EnviarAsync(
                administrador, HttpMethod.Post, $"/api/autorizacao/concessoes/{cenario.Concessoes[Leandro]}/revogacao",
                new { motivo = "A pessoa deixou a função.", confirmacaoEmail = Leandro.Email });
            await ProcessarJanelasAsync();
        });
        Caixa[] caixa = await LerCaixaAsync();

        Tem(caixa, Encerrada).Should().BeTrue();
        revogacao!.StatusCode.Should().Be(HttpStatusCode.NoContent, await revogacao.Content.ReadAsStringAsync());
        Para(caixa, Revogada).Should().NotContain(Leandro.Email);
    }

    private static bool Tem(Caixa[] caixa, string assunto) => caixa.Any(m => m.Assunto.Contains(assunto, StringComparison.Ordinal));

    private static string[] Para(Caixa[] caixa, string assunto) =>
        [.. caixa.Where(m => m.Assunto.Contains(assunto, StringComparison.Ordinal)).Select(m => m.Para)];

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
