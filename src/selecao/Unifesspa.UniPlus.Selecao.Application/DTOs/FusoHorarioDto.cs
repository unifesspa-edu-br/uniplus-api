namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.ValueObjects;

/// <summary>
/// A zona em que o certame conta o dia civil: o identificador IANA, para converter instantes, e o
/// nome com que o candidato a lê em "horário de …".
/// </summary>
/// <remarks>
/// O nome viaja com o identificador porque é do domínio: sem ele, quem apresenta uma data ao
/// candidato teria de manter o próprio mapa de zonas, ou cair no fuso do navegador.
/// </remarks>
public sealed record FusoHorarioDto(string Id, string Nome)
{
    /// <summary>A zona institucional, a única em que os certames correm (UNI-REQ-0111).</summary>
    public static FusoHorarioDto Institucional { get; } = new(FusoInstitucional.ZoneId, FusoInstitucional.Nome);
}
