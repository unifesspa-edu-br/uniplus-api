namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>O cancelamento e a expiração da versão pendente do perfil pela API e pela rotina.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CancelamentoDaVersaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    public CancelamentoDaVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Quem criou a versão a cancela mesmo sem a permissão de manter perfis, e o perfil registra o cancelamento")]
    public async Task Criador_CancelaSemAPermissaoDeManter()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await CriarVersaoAsync(betina, vigente.PerfilId, Nome(), Consultar);
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Betina.Sub.ToString());
        using HttpClient semPermissao = Cliente(Betina);

        using HttpResponseMessage resposta = await CancelarBrutoAsync(semPermissao, versao);

        JsonElement perfil = await CorpoAsync(resposta, HttpStatusCode.OK);
        perfil.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
        perfil.GetProperty("ultimaVersaoEncerradaSemVigor").GetProperty("situacao").GetString().Should().Be("cancelada");
    }

    [Fact(DisplayName = "Terceiro sem a permissão de manter perfis não cancela a versão")]
    public async Task Terceiro_Recebe403()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await CriarVersaoAsync(diogo, vigente.PerfilId, Nome(), Consultar);
        using HttpClient leandro = Cliente(Leandro);

        using HttpResponseMessage resposta = await CancelarBrutoAsync(leandro, versao);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("pendente");
    }

    [Fact(DisplayName = "Sem a permissão de manter perfis a recusa é a mesma para o perfil que existe e o que não existe")]
    public async Task SemPermissao_RecusaIgualParaPerfilExistenteEInexistente()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio existente = await CriarVersaoAsync(diogo, vigente.PerfilId, Nome(), Consultar);
        Envio inexistente = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), string.Empty);
        using HttpClient leandro = Cliente(Leandro);

        using HttpResponseMessage doExistente = await CancelarBrutoAsync(leandro, existente);
        using HttpResponseMessage doInexistente = await CancelarBrutoAsync(leandro, inexistente);

        await ConferirRecusaDoPerfilAsync(doExistente, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
        await ConferirRecusaDoPerfilAsync(doInexistente, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
    }

    [Fact(DisplayName = "Quem tem a permissão de manter perfis recebe não encontrado para o perfil que não existe")]
    public async Task ComPermissao_PerfilInexistente_Recebe404()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio inexistente = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), string.Empty);

        using HttpResponseMessage resposta = await CancelarBrutoAsync(diogo, inexistente);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.NotFound, "nao_encontrado");
    }

    [Fact(DisplayName = "Quem tem a permissão de manter perfis cancela a versão de outra pessoa")]
    public async Task QuemTemManter_Cancela()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await CriarVersaoAsync(betina, vigente.PerfilId, Nome(), Consultar);

        using HttpResponseMessage resposta = await CancelarBrutoAsync(diogo, versao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("cancelada");
    }

    [Fact(DisplayName = "O cancelamento sem motivo é recusado e a versão segue pendente")]
    public async Task SemMotivo_Recusado()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await CriarVersaoAsync(diogo, vigente.PerfilId, Nome(), Consultar);

        using HttpResponseMessage resposta = await CancelarBrutoAsync(diogo, versao, motivo: null);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "motivo_invalido");
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("pendente");
    }

    [Fact(DisplayName = "A rotina expira a versão vencida sem pô-la em vigor, e a vigente segue")]
    public async Task Rotina_ExpiraAVencida()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);
        await ExecutarSqlAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() - interval '1 second' WHERE id = {0}", versao.VersaoId);

        int expiradas = await ExpirarAsync();

        expiradas.Should().BeGreaterThanOrEqualTo(1);
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("expirada");
        (await TemVigenteComVersaoAsync(diogo, vigente.PerfilId, vigente.VersaoId)).Should().BeTrue();
        (await SituacaoDaUltimaEncerradaAsync(diogo, vigente.PerfilId)).Should().Be("expirada");
    }

    [Fact(DisplayName = "Aprovar a versão vencida grava a expiração e repetir a chamada devolve a mesma recusa")]
    public async Task AprovarVencida_GravaAExpiracaoEOReplayRepete()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);
        await ExecutarSqlAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() - interval '1 second' WHERE id = {0}", versao.VersaoId);
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage primeira = await AprovarBrutoAsync(isabella, versao, EtapaGestao, chave);
        using HttpResponseMessage repeticao = await AprovarBrutoAsync(isabella, versao, EtapaGestao, chave);

        await ConferirRecusaDoPerfilAsync(primeira, HttpStatusCode.UnprocessableEntity, "versao_expirada");
        await ConferirRecusaDoPerfilAsync(repeticao, HttpStatusCode.UnprocessableEntity, "versao_expirada");
        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("expirada");
        (await DecisoesAsync(versao.VersaoId)).Should().BeEmpty("a expiração não é decisão de ninguém");
    }

    private async Task<int> ExpirarAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<ExpiracaoDasVersoesPendentes>().ExecutarAsync(CancellationToken.None);
    }
}
