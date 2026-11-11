namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um motivo que o processo escolheu do cadastro (UNI-REQ-0285), com os valores copiados. A
/// justificativa da inclusão depois da publicação é de uso interno da administração e só sai na
/// leitura autorizada da configuração, nunca ao candidato.
/// </summary>
/// <param name="MotivoDeDecisaoId">O motivo de origem no cadastro.</param>
/// <param name="Congelado">Verdadeiro quando já consta de versão publicada: não muda nem sai mais.</param>
public sealed record MotivoDoProcessoDto(
    Guid MotivoDeDecisaoId,
    string Tipo,
    string Codigo,
    string Descricao,
    string? Subclassificacao,
    string Sentido,
    bool Congelado,
    string? JustificativaDaInclusao);
