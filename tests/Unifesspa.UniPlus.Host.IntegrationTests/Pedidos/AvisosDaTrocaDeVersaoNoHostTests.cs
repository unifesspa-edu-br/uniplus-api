namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Wolverine.Tracking;

/// <summary>
/// A entrada em vigor da versão pela fila do host real: o veredicto troca a vigência, mexe nas concessões e nos pedidos
/// ativos e grava os avisos no outbox, tudo na mesma transação. Com o aviso chegando ao relay quando ela confirma, e
/// nada ficando quando ela falha por uma restrição do banco.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AvisosDaTrocaDeVersaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private const string Catalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string Motivos = "selecao:motivos-de-decisao:manter";
    private const string Devolvido = "Pedido de acesso devolvido";
    private const string AguardaOGestor = "análise do gestor de acessos";

    public AvisosDaTrocaDeVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A versão com acréscimo devolve o pedido ativo e avisa o solicitante e o beneficiário pelo outbox")]
    public async Task Acrescimo_DevolveEAvisaAsPartes()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        (Guid perfil, Guid pedido) = await PedidoAguardandoOBeneficiarioAsync(gestor, Catalogo);
        Envio versao2 = await PrepararVersaoAsync(gestor, perfil, Catalogo, Motivos);

        await EntregarPelaFilaAsync(versao2.DocumentoId);
        ITrackedSession sessao = await RastrearAsync(() => AprovarAteAVigenciaAsync(gestor, versao2));

        sessao.Sent.MessagesOf<EnviarEmail>().Where(m => m.Finalidade == "perfil.pedido_devolvido_pela_janela").Should().HaveCount(2, "o solicitante e o beneficiário");
        sessao.Sent.MessagesOf<EnviarEmail>().Should().ContainSingle(m => m.Finalidade == "perfil.versao_encerrada", "a versão anterior saiu sem concessão a migrar");
        await AguardarAvisoAsync(Betina, pedido, Devolvido);
        await AguardarAvisoAsync(Leandro, pedido, Devolvido);
        JsonElement lido = await LerAsync(gestor, $"{Rota}/{pedido}");
        lido.GetProperty("situacao").GetString().Should().Be("devolvido");
        lido.GetProperty("trocasDeVersao").EnumerateArray().Single().GetProperty("desfecho").GetString().Should().Be("devolvido");
    }

    [Fact(DisplayName = "A versão sem acréscimo devolve ao gestor o pedido que esperava a proteção de dados e avisa a fila do gestor pelo outbox")]
    public async Task SemAcrescimo_VoltaAoGestorEAvisaAFila()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        (Guid perfil, Guid pedido) = await PedidoAguardandoAProtecaoAsync(gestor);
        int avisosAntes = (await MensagensAsync(Diogo, pedido)).Count(EhDaFilaDoGestor);
        Envio versao2 = await PrepararVersaoAsync(gestor, perfil, Sensivel);

        await EntregarPelaFilaAsync(versao2.DocumentoId);
        ITrackedSession sessao = await RastrearAsync(() => AprovarAteAVigenciaAsync(gestor, versao2));

        sessao.Sent.MessagesOf<EnviarEmail>().Should().NotBeEmpty("o retorno ao gestor avisa a fila dele");
        await AguardarAsync(
            async () => (await MensagensAsync(Diogo, pedido)).Count(EhDaFilaDoGestor) > avisosAntes,
            "a fila do gestor não foi avisada do retorno do pedido");
        JsonElement lido = await LerAsync(gestor, $"{Rota}/{pedido}");
        lido.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        lido.GetProperty("trocasDeVersao").EnumerateArray().Single().GetProperty("desfecho").GetString().Should().Be("voltou_ao_gestor");
    }

    [Fact(DisplayName = "Se a gravação da troca falha, nem a vigência, nem o pedido, nem os avisos ficam, e a reentrega aplica tudo junto")]
    public async Task FalhaNaGravacao_DesfazTudoEAReentregaAplica()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        (Guid perfil, Guid pedido) = await PedidoAguardandoOBeneficiarioAsync(gestor, Catalogo);
        Envio versao2 = await PrepararVersaoAsync(gestor, perfil, Catalogo, Motivos);
        await ExecutarSqlAsync(
            "ALTER TABLE autorizacao.trocas_de_versao_do_pedido ADD CONSTRAINT ck_teste_sem_troca CHECK (false) NOT VALID");

        await EntregarPelaFilaAsync(versao2.DocumentoId);

        try
        {
            await RastrearAsync(async () =>
            {
                using HttpResponseMessage resposta = await AprovarUltimaEtapaBrutoAsync(gestor, versao2);
                resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            },
            aceitarFalha: true);
            await ConferirNadaGravadoAsync(gestor, perfil, versao2, pedido);
        }
        finally
        {
            await ExecutarSqlAsync("ALTER TABLE autorizacao.trocas_de_versao_do_pedido DROP CONSTRAINT IF EXISTS ck_teste_sem_troca");
        }

        using HttpResponseMessage reentrega = await AprovarUltimaEtapaBrutoAsync(gestor, versao2);
        reentrega.StatusCode.Should().Be(HttpStatusCode.OK, await reentrega.Content.ReadAsStringAsync());

        (await TemVigenteComVersaoAsync(gestor, perfil, versao2.VersaoId)).Should().BeTrue();
        (await SituacaoAsync(gestor, pedido)).Should().Be("devolvido");
        await AguardarAvisoAsync(Betina, pedido, Devolvido);
    }

    private async Task ConferirNadaGravadoAsync(HttpClient gestor, Guid perfil, Envio versao2, Guid pedido)
    {
        (await SituacaoDoDocumentoAsync(gestor, perfil)).Should().Be("verificado");
        (await DecisoesAsync(versao2.VersaoId)).Should().NotContain(d => d.Etapa == "ProtecaoDeDados");
        (await SituacaoDaVersaoNoBancoAsync(versao2.VersaoId)).Should().Be("pendente");
        (await VigenciasAsync(perfil)).Should().ContainSingle(v => v.Aberta && v.VersaoId != versao2.VersaoId);
        (await SituacaoAsync(gestor, pedido)).Should().Be("aguardando_beneficiario");
        (await LerAsync(gestor, $"{Rota}/{pedido}")).GetProperty("trocasDeVersao").GetArrayLength().Should().Be(0);
        (await MensagensAsync(Betina, pedido)).Select(m => m.GetProperty("Subject").GetString()).Should().NotContain(a => a!.Contains(Devolvido, StringComparison.Ordinal));
        (await EnvelopesDeEmailNoOutboxAsync()).Should().Be(0, "os avisos entram no outbox na transação da troca e ela foi desfeita");
    }

    private async Task<int> EnvelopesDeEmailNoOutboxAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'")
            .SingleAsync();
    }

    private static async Task<Envio> PrepararVersaoAsync(HttpClient gestor, Guid perfil, params string[] permissoes)
    {
        Envio envio = await CriarVersaoAsync(gestor, perfil, Nome(), permissoes);
        await ConfirmarEnvioAsync(gestor, envio);

        return envio;
    }

    private async Task<(Guid Perfil, Guid Pedido)> PedidoAguardandoOBeneficiarioAsync(HttpClient gestor, params string[] permissoes)
    {
        Guid perfil = (await PerfilVigenteAsync(gestor, permissoes)).PerfilId;
        using HttpClient betina = Cliente(Betina);

        return (perfil, await CriarAsync(betina, perfil, null, Leandro));
    }

    private async Task<(Guid Perfil, Guid Pedido)> PedidoAguardandoAProtecaoAsync(HttpClient gestor)
    {
        Guid perfil = (await PerfilVigenteAsync(gestor, Sensivel)).PerfilId;
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid pedido = await CriarAsync(betina, perfil, unidade, Leandro);
        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{pedido}/aceite")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(gestor, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(gestor, pedido)).Should().Be("aguardando_protecao_de_dados");

        return (perfil, pedido);
    }

    private static bool EhDaFilaDoGestor(JsonElement mensagem) =>
        mensagem.GetProperty("Subject").GetString()!.Contains(AguardaOGestor, StringComparison.OrdinalIgnoreCase);
}
