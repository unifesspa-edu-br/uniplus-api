namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Ciclo de vida da inscrição do candidato: nasce <see cref="Rascunho"/> ao iniciar e
/// passa a <see cref="Submetida"/> no envio. Uma correção dentro do período mantém a situação
/// <see cref="Submetida"/> — não existe estado para "corrigida". A inscrição enviada que o candidato
/// desfaz fica <see cref="Cancelada"/>, para sempre: ele se inscreve de novo, e a nova é outra inscrição.
/// </summary>
public enum SituacaoInscricao
{
    [CodigoDeContrato("RASCUNHO", "Rascunho", Descricao = "A inscrição está em preenchimento.")]
    Rascunho = 1,
    [CodigoDeContrato("SUBMETIDA", "Submetida", Descricao = "A inscrição foi enviada.")]
    Submetida = 2,
    [CodigoDeContrato("CANCELADA", "Cancelada", Descricao = "A inscrição enviada foi cancelada.")]
    Cancelada = 3,
}
