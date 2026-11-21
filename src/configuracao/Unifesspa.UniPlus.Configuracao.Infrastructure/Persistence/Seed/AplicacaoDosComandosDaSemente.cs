namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Data.Common;
using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Executa os comandos da semente na transação do contexto e registra, uma vez por item, o termo, a versão ou o modelo
/// que o banco não gravou, com o código e o motivo. O registro nunca traz o texto do termo nem dado pessoal.
/// </summary>
internal static partial class AplicacaoDosComandosDaSemente
{
    private const string MotivoCodigoOcupado = "o código já está ocupado";
    private const string MotivoTermoAusente = "o termo não foi gravado";
    private const string MotivoTermoRemovido = "o termo foi removido";
    private const string MotivoNumeroDaVersaoOcupado = "o termo já tem uma versão com esse número";
    private const string MotivoCitacaoAusente = "um termo ou uma versão citada não existe, não é do termo, ou o termo foi removido";

    public static async Task ExecutarAsync(
        UniPlusDbContext contexto, IReadOnlyList<ComandoDaSemente> comandos, ILogger logger, CancellationToken cancellationToken)
    {
        IDbContextTransaction transacao = contexto.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Os comandos da semente rodam dentro da transação do contexto.");

        foreach (ComandoDaSemente comando in comandos)
        {
            await AplicarComandoAsync(contexto, transacao, comando, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AplicarComandoAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, ComandoDaSemente comando, ILogger logger, CancellationToken cancellationToken)
    {
        int gravadas = await ExecutarAsync(contexto, transacao, comando.Texto, cancellationToken).ConfigureAwait(false);
        if (gravadas > 0 || comando.Item is not { } naoGravado)
        {
            return;
        }

        string? motivo = await MotivoDoItemNaoGravadoAsync(contexto, transacao, naoGravado, cancellationToken).ConfigureAwait(false);
        if (motivo is not null)
        {
            LogItemNaoGravado(logger, naoGravado.Descricao, naoGravado.Codigo, motivo);
        }
    }

    // O comando não gravou nada, então o banco está como estava. O item que já está gravado é o caso normal de toda
    // reaplicação e não é registrado; só o item não gravado por outro motivo tem registro: o que impede o item de entrar agora.
    private static async Task<string?> MotivoDoItemNaoGravadoAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, ItemDaSemente item, CancellationToken cancellationToken)
    {
        string tabela = Tabela(item.Tipo);
        if (await ExisteAsync(contexto, transacao, $"{tabela} WHERE id = {SqlDaSemente.Uuid(item.Id)}", cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return item.Tipo switch
        {
            TipoDoItemDaSemente.VersaoDoTermo => await MotivoDaVersaoAsync(contexto, transacao, item, cancellationToken).ConfigureAwait(false),
            TipoDoItemDaSemente.Termo => MotivoCodigoOcupado,
            _ => await MotivoDoModeloAsync(contexto, transacao, item, cancellationToken).ConfigureAwait(false),
        };
    }

    // A versão só deixa de entrar por três condições: o termo dela não está gravado, o termo foi removido, ou o termo já tem
    // versão com o mesmo número.
    private static async Task<string> MotivoDaVersaoAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, ItemDaSemente versao, CancellationToken cancellationToken)
    {
        string termos = Tabela(TipoDoItemDaSemente.Termo);
        if (versao.TermoId is not { } termoId
            || !await ExisteAsync(contexto, transacao, $"{termos} WHERE id = {SqlDaSemente.Uuid(termoId)}", cancellationToken).ConfigureAwait(false))
        {
            return MotivoTermoAusente;
        }

        return await ExisteAsync(contexto, transacao, $"{termos} WHERE id = {SqlDaSemente.Uuid(termoId)} AND is_deleted", cancellationToken).ConfigureAwait(false)
            ? MotivoTermoRemovido
            : MotivoNumeroDaVersaoOcupado;
    }

    private static async Task<string> MotivoDoModeloAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, ItemDaSemente modelo, CancellationToken cancellationToken)
    {
        string origem = $"{Tabela(modelo.Tipo)} WHERE codigo = {SqlDaSemente.Texto(modelo.Codigo)}";
        return await ExisteAsync(contexto, transacao, origem, cancellationToken).ConfigureAwait(false)
            ? MotivoCodigoOcupado
            : MotivoCitacaoAusente;
    }

    private static string Tabela(TipoDoItemDaSemente tipo) => tipo switch
    {
        TipoDoItemDaSemente.Termo => "configuracao.termos_de_consentimento",
        TipoDoItemDaSemente.VersaoDoTermo => "configuracao.versoes_do_termo_consentimento",
        _ => "configuracao.modelos_formulario",
    };

    private static async Task<bool> ExisteAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, string origemEFiltro, CancellationToken cancellationToken)
    {
        DbCommand consulta = contexto.Database.GetDbConnection().CreateCommand();
        await using (consulta.ConfigureAwait(false))
        {
            consulta.CommandText = $"SELECT EXISTS (SELECT 1 FROM {origemEFiltro})";
            consulta.Transaction = transacao.GetDbTransaction();
            return Convert.ToBoolean(await consulta.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
    }

    // O comando vai como texto, sem formatação: o conteúdo dos modelos é JSON, e as chaves dele não são marcadores.
    private static async Task<int> ExecutarAsync(
        UniPlusDbContext contexto, IDbContextTransaction transacao, string comando, CancellationToken cancellationToken)
    {
        DbCommand execucao = contexto.Database.GetDbConnection().CreateCommand();
        await using (execucao.ConfigureAwait(false))
        {
            execucao.CommandText = comando;
            execucao.Transaction = transacao.GetDbTransaction();
            return await execucao.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "A semente não gravou o item {Tipo} {Codigo}: {Motivo}.")]
    private static partial void LogItemNaoGravado(ILogger logger, string tipo, string codigo, string motivo);
}
