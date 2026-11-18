namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Datas de criação e atualização do registro da inscrição.</summary>
public readonly record struct DatasDeGravacaoDaInscricao(DateTimeOffset CriadaEm, DateTimeOffset? AtualizadaEm);
