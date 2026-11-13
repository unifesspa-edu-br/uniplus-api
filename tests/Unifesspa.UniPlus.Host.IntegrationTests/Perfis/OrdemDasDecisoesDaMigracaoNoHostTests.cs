namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// As decisões da migração sobre a mesma concessão, gravadas no mesmo microssegundo, saem na trilha na ordem em que foram
/// registradas, mesmo quando a registrada por último tem o identificador menor.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class OrdemDasDecisoesDaMigracaoNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private const string Reverter = """
        UPDATE autorizacao.decisoes_da_migracao
        SET id = CASE WHEN ordem_de_registro = (SELECT min(ordem_de_registro) FROM autorizacao.decisoes_da_migracao WHERE concessao_id = {0})
                      THEN 'ffffffff-ffff-7fff-bfff-ffffffffffff'::uuid ELSE '00000000-0000-7000-8000-000000000000'::uuid END,
            instante = {1}
        WHERE concessao_id = {0}
        """;

    public OrdemDasDecisoesDaMigracaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "Com o mesmo instante, as decisões da migração saem na trilha pela ordem de registro, e não pelo identificador")]
    public async Task MesmoInstante_PelaOrdemDeRegistro()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        await DecidirEmDuasEtapasAsync(cenario.PerfilId, concessao);
        await ExecutarSqlAsync(Reverter, concessao, new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
        using HttpClient diogo = await GestorAsync();

        JsonElement trilha = await LerAsync(diogo, $"/api/autorizacao/concessoes/{concessao}/trilha");

        trilha.EnumerateArray().Where(static e => e.GetProperty("tipo").GetString() == "migracao_migrar")
            .Select(static e => e.GetProperty("autor").GetProperty("sub").GetString())
            .Should().Equal(Isabella.Sub.ToString(), Marlene.Sub.ToString());
    }

    private async Task DecidirEmDuasEtapasAsync(Guid perfilId, Guid concessao)
    {
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(gestao, perfilId, [Decisao(concessao, "migrar")]);
        (await CorpoAsync(lote, HttpStatusCode.OK)).Should().NotBeNull();
        using HttpResponseMessage etapa = await EnviarAsync(
            protecao, HttpMethod.Post, RotaDaJanela(perfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "migrar") } });
        (await CorpoAsync(etapa, HttpStatusCode.OK)).Should().NotBeNull();
    }
}
