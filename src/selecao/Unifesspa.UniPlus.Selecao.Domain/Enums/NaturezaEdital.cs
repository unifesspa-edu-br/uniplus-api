namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Natureza do <see cref="Entities.Edital"/> emitido pelo ato de publicação do
/// <see cref="Entities.ProcessoSeletivo"/> (RN08, ADR-0101): <see cref="Abertura"/>
/// é a primeira publicação do processo; <see cref="Retificacao"/> (T5, #786)
/// é uma mudança pós-publicação, sempre vinculada ao Edital retificado e com
/// motivo obrigatório.
/// </summary>
public enum NaturezaEdital
{
    Nenhuma = 0,
    Abertura = 1,
    Retificacao = 2
}
