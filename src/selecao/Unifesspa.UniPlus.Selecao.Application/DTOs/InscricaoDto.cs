namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed record InscricaoDto(
    Guid Id,
    Guid CandidatoId,
    Guid EditalId,
    string Modalidade,
    string Status,
    string? CodigoCursoPrimeiraOpcao,
    string? CodigoCursoSegundaOpcao,
    bool ListaEspera,
    string? NumeroInscricao,
    DateTimeOffset CriadoEm);
