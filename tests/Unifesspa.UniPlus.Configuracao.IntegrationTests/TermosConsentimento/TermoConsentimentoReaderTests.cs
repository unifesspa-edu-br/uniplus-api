namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O leitor cross-módulo das versões de termo (UNI-REQ-0086), contra Postgres real: é por ele que
/// a Seleção congela o conteúdo do termo que o formulário exige.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TermoConsentimentoReaderTests
{
    private const string AdminA = "admin-a";
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public TermoConsentimentoReaderTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "As versões pedidas voltam com o conteúdo e a forma de aceite em token; a de termo removido, não")]
    public async Task ListarVersoesAsync_DevolveConteudoEOmiteTermoRemovido()
    {
        (TermoConsentimento vivo, TermoConsentimentoVersao versaoViva) = Promovido("Autorização de consulta", "A_DEFINIR");
        (TermoConsentimento removido, TermoConsentimentoVersao versaoRemovida) = Promovido("Termo retirado", "REGISTRO_DIGITAL_SEM_LOG_IP");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.AddRange(vivo, removido);
            await ctx.SaveChangesAsync();

            removido.MarkAsDeleted(AdminA, Agora);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TermoConsentimentoReader reader = new(readCtx);

        IReadOnlyList<VersaoTermoConsentimentoView> versoes =
            await reader.ListarVersoesAsync([versaoViva.Id, versaoRemovida.Id], CancellationToken.None);

        versoes.Should().ContainSingle().Which.Should().BeEquivalentTo(new VersaoTermoConsentimentoView(
            vivo.Id, versaoViva.Id, "Autorização de consulta", "Texto do termo", "Lei 13.709/2018", "A_DEFINIR", versaoViva.Hash));
    }

    private static (TermoConsentimento Termo, TermoConsentimentoVersao Versao) Promovido(string nome, string formaAceite)
    {
        TermoConsentimento termo = TermoConsentimento.Criar(nome, "Texto do termo", "Lei 13.709/2018", formaAceite).Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);
        return (termo, termo.Promover("usuario.revisor", Agora).Value!);
    }
}
