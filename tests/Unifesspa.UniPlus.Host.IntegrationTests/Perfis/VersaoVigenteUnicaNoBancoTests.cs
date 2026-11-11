namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A garantia de uma só versão em vigor por perfil, conferida pelo banco no fim da transação e não a cada gravação, com
/// gravação direta que contorna o agregado.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VersaoVigenteUnicaNoBancoTests : AprovacaoDaVersaoNoHostTestBase
{
    private const string ViolacaoDeExclusao = "23P01";
    private const string RestricaoDaVersaoVigente = "ex_versoes_do_perfil_vigente_unica";

    public VersaoVigenteUnicaNoBancoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O banco recusa a transação que termina com duas versões em vigor no mesmo perfil")]
    public async Task TransacaoComDuasVigentes_RecusadaNoCommit()
    {
        (Envio vigente, Envio pendente) = await PerfilComVigenteEPendenteAsync();

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(
            () => GravarNaTransacaoAsync((pendente.VersaoId, "vigente")));

        erro.SqlState.Should().Be(ViolacaoDeExclusao);
        erro.ConstraintName.Should().Be(RestricaoDaVersaoVigente);
        (await SituacaoDaVersaoNoBancoAsync(vigente.VersaoId)).Should().Be("vigente");
        (await SituacaoDaVersaoNoBancoAsync(pendente.VersaoId)).Should().Be("pendente");
    }

    [Fact(DisplayName = "O banco aceita a troca de versão em vigor gravada com a nova antes de a anterior ser encerrada")]
    public async Task TrocaGravadaNaOrdemInversa_AceitaNoCommit()
    {
        (Envio vigente, Envio pendente) = await PerfilComVigenteEPendenteAsync();

        await GravarNaTransacaoAsync((pendente.VersaoId, "vigente"), (vigente.VersaoId, "encerrada"));

        (await SituacaoDaVersaoNoBancoAsync(pendente.VersaoId)).Should().Be("vigente");
        (await SituacaoDaVersaoNoBancoAsync(vigente.VersaoId)).Should().Be("encerrada");
        (await QuantidadeDeVersoesVigentesAsync(vigente.PerfilId)).Should().Be(1);
    }

    private async Task<(Envio Vigente, Envio Pendente)> PerfilComVigenteEPendenteAsync()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio pendente = await CriarVersaoAsync(diogo, vigente.PerfilId, Nome(), Consultar, Sensivel);

        return (vigente, pendente);
    }

    private async Task GravarNaTransacaoAsync(params (Guid VersaoId, string Situacao)[] alteracoes)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        await using IDbContextTransaction transacao = await db.Database.BeginTransactionAsync();
        foreach ((Guid versaoId, string situacao) in alteracoes)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE autorizacao.versoes_do_perfil SET situacao = {situacao} WHERE id = {versaoId}");
        }

        await transacao.CommitAsync();
    }
}
