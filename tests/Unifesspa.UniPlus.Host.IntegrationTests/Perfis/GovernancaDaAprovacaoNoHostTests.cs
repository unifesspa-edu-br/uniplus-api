namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>Os perfis de governança carregam as permissões da aprovação e não recebem nenhuma das operações novas.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class GovernancaDaAprovacaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private const string AprovarFuncionalDoPerfil = "autorizacao:perfis:aprovar-funcional";
    private const string AprovarProtecaoDoPerfil = "autorizacao:perfis:aprovar-protecao-de-dados";

    public GovernancaDaAprovacaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Cada perfil de governança reúne a permissão de aprovar da sua etapa")]
    public async Task PerfisDeGovernanca_ReunemAsPermissoesNovas()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        string[] gestor = await CodigosAsync(db, PerfisDeSistema.GestorDeAcessosId);
        string[] protecao = await CodigosAsync(db, PerfisDeSistema.RepresentanteDoControladorId);

        gestor.Should().Contain(AprovarFuncionalDoPerfil).And.NotContain(AprovarProtecaoDoPerfil);
        protecao.Should().Contain(AprovarProtecaoDoPerfil).And.NotContain(AprovarFuncionalDoPerfil);
    }

    [Fact(DisplayName = "Remoção, aprovação, recusa e cancelamento no perfil de governança são recusados como imutáveis")]
    public async Task OperacoesNoPerfilDeGovernanca_Recusadas()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorComAmbasAsync();
        using HttpClient diogo = await GestorAsync();
        Envio alvo = new(PerfisDeSistema.GestorDeAcessosId, Guid.NewGuid(), Guid.NewGuid(), string.Empty);

        using HttpResponseMessage remocao = await RemoverBrutoAsync(diogo, alvo.PerfilId, [AprovarFuncionalDoPerfil]);
        using HttpResponseMessage aprovacao = await AprovarBrutoAsync(gestor, alvo, EtapaGestao);
        using HttpResponseMessage recusa = await RecusarBrutoAsync(gestor, alvo, EtapaGestao);
        using HttpResponseMessage cancelamento = await CancelarBrutoAsync(diogo, alvo);

        foreach (HttpResponseMessage resposta in new[] { remocao, aprovacao, recusa, cancelamento })
        {
            await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "governanca_imutavel");
        }
    }

    private static async Task<string[]> CodigosAsync(AutorizacaoDbContext db, Guid perfilId) =>
        await db.Database
            .SqlQuery<string>($"SELECT codigo AS \"Value\" FROM autorizacao.perfil_permissoes WHERE perfil_id = {perfilId}")
            .ToArrayAsync();
}
