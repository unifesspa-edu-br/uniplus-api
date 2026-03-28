namespace Unifesspa.UniPlus.Ingresso.Application.DTOs;

public sealed record MatriculaDto(
    Guid Id,
    Guid ConvocacaoId,
    Guid CandidatoId,
    string Status,
    string CodigoCurso,
    string? Observacoes,
    DateTimeOffset CriadoEm);
