namespace Unifesspa.UniPlus.Ingresso.Application.DTOs;

public sealed record ChamadaDto(
    Guid Id,
    Guid EditalId,
    int Numero,
    string Status,
    DateTimeOffset DataPublicacao,
    DateTimeOffset PrazoManifestacao,
    DateTimeOffset CriadoEm);
