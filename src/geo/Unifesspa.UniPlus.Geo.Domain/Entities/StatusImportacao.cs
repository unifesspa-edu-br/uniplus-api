namespace Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Estado de uma execução do ETL DNE (Story #674). O valor inteiro é estável e
/// faz parte do contrato de persistência: <see cref="EmAndamento"/> é
/// <strong>0</strong> porque o índice único parcial de concorrência filtra por
/// <c>status = 0</c> (no máximo uma carga em andamento) — não reordenar.
/// </summary>
public enum StatusImportacao
{
    /// <summary>Carga em curso. No máximo uma linha pode estar neste estado (índice único parcial).</summary>
    EmAndamento = 0,

    /// <summary>Carga concluída com sucesso; o relatório está persistido.</summary>
    Concluida = 1,

    /// <summary>Carga abortada por falha de infraestrutura ou abandonada por reinício do serviço.</summary>
    Falhou = 2,
}
