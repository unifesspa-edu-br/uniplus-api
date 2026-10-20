namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Ciclo de vida da inscrição do candidato (issue #1858): nasce <see cref="Rascunho"/> ao iniciar e
/// passa a <see cref="Submetida"/> no envio. Uma correção dentro do período mantém a situação
/// <see cref="Submetida"/> — não existe estado para "corrigida". A inscrição enviada que o candidato
/// desfaz fica <see cref="Cancelada"/>, para sempre: ele se inscreve de novo, e a nova é outra inscrição.
/// </summary>
public enum SituacaoInscricao
{
    Rascunho = 1,
    Submetida = 2,
    Cancelada = 3,
}
