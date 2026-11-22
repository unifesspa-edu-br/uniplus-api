namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Apresentação considerada na avaliação de uma exigência documental.</summary>
/// <param name="Id">Identificador da apresentação.</param>
/// <param name="ChaveDistincao">Identificação da apresentação quando há critério de distinção.</param>
/// <param name="OcorrenciaId">Registro do grupo do formulário a que a apresentação pertence, quando houver repetição.</param>
public sealed record ApresentacaoDocumento(Guid Id, string? ChaveDistincao = null, string? OcorrenciaId = null);
