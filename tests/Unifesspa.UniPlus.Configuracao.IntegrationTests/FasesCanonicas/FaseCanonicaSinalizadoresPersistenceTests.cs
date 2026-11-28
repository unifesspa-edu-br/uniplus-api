namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Os sinalizadores da fase canônica no banco: as cinco colunas da linha de <c>tipos_de_fase</c>
/// que o valor <see cref="SinalizadoresDaFase"/> grava e lê, e os dois CHECKs que as cruzam com o
/// código e entre si.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FaseCanonicaSinalizadoresPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FaseCanonicaSinalizadoresPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "CHECK de banco amarra a marca de solicitação de isenção à fase de isenção via SQL cru")]
    [InlineData("SOLICITACAO_ISENCAO", false)]
    [InlineData("HABILITACAO", true)]
    public async Task Check_RejeitaMarcaDeIsencaoDivergenteDoCodigoViaSqlCru(string codigo, bool coletaSolicitacaoIsencao)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await InserirViaSqlCruAsync(
            ctx, codigo, coletaInscricao: false, coletaSolicitacaoIsencao);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_tipos_de_fase_solicitacao_isencao");
    }

    [Fact(DisplayName = "CHECK de banco recusa coletar inscrição e solicitação de isenção na mesma fase via SQL cru")]
    public async Task Check_RejeitaInscricaoEIsencaoJuntasViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await InserirViaSqlCruAsync(
            ctx, "SOLICITACAO_ISENCAO", coletaInscricao: true, coletaSolicitacaoIsencao: true);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_tipos_de_fase_inscricao_e_isencao_exclusivas");
    }

    [Theory(DisplayName = "Os sinalizadores gravados voltam iguais na leitura, uma marca por fase")]
    [InlineData("AVALIACAO", true, false, false)]
    [InlineData("HOMOLOGACAO_INSCRICOES", false, true, false)]
    [InlineData("RESULTADO_FINAL", false, false, true)]
    public async Task Sinalizadores_GravadosVoltamIguaisNaLeitura(
        string codigo, bool agrupaEtapas, bool permiteComplementacao, bool coletaInscricao)
    {
        await LiberarSlotAsync(codigo);
        SinalizadoresInformados sinalizadores = new(agrupaEtapas, permiteComplementacao, coletaInscricao, ColetaSolicitacaoIsencao: false, OrganizadaEmChamadas: false);
        FaseCanonica fase = FaseCanonica.Criar(codigo, "Fase", null, null, "NO_EDITAL", sinalizadores).Value!;
        await GravarComoAsync("admin-a", fase);

        FaseCanonica lida = await LerAsync(fase.Id);

        lida.Sinalizadores.Should().Be(fase.Sinalizadores);
    }

    [Theory(DisplayName = "A marca de organizada em chamadas grava na coluna própria e volta igual na leitura")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrganizadaEmChamadas_GravaEVolta(bool organizada)
    {
        await LiberarSlotAsync("HABILITACAO");
        SinalizadoresInformados sinalizadores = new(false, false, false, false, organizada);
        FaseCanonica fase = FaseCanonica.Criar("HABILITACAO", "Habilitação", null, null, "NO_EDITAL", sinalizadores).Value!;
        await GravarComoAsync("admin-a", fase);

        FaseCanonica lida = await LerAsync(fase.Id);

        lida.Sinalizadores.OrganizadaEmChamadas.Should().Be(organizada);
    }

    [Fact(DisplayName = "Atualizar troca os sinalizadores gravados e a leitura devolve os novos")]
    public async Task Atualizar_TrocaOsSinalizadoresGravados()
    {
        await LiberarSlotAsync("HOMOLOGACAO_INSCRICOES");
        FaseCanonica fase = FaseCanonica.Criar(
            "HOMOLOGACAO_INSCRICOES", "Homologação das inscrições", null, null, "NO_EDITAL", new SinalizadoresInformados(false, false, false, false, false)).Value!;
        await GravarComoAsync("admin-a", fase);

        await AtualizarComoAsync("admin-b", fase.Id, new SinalizadoresInformados(false, true, false, false, false));

        FaseCanonica lida = await LerAsync(fase.Id);
        lida.Sinalizadores.PermiteComplementacao.Should().BeTrue();
        lida.UpdatedBy.Should().Be("admin-b");
    }

    private static Task<int> InserirViaSqlCruAsync(
        UniPlusDbContext ctx, string codigo, bool coletaInscricao, bool coletaSolicitacaoIsencao) =>
        ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_fase (id, codigo, nome, coleta_inscricao, coleta_solicitacao_isencao, origem_data, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {codigo}, {"Fase"}, {coletaInscricao}, {coletaSolicitacaoIsencao}, {"NO_EDITAL"}, {DateTimeOffset.UtcNow}, {false})");

    private async Task GravarComoAsync(string usuario, FaseCanonica fase)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(usuario);
        escrita.FasesCanonicas.Add(fase);
        await escrita.SaveChangesAsync();
    }

    private async Task AtualizarComoAsync(string usuario, Guid id, SinalizadoresInformados sinalizadores)
    {
        await using UniPlusDbContext edicao = _fixture.CreateDbContext(usuario);
        FaseCanonica rastreada = await edicao.FasesCanonicas.SingleAsync(f => f.Id == id);
        rastreada.Atualizar(rastreada.Nome, null, null, "NO_EDITAL", sinalizadores).IsSuccess.Should().BeTrue();
        await edicao.SaveChangesAsync();
    }

    private async Task<FaseCanonica> LerAsync(Guid id)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await leitura.FasesCanonicas.AsNoTracking().SingleAsync(f => f.Id == id);
    }

    // O seed, ou outro teste da coleção, já ocupa o código do vocabulário: o índice único parcial só libera o
    // slot com a linha viva do código fora do caminho.
    private Task LiberarSlotAsync(string codigo) =>
        ApoioDosEndpointsDeFaseCanonica.LiberarSlotDoCodigoAsync(_fixture.ConnectionString, codigo);
}
