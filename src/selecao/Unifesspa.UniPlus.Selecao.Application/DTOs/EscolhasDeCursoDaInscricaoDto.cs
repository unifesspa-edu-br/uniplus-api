namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>As ofertas escolhidas e disponíveis na versão da inscrição.</summary>
public sealed record EscolhasDeCursoDaInscricaoDto(
    Guid? OfertaCursoId,
    Guid? OfertaCursoSegundaOpcaoId,
    int NumeroDeOpcoesDeCurso,
    IReadOnlyList<OfertaDeCursoCertameDto> Ofertas);
