namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

/// <summary>
/// Peso e corte de uma área na gravação de Pesos por Área. O <c>Codigo</c> só identifica
/// a área que recebe os valores; o rótulo não faz parte do contrato de gravação e é
/// sempre posto pelo sistema.
/// </summary>
/// <remarks>
/// <c>Peso</c> e <c>Codigo</c> são anuláveis para o valor ausente chegar à validação de domínio e voltar
/// como erro do campo da área (<c>areas[i].peso</c>, <c>areas[i].codigo</c>), em vez de virar <c>0m</c> — um
/// peso válido — ou de derrubar a leitura do corpo. <c>Corte</c> é opcional: <see langword="null"/> é
/// "sem corte" (ADR-0125).
/// </remarks>
public sealed record PesoAreaEnemAreaCommand(
    string? Codigo,
    decimal? Peso,
    decimal? Corte = null);
