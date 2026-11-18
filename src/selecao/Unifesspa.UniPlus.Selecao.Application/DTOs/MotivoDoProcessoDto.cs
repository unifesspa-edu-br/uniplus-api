namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um motivo que o processo escolheu do cadastro (UNI-REQ-0285), com os valores copiados, na fase ou etapa em que foi
/// escolhido. A justificativa da inclusão depois da publicação é de uso interno da administração e só sai na leitura
/// autorizada da configuração, nunca ao candidato.
/// </summary>
/// <param name="MotivoDeDecisaoId">O motivo de origem no cadastro.</param>
/// <param name="Lugar">A fase ou a etapa em que o motivo foi escolhido.</param>
/// <param name="Vigencia">As versões do edital em que a escolha vale.</param>
public sealed record MotivoDoProcessoDto(
    Guid MotivoDeDecisaoId,
    LugarDoMotivoDto Lugar,
    string Tipo,
    string Codigo,
    string Descricao,
    string? Subclassificacao,
    string Sentido,
    VigenciaDoMotivoDto Vigencia,
    string? JustificativaDaInclusao);

/// <summary>O lugar da escolha: exatamente um dos dois é informado, a fase pela fase canônica de origem ou a etapa pela identidade.</summary>
public sealed record LugarDoMotivoDto(Guid? FaseCanonicaOrigemId, Guid? EtapaId);

/// <summary>As versões do edital em que a escolha vale; a saída é nula enquanto ela vale.</summary>
public sealed record VigenciaDoMotivoDto(int VersaoDoEditalDeEntrada, int? VersaoDoEditalDeSaida);
