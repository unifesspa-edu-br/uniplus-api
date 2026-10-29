namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>O CPF completo de uma inscrição, revelado a quem tem a permissão própria.</summary>
/// <param name="CpfCompleto">O CPF com a pontuação usual, <c>999.999.999-99</c>.</param>
public sealed record CpfReveladoDto(string CpfCompleto);
