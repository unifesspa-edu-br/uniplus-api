namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>O valor completo de um campo identificador de uma inscrição, revelado a quem tem a permissão de revelar.</summary>
/// <param name="FatoCodigo">O código do fato revelado.</param>
/// <param name="Valor">O valor completo, como o candidato o informou.</param>
public sealed record CampoReveladoDto(string FatoCodigo, string Valor);
