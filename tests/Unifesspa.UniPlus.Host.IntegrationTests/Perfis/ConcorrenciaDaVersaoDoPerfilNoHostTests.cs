namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.Tracking;

/// <summary>
/// As operações sobre o perfil seguem a mesma ordem de travas, então duas transações concorrentes se resolvem pela que
/// trava primeiro. Cada teste segura uma transação aberta, espera o banco registrar a outra bloqueada e só então
/// confirma, de modo que a ordem é a decidida pelo teste e não pela sorte do agendador.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConcorrenciaDaVersaoDoPerfilNoHostTests : VersoesDoPerfilNoHostTestBase
{
    public ConcorrenciaDaVersaoDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A concessão que vence a trava não impede a aprovação da etapa da gestão: a versão segue pendente da proteção de dados")]
    public async Task ConcessaoVenceATrava_AprovacaoDaGestaoSegue()
    {
        (Envio original, Envio ampliada) = await PerfilComAmpliadaVerificadaAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste concessao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await ConcederAsync(concessao.Servicos, Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(unidade));

        Task<HttpResponseMessage> aprovacao = AprovarBrutoAsync(gestao, ampliada, EtapaGestao);
        await concessao.AguardarBloqueioAsync();
        aprovacao.IsCompleted.Should().BeFalse();
        await concessao.ConfirmarAsync();

        (await aprovacao).StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirConcessaoESituacaoAsync(original.PerfilId, ampliada.VersaoId, "pendente");
        (await VigenciasAsync(original.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == original.VersaoId);
    }

    [Fact(DisplayName = "A aprovação final que vence a trava faz a concessão ser avaliada contra a composição nova", Skip = "Intermitente na suíte completa: a transação retida termina antes de a segunda operação disputar a trava (#2305).")]
    public async Task AprovacaoVenceATrava_ConcessaoLeAComposicaoNova()
    {
        (Envio original, Envio ampliada) = await PerfilComAmpliadaVerificadaAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        await AprovarAsync(gestao, ampliada, EtapaGestao);
        Task<Perfil>? concessao = null;

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            original.PerfilId, () => AprovarBrutoAsync(protecao, ampliada, EtapaProtecao), NaFila(() => concessao = ConcederEmTransacaoAsync(original.PerfilId, EscopoDaConcessao.DaUnidade(unidade))));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK, await respostas[0].Content.ReadAsStringAsync());
        (await concessao!).ComposicaoEmVigor.Should().BeEquivalentTo(Consultar, Sensivel);
        await ConferirConcessaoESituacaoAsync(original.PerfilId, ampliada.VersaoId, "vigente");
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "Duas aprovações simultâneas da mesma etapa: uma grava e a outra recebe conflito")]
    public async Task AprovacoesSimultaneas_UmaGravaEAOutraRecebeConflito()
    {
        (Envio original, Envio ampliada) = await PerfilComAmpliadaVerificadaAsync();
        using HttpClient primeira = await GestaoAsync();
        using HttpClient segunda = await ClienteComPermissoesAsync(Betina, AprovarFuncional);

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            original.PerfilId,
            () => AprovarBrutoAsync(primeira, ampliada, EtapaGestao),
            () => AprovarBrutoAsync(segunda, ampliada, EtapaGestao));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK, await respostas[0].Content.ReadAsStringAsync());
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.Conflict, "etapa_ja_decidida");
        (await DecisoesNoBancoAsync(ampliada.VersaoId)).Should().Be(1);
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "Recusa e aprovação simultâneas: vence a primeira e a segunda encontra a versão como ficou")]
    public async Task RecusaEAprovacaoSimultaneas_VenceAPrimeira()
    {
        (Envio original, Envio ampliada) = await PerfilComAmpliadaVerificadaAsync();
        using HttpClient recusadora = await GestaoAsync();
        using HttpClient aprovador = await ClienteComPermissoesAsync(Betina, AprovarFuncional);

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            original.PerfilId,
            () => RecusarBrutoAsync(recusadora, ampliada, EtapaGestao),
            () => AprovarBrutoAsync(aprovador, ampliada, EtapaGestao));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK, await respostas[0].Content.ReadAsStringAsync());
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.Conflict, "etapa_ja_decidida");
        (await SituacaoDaVersaoNoBancoAsync(ampliada.VersaoId)).Should().Be("recusada");
        (await DecisoesNoBancoAsync(ampliada.VersaoId)).Should().Be(1);
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "Remoção e aprovação simultâneas: a remoção cancela a pendente e a aprovação que perde não entra em vigor")]
    public async Task RemocaoEAprovacaoSimultaneas_ARemocaoPrevalece()
    {
        (Envio original, Envio ampliada) = await PerfilComAmpliadaVerificadaAsync(Consultar, Sensivel);
        using HttpClient gestor = await GestorAsync();
        using HttpClient gestao = await GestaoAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            original.PerfilId,
            () => RemoverBrutoAsync(gestor, original.PerfilId, [Consultar]),
            () => AprovarBrutoAsync(gestao, ampliada, EtapaGestao));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK, await respostas[0].Content.ReadAsStringAsync());
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.UnprocessableEntity, "versao_nao_pendente");
        (await SituacaoDaVersaoNoBancoAsync(ampliada.VersaoId)).Should().Be("cancelada");
        (await DecisoesNoBancoAsync(ampliada.VersaoId)).Should().Be(0);
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "Aprovação e expiração simultâneas: a versão vencida expira e nunca entra em vigor", Skip = "Intermitente na suíte completa: a transação retida termina antes de a segunda operação disputar a trava (#2305).")]
    public async Task AprovacaoEExpiracaoSimultaneas_ExpiraSemEntrarEmVigor()
    {
        (Envio original, Envio ampliada) = await PerfilComAmpliadaVerificadaAsync();
        await ExecutarSqlAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() - interval '1 minute' WHERE id = {0}", ampliada.VersaoId);
        using HttpClient gestao = await GestaoAsync();
        Task<int>? expiracao = null;

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            original.PerfilId, () => AprovarBrutoAsync(gestao, ampliada, EtapaGestao), NaFila(() => expiracao = ExpirarPendentesAsync()));

        await ConferirRecusaDoPerfilAsync(respostas[0], HttpStatusCode.UnprocessableEntity, "versao_expirada");
        await expiracao!;
        (await SituacaoDaVersaoNoBancoAsync(ampliada.VersaoId)).Should().Be("expirada");
        (await VigenciasAsync(original.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == original.VersaoId);
        DescartarTodas(respostas);
    }

    private async Task<(Envio Original, Envio Ampliada)> PerfilComAmpliadaVerificadaAsync(params string[] doOriginal)
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, doOriginal.Length > 0 ? doOriginal : [Consultar]);
        await EnviarEVerificarAsync(gestor, original);
        Envio ampliada = await CriarVersaoAsync(gestor, original.PerfilId, Nome(), Consultar, Sensivel);
        await ConfirmarEnvioAsync(gestor, ampliada);
        await InjetarResultadoAsync(ampliada.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(gestor, original.PerfilId) == "verificado", "o documento não foi verificado");

        return (original, ampliada);
    }

    private async Task<int> ExpirarPendentesAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<ExpiracaoDasVersoesPendentes>().ExecutarAsync(CancellationToken.None);
    }

    private async Task<int> DecisoesNoBancoAsync(Guid versaoId)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.decisoes_da_versao WHERE versao_id = {versaoId}")
            .SingleAsync();
    }

    private async Task<Perfil> ConcederEmTransacaoAsync(Guid perfilId, EscopoDaConcessao escopo)
    {
        await using TransacaoRetidaDeTeste transacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        Perfil perfil = await ConcederAsync(transacao.Servicos, Leandro, perfilId, escopo);
        await transacao.ConfirmarAsync();

        return perfil;
    }

    private static void DescartarTodas(HttpResponseMessage[] respostas)
    {
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

    // A segunda operação da fila: começa e espera as duas sessões disputarem a trava do perfil.
    private Func<Task<HttpResponseMessage>> NaFila(Action iniciar) => async () =>
    {
        iniciar();
        await AguardarFilaDaTravaAsync(2);
        return new HttpResponseMessage(HttpStatusCode.OK);
    };

    private async Task ConferirConcessaoESituacaoAsync(Guid perfilId, Guid versaoId, string situacao)
    {
        (await ConcessoesAsync(Leandro, perfilId)).Should().ContainSingle();
        (await SituacaoDaVersaoNoBancoAsync(versaoId)).Should().Be(situacao);
    }
}
