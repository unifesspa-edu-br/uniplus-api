namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Data.Common;
using System.Globalization;

/// <summary>
/// O que a transação da retificação gravou, visto de dentro dela, antes da confirmação.
/// </summary>
/// <param name="AtoDaVersaoDois">O ato que criou a versão 2.</param>
/// <param name="VigenciaDaVersaoDois">A vigência da versão 2.</param>
/// <param name="AtoDoProcessoGravado">O registro do ato do processo, na Seleção, existe.</param>
/// <param name="AtoNormativoGravado">O ato no registro central de Publicações existe.</param>
/// <param name="NumeroDaVersaoDivulgada">O número da versão que a divulgação do certame já mostra.</param>
/// <param name="TransacoesDasLinhasGravadas">O número de transações distintas que gravaram as quatro linhas da retificação; zero quando falta alguma das quatro.</param>
/// <param name="MensagensPendentesDeEnvio">As mensagens pendentes de envio (nas filas duráveis de entrada e de saída) que citam o ato da versão 2.</param>
/// <param name="PidDaConexao">O processo do banco que atende a conexão da transação: é quem segura a trava do processo seletivo.</param>
internal sealed record ConteudoDaTransacaoDaRetificacao(
    Guid AtoDaVersaoDois,
    DateTimeOffset VigenciaDaVersaoDois,
    bool AtoDoProcessoGravado,
    bool AtoNormativoGravado,
    int NumeroDaVersaoDivulgada,
    int TransacoesDasLinhasGravadas,
    int MensagensPendentesDeEnvio,
    int PidDaConexao);

/// <summary>
/// Lê, pela conexão e dentro da transação, o que a retificação gravou. A consulta devolve nulo para a transação que não
/// enxerga a versão 2 do processo: só a transação da retificação a enxerga antes da confirmação.
/// </summary>
internal static class LeituraDaTransacaoDaRetificacao
{
    private const string VersaoDois = "SELECT ato_criador_id, vigente_a_partir_de FROM selecao.versoes_configuracao WHERE processo_seletivo_id = @valor AND numero_versao = 2";
    private const string AtoDoProcesso = "SELECT count(*) FROM selecao.atos_do_processo WHERE id = @valor";
    private const string AtoNormativo = "SELECT count(*) FROM publicacoes.atos_normativos WHERE id = @valor";
    private const string NumeroDivulgado = "SELECT numero_versao FROM selecao.certames_divulgados WHERE id = @valor";
    private const string PidDaConexaoNoBanco = "SELECT pg_backend_pid()";
    private const string TransacoesDasLinhas = """
        SELECT CASE WHEN count(*) = 4 THEN count(DISTINCT transacao) ELSE 0 END FROM (
            SELECT xmin::text AS transacao FROM selecao.versoes_configuracao WHERE ato_criador_id = @valor
            UNION ALL SELECT xmin::text FROM selecao.atos_do_processo WHERE id = @valor
            UNION ALL SELECT xmin::text FROM publicacoes.atos_normativos WHERE id = @valor
            UNION ALL SELECT xmin::text FROM selecao.certames_divulgados WHERE id = @processoId
        ) AS linhas
        """;
    private const string MensagensPendentes = """
        SELECT (SELECT count(*) FROM wolverine.wolverine_outgoing_envelopes WHERE position(convert_to(@valor, 'UTF8') in body) > 0)
             + (SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE position(convert_to(@valor, 'UTF8') in body) > 0)
        """;

    public static async Task<ConteudoDaTransacaoDaRetificacao?> LerAsync(
        DbTransaction transacao, Guid processoId, CancellationToken cancellationToken)
    {
        (Guid Ato, DateTimeOffset Vigencia)? versao = await LerAVersaoDoisAsync(transacao, processoId, cancellationToken);

        return versao is null ? null : await LerOConteudoAsync(transacao, processoId, versao.Value, cancellationToken);
    }

    private static async Task<ConteudoDaTransacaoDaRetificacao> LerOConteudoAsync(
        DbTransaction transacao, Guid processoId, (Guid Ato, DateTimeOffset Vigencia) versao, CancellationToken cancellationToken) =>
        new(
            versao.Ato,
            versao.Vigencia,
            await ContarAsync(transacao, AtoDoProcesso, versao.Ato, cancellationToken) == 1,
            await ContarAsync(transacao, AtoNormativo, versao.Ato, cancellationToken) == 1,
            await ContarAsync(transacao, NumeroDivulgado, processoId, cancellationToken),
            await ContarAsync(transacao, TransacoesDasLinhas, versao.Ato, cancellationToken, processoId),
            await ContarAsync(transacao, MensagensPendentes, versao.Ato.ToString("D"), cancellationToken),
            await ContarAsync(transacao, PidDaConexaoNoBanco, processoId, cancellationToken));

    private static async Task<(Guid Ato, DateTimeOffset Vigencia)?> LerAVersaoDoisAsync(
        DbTransaction transacao, Guid processoId, CancellationToken cancellationToken)
    {
        await using DbCommand comando = Comando(transacao, VersaoDois, processoId);
        await using DbDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        return await leitor.ReadAsync(cancellationToken)
            ? (leitor.GetGuid(0), leitor.GetFieldValue<DateTimeOffset>(1))
            : null;
    }

    private static async Task<int> ContarAsync(DbTransaction transacao, string consulta, object valor, CancellationToken cancellationToken, Guid? processoId = null)
    {
        await using DbCommand comando = Comando(transacao, consulta, valor, processoId);

        return Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static DbCommand Comando(DbTransaction transacao, string consulta, object valor, Guid? processoId = null)
    {
        DbCommand comando = transacao.Connection!.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = consulta;
        AdicionarParametro(comando, "valor", valor);
        if (processoId is not null)
        {
            AdicionarParametro(comando, "processoId", processoId.Value);
        }

        return comando;
    }

    private static void AdicionarParametro(DbCommand comando, string nome, object valor)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
