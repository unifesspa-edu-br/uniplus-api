namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Constraints do banco sobre os destinos da cascata de remanejamento — o caminho que o agregado
/// NUNCA toma (defesa em profundidade). <c>ConfiguracaoCascataRemanejamento.Criar</c> e
/// <c>DestinoRemanejamento.Criar</c> já recusam estas formas (OrdemDuplicadaNaOrigem,
/// DestinoDuplicado, OrdemInvalida, OrigemIgualAoDestino); os testes provam que o BANCO recusa
/// sozinho, mesmo se o domínio nunca as produzisse. O INSERT é cru, via <c>NpgsqlCommand</c>,
/// contornando deliberadamente a factory.
/// </summary>
[Collection(PublicacaoSnapshotPersistenciaColecao.Nome)]
public sealed class ConstraintsDosDestinosDaCascataTests
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";

    private readonly CenarioDeCascataPersistida _cenario;

    public ConstraintsDosDestinosDaCascataTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeCascataPersistida(fixture);
    }

    [Fact(DisplayName = "Índice único (cascata, origem, ordem) — o banco recusa duas linhas com a mesma posição na fila da mesma origem")]
    public async Task Destinos_MesmaOrigemEOrdem_BancoRejeitaComIndiceUnico()
    {
        Guid cascataId = await SemearCascataComUmDestinoAsync(nameof(Destinos_MesmaOrigemEOrdem_BancoRejeitaComIndiceUnico));

        await InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_Q");

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_PCD"));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_destinos_remanejamento_cascata_origem_ordem");
    }

    [Fact(DisplayName = "Índice único (cascata, origem, destino) — o banco recusa a mesma origem apontando duas vezes para o mesmo destino")]
    public async Task Destinos_MesmaOrigemEDestino_BancoRejeitaComIndiceUnico()
    {
        Guid cascataId = await SemearCascataComUmDestinoAsync(nameof(Destinos_MesmaOrigemEDestino_BancoRejeitaComIndiceUnico));

        await InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_Q");

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 2, "LB_Q"));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_destinos_remanejamento_cascata_origem_destino");
    }

    [Fact(DisplayName = "CHECK ordem >= 1 — o banco recusa uma posição de fila zero ou negativa")]
    public async Task Destinos_OrdemNaoPositiva_BancoRejeitaComCheck()
    {
        Guid cascataId = await SemearCascataComUmDestinoAsync(nameof(Destinos_OrdemNaoPositiva_BancoRejeitaComCheck));

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 0, "LB_Q"));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_destinos_remanejamento_ordem_positiva");
    }

    [Fact(DisplayName = "CHECK origem <> destino — o banco recusa uma modalidade remanejando para si mesma")]
    public async Task Destinos_OrigemIgualAoDestino_BancoRejeitaComCheck()
    {
        Guid cascataId = await SemearCascataComUmDestinoAsync(nameof(Destinos_OrigemIgualAoDestino_BancoRejeitaComCheck));

        PostgresException erro = await CapturarErroAsync(
            () => InserirDestinoCruAsync(cascataId, "LB_PPI", 1, "LB_PPI"));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_destinos_remanejamento_origem_diferente_destino");
    }

    /// <summary>
    /// Persiste um processo com uma cascata de UM destino — o mínimo que
    /// <c>ConfiguracaoCascataRemanejamento.Criar</c> aceita — e devolve o id da cascata para os
    /// INSERTs crus. Os testes então adicionam um SEGUNDO destino, cru, que colide ou viola.
    /// </summary>
    private async Task<Guid> SemearCascataComUmDestinoAsync(string nome)
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nome);
        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            CenarioDeCascataPersistida.Regra(RegraRemanejamentoCodigo.Cascata, "5"),
            "AC",
            [DestinoRemanejamento.Criar("LI_EP", 1, "AC").Value!]).Value!;
        processo.DefinirCascataRemanejamento(cascata, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        return cascata.Id;
    }

    /// <summary>INSERT cru na tabela de destinos — o caminho que a factory do domínio nunca toma.</summary>
    private async Task InserirDestinoCruAsync(Guid cascataId, string origem, int ordem, string destino)
    {
        await using NpgsqlConnection conexao = new(_cenario.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            """
            INSERT INTO selecao.destinos_remanejamento (
                id, configuracao_cascata_remanejamento_id, modalidade_origem_codigo, ordem, modalidade_destino_codigo,
                created_at)
            VALUES (@id, @cascataId, @origem, @ordem, @destino, now())
            """,
            conexao);
        comando.Parameters.AddWithValue("id", Guid.CreateVersion7());
        comando.Parameters.AddWithValue("cascataId", cascataId);
        comando.Parameters.AddWithValue("origem", origem);
        comando.Parameters.AddWithValue("ordem", ordem);
        comando.Parameters.AddWithValue("destino", destino);

        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<PostgresException> CapturarErroAsync(Func<Task> acao) =>
        (await acao.Should().ThrowAsync<PostgresException>()).Which;
}
