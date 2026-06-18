namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using System.Linq;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Trilha de uma execução do ETL DNE (Story #674): qual versão (<c>AAAAMM</c>) foi
/// carregada, quando, por quem e com qual resultado. É dado operacional do módulo
/// (não reference data), <see cref="EntityBase"/> puro — sem soft-delete; cada
/// carga é um fato append-only. O relatório por tabela (contadores, sem PII) é
/// persistido como JSON ao concluir.
/// </summary>
/// <remarks>
/// A concorrência (no máximo uma carga em andamento) é garantida no banco por um
/// índice único parcial sobre <see cref="Status"/> = <see cref="StatusImportacao.EmAndamento"/>;
/// o segundo disparo simultâneo colide na UNIQUE e vira <c>409 Conflict</c>.
/// </remarks>
public sealed class GeoImportacaoExecucao : EntityBase
{
    /// <summary>Release DNE (AAAAMM) que esta execução aplica.</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary>Estado atual da carga.</summary>
    public StatusImportacao Status { get; private set; }

    /// <summary>Instante de início (relógio do orquestrador via <c>TimeProvider</c>).</summary>
    public DateTimeOffset IniciadoEm { get; private set; }

    /// <summary>Instante de término (conclusão ou falha); <see langword="null"/> enquanto em andamento.</summary>
    public DateTimeOffset? ConcluidoEm { get; private set; }

    /// <summary>Subject do administrador que disparou, ou <c>seed</c> no boot de desenvolvimento. Nunca PII de candidato.</summary>
    public string DisparadoPor { get; private set; } = string.Empty;

    /// <summary>Relatório por tabela (contadores, sem PII) serializado em JSON; preenchido ao terminar.</summary>
    public string? RelatorioJson { get; private set; }

    /// <summary>Resumo ou motivo da falha (sem PII).</summary>
    public string? Mensagem { get; private set; }

    // Construtor privado para materialização do EF Core.
    private GeoImportacaoExecucao()
    {
    }

    /// <summary>
    /// Abre uma execução no estado <see cref="StatusImportacao.EmAndamento"/>. Valida o
    /// formato da versão (AAAAMM) e a identificação do disparador antes de criar.
    /// </summary>
    /// <param name="versao">Release DNE no formato AAAAMM (6 dígitos, mês 01–12).</param>
    /// <param name="disparadoPor">Subject do administrador ou <c>seed</c>.</param>
    /// <param name="iniciadoEm">Instante de início (relógio injetado).</param>
    public static Result<GeoImportacaoExecucao> Iniciar(string versao, string disparadoPor, DateTimeOffset iniciadoEm)
    {
        // Diferente das entidades reference data (criadas por ETL controlado), esta nasce de
        // entrada da API — versão/disparador ausentes são erro de validação (→ 422), não de
        // schema: null cai no IsNullOrWhiteSpace abaixo e vira Failure, sem lançar.
        if (string.IsNullOrWhiteSpace(versao))
        {
            return Result<GeoImportacaoExecucao>.Failure(new DomainError(
                GeoImportacaoErrorCodes.VersaoObrigatoria,
                "Versão (AAAAMM) do dataset é obrigatória."));
        }

        string versaoNormalizada = versao.Trim();
        if (!EhVersaoValida(versaoNormalizada))
        {
            return Result<GeoImportacaoExecucao>.Failure(new DomainError(
                GeoImportacaoErrorCodes.VersaoFormatoInvalido,
                "Versão do dataset deve estar no formato AAAAMM (6 dígitos, mês 01–12)."));
        }

        if (string.IsNullOrWhiteSpace(disparadoPor))
        {
            return Result<GeoImportacaoExecucao>.Failure(new DomainError(
                GeoImportacaoErrorCodes.DisparadoPorObrigatorio,
                "A identificação de quem disparou a carga é obrigatória."));
        }

        var execucao = new GeoImportacaoExecucao
        {
            VersaoDataset = versaoNormalizada,
            DisparadoPor = disparadoPor.Trim(),
            Status = StatusImportacao.EmAndamento,
            IniciadoEm = iniciadoEm,
        };

        return Result<GeoImportacaoExecucao>.Success(execucao);
    }

    /// <summary>
    /// Marca a execução como concluída com sucesso, gravando o relatório. Só é válido
    /// a partir de <see cref="StatusImportacao.EmAndamento"/> (idempotência da transição).
    /// </summary>
    public Result Concluir(DateTimeOffset concluidoEm, string relatorioJson, string? mensagem)
    {
        ArgumentNullException.ThrowIfNull(relatorioJson);

        if (Status != StatusImportacao.EmAndamento)
        {
            return TransicaoInvalida();
        }

        Status = StatusImportacao.Concluida;
        ConcluidoEm = concluidoEm;
        RelatorioJson = relatorioJson;
        Mensagem = mensagem;
        return Result.Success();
    }

    /// <summary>
    /// Marca a execução como falha, registrando o motivo (sem PII) e, quando houver,
    /// o relatório parcial. Só é válido a partir de <see cref="StatusImportacao.EmAndamento"/>.
    /// </summary>
    public Result Falhar(DateTimeOffset concluidoEm, string mensagem, string? relatorioJson = null)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        if (Status != StatusImportacao.EmAndamento)
        {
            return TransicaoInvalida();
        }

        Status = StatusImportacao.Falhou;
        ConcluidoEm = concluidoEm;
        Mensagem = mensagem;
        RelatorioJson = relatorioJson;
        return Result.Success();
    }

    private static Result TransicaoInvalida() =>
        Result.Failure(new DomainError(
            GeoImportacaoErrorCodes.TransicaoInvalida,
            "A execução não está em andamento; transição de estado inválida."));

    // AAAAMM: 6 dígitos, ano qualquer, mês 01–12. A comparação de stale entre
    // versões é lexicográfica (válida porque o comprimento é fixo).
    private static bool EhVersaoValida(string versao)
    {
        if (versao.Length != 6 || !versao.All(char.IsAsciiDigit))
        {
            return false;
        }

        int mes = ((versao[4] - '0') * 10) + (versao[5] - '0');
        return mes is >= 1 and <= 12;
    }
}
