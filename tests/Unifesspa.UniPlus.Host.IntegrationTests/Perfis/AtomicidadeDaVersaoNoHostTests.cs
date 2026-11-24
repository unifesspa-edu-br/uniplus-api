namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A operação sobre o perfil é uma transação só: o fato, as decisões, os avisos e o outbox entram juntos ou nenhum
/// entra. As falhas vêm do banco real, sem gancho no código de produção.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AtomicidadeDaVersaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    public AtomicidadeDaVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A remoção que falha não deixa fato, aviso nem e-mail no outbox")]
    public async Task RemocaoQueFalha_NaoDeixaNada()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using var cenario = new CenarioDeRemocaoComFalha(this, diogo);
        await cenario.ExecutarAsync();
        cenario.Falha.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await TemVigenteComVersaoAsync(diogo, cenario.Vigente.PerfilId, cenario.Vigente.VersaoId)).Should().BeTrue();
        (await AvisosRegistradosAsync()).Should().Be(cenario.AvisosAntes);
        (await cenario.EnvelopesDeEmailNoOutboxAsync()).Should().Be(0);
        (await RemoverBrutoAsync(diogo, cenario.Vigente.PerfilId, [Sensivel])).Dispose();
        await AguardarAvisoAsync(Isabella, cenario.Vigente.PerfilId, "Permissões removidas");
        (await AvisosRegistradosAsync()).Should().BeGreaterThan(cenario.AvisosAntes);
    }

    private sealed class CenarioDeRemocaoComFalha(AtomicidadeDaVersaoNoHostTests teste, HttpClient gestor) : IDisposable
    {
        public Envio Vigente { get; private set; } = null!;
        public int AvisosAntes { get; private set; }
        public HttpResponseMessage Falha { get; private set; } = null!;

        public async Task<int> EnvelopesDeEmailNoOutboxAsync()
        {
            await using AsyncServiceScope escopo = teste.Fixture.Factory.Services.CreateAsyncScope();

            return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'")
                .SingleAsync();
        }

        public void Dispose() => Falha?.Dispose();

        public async Task ExecutarAsync()
        {
            Vigente = await teste.PerfilVigenteAsync(gestor, Consultar, Sensivel);
            AvisosAntes = await teste.AvisosRegistradosAsync();
            await teste.ExecutarSqlAsync("ALTER TABLE autorizacao.vigencias_do_perfil ADD CONSTRAINT ck_teste_sem_vigencia CHECK (false) NOT VALID");
            try
            {
                Falha = await RemoverBrutoAsync(gestor, Vigente.PerfilId, [Sensivel]);
            }
            finally
            {
                await teste.ExecutarSqlAsync("ALTER TABLE autorizacao.vigencias_do_perfil DROP CONSTRAINT IF EXISTS ck_teste_sem_vigencia");
            }
        }
    }

    [Fact(DisplayName = "O nome tomado na gravação provoca conflito e a repetição recusa o nome duplicado")]
    public async Task NomeTomadoNoCommit_RecusaAprovacaoERepeticao()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using var cenario = new CenarioDeNomeTomado(this, diogo, isabella);
        await cenario.ExecutarAsync();
        cenario.Falha.StatusCode.Should().Be(HttpStatusCode.Conflict, await cenario.Falha.Content.ReadAsStringAsync());
        (await SituacaoDaVersaoNoBancoAsync(cenario.Versao.VersaoId)).Should().Be("PENDENTE");
        (await DecisoesAsync(cenario.Versao.VersaoId)).Should().BeEmpty();
        (await VigenciasAsync(cenario.Vigente.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == cenario.Vigente.VersaoId);
        using HttpResponseMessage repeticao = await AprovarBrutoAsync(isabella, cenario.Versao, EtapaGestao);
        await ConferirRecusaDoPerfilAsync(repeticao, HttpStatusCode.Conflict, "nome_duplicado");
    }

    private sealed class CenarioDeNomeTomado(AtomicidadeDaVersaoNoHostTests teste, HttpClient gestor, HttpClient gestao) : IDisposable
    {
        public Envio Vigente { get; private set; } = null!;
        public Envio Versao { get; private set; } = null!;
        public HttpResponseMessage Falha { get; private set; } = null!;

        public async Task ExecutarAsync()
        {
            Vigente = await teste.PerfilVigenteAsync(gestor, Consultar);
            string nome = Nome();
            Versao = await CriarVersaoAsync(gestor, Vigente.PerfilId, nome, Consultar);
            await teste.ConfirmarEVerificarAsync(gestor, Versao);
            await using TransacaoRetidaDeTeste outra = await TransacaoRetidaDeTeste.AbrirAsync(teste.Fixture.Factory.Services);
            await outra.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
                "INSERT INTO autorizacao.perfis (id, nome, descricao, ativo, created_at) VALUES ({0}, {1}, 'Perfil que toma o nome.', true, now())",
                Guid.CreateVersion7(), nome);
            Task<HttpResponseMessage> aprovacao = AprovarBrutoAsync(gestao, Versao, EtapaGestao);
            await outra.AguardarBloqueioAsync();
            await outra.ConfirmarAsync();
            Falha = await aprovacao;
        }

        public void Dispose() => Falha?.Dispose();
    }

    [Fact(DisplayName = "O pedido depois da aprovação final nasce na versão nova")]
    public async Task AprovacaoFinal_PedidoSeguinteNasceNaVersaoNova()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);
        using HttpClient betina = Cliente(Betina);
        Guid unidade = await SemearUnidadeAsync();
        HttpResponseMessage[] respostas = await EmFilaSobOBloqueioAsync(
            vigente.PerfilId, () => AprovarBrutoAsync(isabella, versao, EtapaGestao),
            () => CriarBrutoAsync(betina, vigente.PerfilId, unidade, Leandro.Email));
        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK, await respostas[0].Content.ReadAsStringAsync());
        JsonElement pedido = await CorpoAsync(respostas[1], HttpStatusCode.Created);
        pedido.GetProperty("versao").GetInt32().Should().Be(2);
        DescartarTodas(respostas);
    }

    private async Task<int> AvisosRegistradosAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.avisos_emitidos")
            .SingleAsync();
    }


}
