namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Uma conexão à parte que abre a transação e retém a linha do processo, como outra operação em andamento. Enquanto ela
/// está aberta, as demais operações sobre o processo esperam na fila; ao confirmar ou desfazer, a trava se libera.
/// </summary>
internal sealed class ConexaoQueTravaOProcesso : IAsyncDisposable
{
    private readonly NpgsqlConnection _conexao;
    private readonly NpgsqlTransaction _transacao;

    private ConexaoQueTravaOProcesso(NpgsqlConnection conexao, NpgsqlTransaction transacao)
    {
        _conexao = conexao;
        _transacao = transacao;
    }

    internal static async Task<ConexaoQueTravaOProcesso> TravarAsync(IServiceProvider servicos, Guid processoId)
    {
        NpgsqlConnection conexao = new(StringDeConexao(servicos));
        await conexao.OpenAsync();
        NpgsqlTransaction transacao = await conexao.BeginTransactionAsync();
        await using NpgsqlCommand trava = new("SELECT 1 FROM selecao.processos_seletivos WHERE id = @id FOR UPDATE", conexao, transacao);
        trava.Parameters.AddWithValue("id", processoId);
        await trava.ExecuteNonQueryAsync();
        return new ConexaoQueTravaOProcesso(conexao, transacao);
    }

    internal Task ConfirmarAsync() => _transacao.CommitAsync();

    internal Task DesfazerAsync() => _transacao.RollbackAsync();

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await _conexao.DisposeAsync();
    }

    private static string StringDeConexao(IServiceProvider servicos)
    {
        using IServiceScope escopo = servicos.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.GetConnectionString()!;
    }
}
