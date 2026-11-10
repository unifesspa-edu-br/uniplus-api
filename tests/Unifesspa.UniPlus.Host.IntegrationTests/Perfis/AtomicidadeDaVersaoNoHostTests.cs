namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A operação sobre o perfil é uma transação só: o fato, as decisões, os avisos e o outbox entram juntos ou nenhum
/// entra. As falhas vêm do banco real, sem gancho no código de produção.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtomicidadeDaVersaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    public AtomicidadeDaVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A remoção que falha na gravação não deixa fato, aviso registrado nem e-mail no outbox, e a repetição aplica tudo")]
    public async Task RemocaoQueFalha_NaoDeixaNada()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);
        int avisosAntes = await AvisosRegistradosAsync();
        await ExecutarSqlAsync("ALTER TABLE autorizacao.vigencias_do_perfil ADD CONSTRAINT ck_teste_sem_vigencia CHECK (false) NOT VALID");

        try
        {
            using HttpResponseMessage falha = await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel]);
            falha.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await ExecutarSqlAsync("ALTER TABLE autorizacao.vigencias_do_perfil DROP CONSTRAINT IF EXISTS ck_teste_sem_vigencia");
        }

        (await TemVigenteComVersaoAsync(diogo, vigente.PerfilId, vigente.VersaoId)).Should().BeTrue();
        (await AvisosRegistradosAsync()).Should().Be(avisosAntes);
        (await EnvelopesDeEmailNoOutboxAsync()).Should().Be(0);
        (await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel])).Dispose();
        await AguardarAvisoAsync(Isabella, vigente.PerfilId, "Permissões removidas");
        (await AvisosRegistradosAsync()).Should().BeGreaterThan(avisosAntes);
    }

    [Fact(DisplayName = "O nome tomado entre a conferência e a gravação desfaz a aprovação inteira, e a repetição recusa por nome duplicado")]
    public async Task NomeTomadoNoCommit_DesfazAAprovacao()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        string nomeNovo = Nome();
        Envio versao = await CriarVersaoAsync(diogo, vigente.PerfilId, nomeNovo, Consultar);
        await ConfirmarEnvioAsync(diogo, versao);
        await InjetarResultadoAsync(versao.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(diogo, vigente.PerfilId) == "verificado", "o documento não foi verificado");
        await using TransacaoRetidaDeTeste outra = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await outra.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "INSERT INTO autorizacao.perfis (id, nome, descricao, ativo, created_at) VALUES ({0}, {1}, 'Perfil que toma o nome.', true, now())",
            Guid.CreateVersion7(), nomeNovo);

        Task<HttpResponseMessage> aprovacao = AprovarBrutoAsync(isabella, versao, EtapaGestao);
        await outra.AguardarBloqueioAsync();
        await outra.ConfirmarAsync();
        using HttpResponseMessage falha = await aprovacao;

        falha.StatusCode.Should().Be(HttpStatusCode.Conflict, await falha.Content.ReadAsStringAsync());
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("pendente");
        (await DecisoesAsync(versao.VersaoId)).Should().BeEmpty();
        (await VigenciasAsync(vigente.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == vigente.VersaoId);
        using HttpResponseMessage repeticao = await AprovarBrutoAsync(isabella, versao, EtapaGestao);
        await ConferirRecusaDoPerfilAsync(repeticao, HttpStatusCode.Conflict, "nome_duplicado");
    }

    [Fact(DisplayName = "A aprovação final que vence a trava faz o pedido criado em seguida nascer na versão nova")]
    public async Task AprovacaoFinal_PedidoSeguinteNasceNaVersaoNova()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient betina = Cliente(Betina);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);
        Guid unidade = await SemearUnidadeAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            vigente.PerfilId,
            () => AprovarBrutoAsync(isabella, versao, EtapaGestao),
            () => CriarBrutoAsync(betina, vigente.PerfilId, unidade, Leandro.Email));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK, await respostas[0].Content.ReadAsStringAsync());
        JsonElement pedido = await CorpoAsync(respostas[1], HttpStatusCode.Created);
        pedido.GetProperty("versao").GetInt32().Should().Be(2);
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

    private async Task<int> AvisosRegistradosAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.avisos_emitidos")
            .SingleAsync();
    }

    private async Task<int> EnvelopesDeEmailNoOutboxAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'")
            .SingleAsync();
    }
}
