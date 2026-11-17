namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O que o salvamento do rascunho devolve: a revisão que ficou, o ETag que a identifica e o diagnóstico do documento.</summary>
/// <param name="Id">O identificador do rascunho, que é o do processo que a publicação cria.</param>
/// <param name="Revisao">A revisão gravada: 1 na criação e mais um a cada substituição.</param>
/// <param name="Diagnosticos">As causas pelas quais o documento ainda não se publica, por ponteiro do documento. Vazio enquanto a composição não as emite.</param>
/// <param name="ETag">O ETag forte da revisão gravada, para a próxima mutação.</param>
public sealed record RascunhoSalvoDto(Guid Id, int Revisao, IReadOnlyList<FieldError> Diagnosticos, string ETag);
