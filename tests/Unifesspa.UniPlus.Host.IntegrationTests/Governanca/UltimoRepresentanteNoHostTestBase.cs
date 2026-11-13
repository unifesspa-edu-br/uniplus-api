namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Providencias;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Base das suítes da regra do último Representante do controlador pelo host real, com transações reais (UNI-REQ-0394,
/// ADR-0162). Parte da governança reiniciada: Diogo, Isabella e Betina são gestores de acessos, Marlene é a única
/// Representante e Cecília, o Encarregado de dados.
/// </summary>
public abstract class UltimoRepresentanteNoHostTestBase : ProvidenciasNoHostTestBase
{
    internal const string UltimoRepresentante = "uniplus.autorizacao.concessao.ultimo_representante";

    /// <summary>A trava única do papel de Representante, que a revogação dele toma antes de qualquer outra.</summary>
    internal const string TravaDoPapel = "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE";

    internal static readonly Guid Representante = PerfisDeSistema.RepresentanteDoControladorId;

    private protected UltimoRepresentanteNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>A concessão de Representante da pessoa, com o e-mail dela; a já existente é reaproveitada.</summary>
    private protected Task<Guid> DesignacaoAsync(Persona pessoa) =>
        ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, pessoa.Sub.ToString(), TipoDeGovernanca.RepresentanteDoControlador, pessoa.Email);

    /// <summary>Quem tem concessão vigente de Representante agora, lido direto do banco.</summary>
    private protected async Task<string[]> VigentesAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        DateTimeOffset agora = DateTimeOffset.UtcNow;

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes.AsNoTracking()
            .Where(c => c.PerfilId == Representante && c.RevogadaEm == null && (c.ValidaAte == null || c.ValidaAte > agora))
            .Select(c => c.BeneficiarioSub)
            .ToArrayAsync();
    }

    /// <summary>O pedido de designação de Representante feito por Betina, com o documento verificado e aceito pelo beneficiário.</summary>
    private protected async Task<Guid> DesignacaoAceitaAsync(Persona beneficiario)
    {
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, Representante, null, beneficiario);
        await AnexarVerificadoAsync(betina, id);
        await AceitarAsync(Cliente(beneficiario), id);

        return id;
    }

    /// <summary>Confere as respostas de duas revogações simultâneas: uma aceita e a outra recusada pela regra.</summary>
    private protected static async Task ConferirUmaAceitaEOutraRecusadaAsync(HttpResponseMessage[] respostas, HttpStatusCode aceita)
    {
        respostas.Select(static r => r.StatusCode).Should().BeEquivalentTo([aceita, HttpStatusCode.UnprocessableEntity]);
        string recusa = await respostas.Single(static r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Content.ReadAsStringAsync();
        recusa.Should().Contain(UltimoRepresentante);
    }
}
