namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RetencaoDoModeloDeDocumentoConfirmadoTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string Restricao = "ck_modelos_de_documento_confirmado_retido";
    private static readonly string HashFixo = new('a', 64);
    private static readonly TimeProvider Relogio = new RelogioFixo(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ProcessoSeletivoDbFixture _fixture;

    public RetencaoDoModeloDeDocumentoConfirmadoTests(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Delete_ModeloConfirmadoDeProcessoExistente_Recusado()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: true, criarProcesso: true);
        await ConferirRecusaAsync("DELETE FROM selecao.modelos_de_documento WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ModeloConfirmadoDeProcessoExcluidoLogicamente_Recusado()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: true, criarProcesso: true);
        ModeloDeDocumento modelo = (await ObterModeloAsync(modeloId))!;
        await ExecutarAsync("UPDATE selecao.processos_seletivos SET is_deleted = true WHERE id = @id", modelo.ProcessoSeletivoId);
        await ConferirRecusaAsync("DELETE FROM selecao.modelos_de_documento WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ModeloConfirmadoSemLinhaDeProcesso_Aceito()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: true, criarProcesso: false);
        await ExecutarAsync("DELETE FROM selecao.modelos_de_documento WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId)).Should().BeNull();
    }

    [Fact]
    public async Task Update_DesconfirmarModelo_Recusado()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: true, criarProcesso: true);
        await ConferirRecusaAsync("UPDATE selecao.modelos_de_documento SET situacao = 'PENDENTE' WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId))!.Situacao.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    [Fact]
    public async Task Update_TrocarProcessoDeModeloConfirmado_Recusado()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: true, criarProcesso: true);
        Guid processoId = (await ObterModeloAsync(modeloId))!.ProcessoSeletivoId;
        await ConferirRecusaAsync("UPDATE selecao.modelos_de_documento SET processo_seletivo_id = gen_random_uuid() WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId))!.ProcessoSeletivoId.Should().Be(processoId);
    }

    [Fact]
    public async Task Truncate_TabelaDeModelos_Recusado()
    {
        await SemearModeloAsync(confirmado: true, criarProcesso: true);
        await ConferirRecusaAsync("TRUNCATE TABLE selecao.modelos_de_documento CASCADE", null);
    }

    [Fact]
    public async Task Delete_ModeloPendente_Aceito()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: false, criarProcesso: true);
        await ExecutarAsync("DELETE FROM selecao.modelos_de_documento WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId)).Should().BeNull();
    }

    [Fact]
    public async Task Update_ConfirmarModeloPendente_Aceito()
    {
        Guid modeloId = await SemearModeloAsync(confirmado: false, criarProcesso: true);
        await ExecutarAsync("UPDATE selecao.modelos_de_documento SET situacao = 'CONFIRMADO' WHERE id = @id", modeloId);
        (await ObterModeloAsync(modeloId))!.Situacao.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    private async Task<Guid> SemearModeloAsync(bool confirmado, bool criarProcesso)
    {
        Guid processoId = criarProcesso ? await SemearProcessoAsync() : Guid.NewGuid();
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(processoId, "modelo.odt", "ODT", Relogio, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            modelo.Confirmar(1024, HashFixo, Relogio).IsSuccess.Should().BeTrue();
        }
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await contexto.ModelosDeDocumento.AddAsync(modelo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
        return modelo.Id;
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        ProcessoSeletivo processo = CriarProcesso();
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await contexto.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
        return processo.Id;
    }

    private static ProcessoSeletivo CriarProcesso() => ProcessoSeletivo.Criar(
        $"Retenção {Guid.CreateVersion7()}", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria,
        Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private async Task<ModeloDeDocumento?> ObterModeloAsync(Guid modeloId)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        return await contexto.ModelosDeDocumento.FindAsync(modeloId);
    }

    private async Task ConferirRecusaAsync(string sql, Guid? id)
    {
        Func<Task> executar = () => ExecutarAsync(sql, id);
        PostgresException erro = (await executar.Should().ThrowAsync<PostgresException>()).Which;
        erro.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        erro.ConstraintName.Should().Be(Restricao);
        erro.TableName.Should().Be("modelos_de_documento");
        erro.SchemaName.Should().Be("selecao");
    }

    private async Task ExecutarAsync(string sql, Guid? id)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        if (id is { } identificador)
        {
            comando.Parameters.AddWithValue("id", identificador);
        }
        await comando.ExecuteNonQueryAsync();
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
