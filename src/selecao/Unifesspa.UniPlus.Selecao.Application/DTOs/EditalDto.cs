namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed record EditalDto(
    Guid Id,
    string NumeroEdital,
    string Titulo,
    string TipoProcesso,
    string Status,
    int MaximoOpcoesCurso,
    bool BonusRegionalHabilitado,
    DateTimeOffset CriadoEm);
