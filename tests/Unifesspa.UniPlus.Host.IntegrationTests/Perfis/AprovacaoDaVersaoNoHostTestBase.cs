namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Tracking;

/// <summary>Montagens comuns às suítes da aprovação, da remoção e dos avisos da versão do perfil pelo host real.</summary>
public abstract class AprovacaoDaVersaoNoHostTestBase : VersoesDoPerfilNoHostTestBase
{
    private protected AprovacaoDaVersaoNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>Gestora que também exerce a proteção de dados, como a persona do portal.</summary>
    private protected Task<HttpClient> GestoraComAmbasAsync() =>
        ClienteComPermissoesAsync(Isabella, AprovarFuncional, AprovarProtecaoDeDados);

    private protected static Task<JsonElement> PerfilAsync(HttpClient cliente, Guid perfilId) =>
        LerAsync(cliente, $"{RotaPerfis}/{perfilId}");

    /// <summary>Cria o perfil e o leva à vigência pelo caminho real, aprovando as etapas que a composição pede.</summary>
    private protected async Task<Envio> PerfilVigenteAsync(HttpClient criador, params string[] permissoes)
    {
        Envio envio = await CriarPerfilAsync(criador, permissoes);
        await EnviarEVerificarAsync(criador, envio);

        return envio;
    }

    /// <summary>Cria a versão do perfil e a deixa com o documento verificado, pronta para a primeira decisão.</summary>
    private protected async Task<Envio> PendenteVerificadaAsync(HttpClient criador, Guid perfilId, params string[] permissoes)
    {
        Envio envio = await CriarVersaoAsync(criador, perfilId, Nome(), permissoes);
        await ConfirmarEnvioAsync(criador, envio);
        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(criador, perfilId) == "verificado", "o documento não foi verificado");

        return envio;
    }

    /// <summary>Decide a última etapa que a versão pede e devolve a resposta dela sem conferir o status.</summary>
    private protected async Task<HttpResponseMessage> AprovarUltimaEtapaBrutoAsync(HttpClient criador, Envio envio)
    {
        JsonElement pendente = (await PerfilAsync(criador, envio.PerfilId)).GetProperty("versaoPendente");
        if (pendente.GetProperty("etapaAtual").GetString() == EtapaGestao && pendente.GetProperty("exigeProtecaoDeDados").GetBoolean())
        {
            using HttpClient gestao = await GestaoAsync();
            await AprovarAsync(gestao, envio, EtapaGestao);
        }
        else if (pendente.GetProperty("etapaAtual").GetString() == EtapaGestao)
        {
            using HttpClient unica = await GestaoAsync();
            return await AprovarBrutoAsync(unica, envio, EtapaGestao);
        }

        using HttpClient protecao = await ProtecaoAsync();

        return await AprovarBrutoAsync(protecao, envio, EtapaProtecao);
    }

    /// <summary>Executa a ação e espera o Wolverine terminar de processar tudo o que ela disparou, o outbox e o relay inclusive.</summary>
    private protected Task<ITrackedSession> RastrearAsync(Func<Task> acao, bool aceitarFalha = false)
    {
        TrackedSessionConfiguration sessao = Fixture.Factory.Services.GetRequiredService<IHost>()
            .TrackActivity()
            .IncludeExternalTransports()
            .Timeout(TimeSpan.FromSeconds(60));

        return (aceitarFalha ? sessao.DoNotAssertOnExceptionsDetected() : sessao).ExecuteAndWaitAsync(_ => acao());
    }

    private protected async Task<DecisaoGravada[]> DecisoesAsync(Guid versaoId)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        List<DecisaoDaVersao> decisoes = await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>()
            .DecisoesDasVersoes.AsNoTracking().Where(d => d.VersaoId == versaoId).OrderBy(d => d.Instante).ToListAsync();

        return [.. decisoes.Select(d => new DecisaoGravada(d.Etapa.ToString(), d.Resultado.ToString(), d.DecisorSub, d.DecisorTemConcessao))];
    }

    private protected sealed record DecisaoGravada(string Etapa, string Resultado, string Sub, bool TemConcessao);
}
