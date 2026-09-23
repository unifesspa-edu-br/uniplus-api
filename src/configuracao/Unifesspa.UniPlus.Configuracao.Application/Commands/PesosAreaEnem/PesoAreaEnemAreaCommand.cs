namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using System.Text.Json.Serialization;

/// <summary>
/// Peso e corte de uma área na gravação de Pesos por Área. O <c>Codigo</c> só identifica
/// a área que recebe os valores; o rótulo não faz parte do contrato de gravação e é
/// sempre posto pelo sistema.
/// </summary>
/// <remarks>
/// <c>Peso</c> é <c>[JsonRequired]</c>: como é <c>decimal</c> não-anulável, omiti-lo no
/// JSON faria o System.Text.Json construir o item com <c>0m</c> — um peso válido — em vez
/// de rejeitar o campo ausente. <c>Corte</c> é opcional: <see langword="null"/> é "sem
/// corte". <c>Codigo</c> é <c>string?</c> (ADR-0125) para o valor ausente chegar à
/// validação de domínio e voltar como erro do campo.
/// </remarks>
public sealed record PesoAreaEnemAreaCommand(
    string? Codigo,
    [property: JsonRequired] decimal Peso,
    decimal? Corte = null);
