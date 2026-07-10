namespace Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Aviso de numeração duplicada (AC4 da #799): há outro(s) ato(s) com a mesma
/// numeração <c>(orgao, serie, ano, numero)</c>. O número é declarado, não
/// gerado — colisão é aviso, jamais recusa; é o que pega o erro humano de reusar
/// um número já gasto.
/// </summary>
/// <remarks>
/// Diagnóstico do <b>estado atual</b>, computado na leitura — não é prova imutável
/// registrada no ato. Entre a consulta e um insert concorrente cabe uma corrida,
/// tolerada porque o aviso não bloqueia o registro; uma consulta posterior recomputa
/// e enxerga ambos os atos.
/// </remarks>
public sealed record AvisoNumeracao(
    string Codigo,
    string Mensagem,
    IReadOnlyList<Guid> AtosConflitantes);
