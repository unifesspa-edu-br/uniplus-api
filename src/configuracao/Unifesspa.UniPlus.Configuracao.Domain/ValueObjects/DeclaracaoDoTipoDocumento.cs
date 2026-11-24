namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O que a gestão declara de um tipo de documento, em criação e em edição, ainda como chega: qualquer campo pode estar
/// ausente, em branco ou fora do formato, e a conferência acumula todas as recusas.
/// </summary>
/// <param name="Codigo">Código do tipo, obrigatório e no formato do <see cref="CodigoTipoDocumento"/>.</param>
/// <param name="Nome">Nome do tipo, obrigatório.</param>
/// <param name="Descricao">Descrição, opcional.</param>
/// <param name="Categoria">Código de uma categoria do cadastro, obrigatório; a existência é conferida na gravação.</param>
/// <param name="FormatosAceitos">Formatos aceitos, opcional.</param>
/// <param name="TamanhoMaximoMb">Tamanho máximo em MB, opcional; quando informado, positivo.</param>
/// <param name="TipoEquivalente">Código de outro tipo tido como equivalente, opcional; nunca o do próprio tipo.</param>
/// <param name="ClassificacaoProtecao">Classificação de proteção do documento; ausente é recusada como obrigatória.</param>
/// <param name="FinalidadeTratamento">Finalidade do tratamento do dado do documento.</param>
/// <param name="HipoteseLegal">Hipótese legal do tratamento do dado do documento.</param>
public sealed record DeclaracaoDoTipoDocumento(
    string? Codigo,
    string? Nome,
    string? Descricao,
    string? Categoria,
    string? FormatosAceitos,
    int? TamanhoMaximoMb,
    string? TipoEquivalente,
    CodigoInformado<ClassificacaoDeProtecao> ClassificacaoProtecao,
    string? FinalidadeTratamento,
    CodigoInformado<HipoteseLegalTratamento> HipoteseLegal);
