namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// Quais códigos dos quais uma invariante cruzada depende foram reconhecidos: sobre um código inválido a
/// comparação usaria o valor default do enum e reportaria uma incoerência que o autor não cometeu.
/// </summary>
internal sealed record CodigosReconhecidosDaModalidade(bool Natureza, bool Composicao, bool Regra);
