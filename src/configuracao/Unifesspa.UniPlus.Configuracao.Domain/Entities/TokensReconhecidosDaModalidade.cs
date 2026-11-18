namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// Quais tokens dos quais uma invariante cruzada depende foram reconhecidos: sobre um token inválido a
/// comparação usaria o valor default do enum e reportaria uma incoerência que o autor não cometeu.
/// </summary>
internal sealed record TokensReconhecidosDaModalidade(bool Natureza, bool Composicao, bool Regra);
