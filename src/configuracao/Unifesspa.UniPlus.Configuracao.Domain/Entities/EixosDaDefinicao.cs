namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Os eixos de definição e de proteção que a alteração da definição do fato troca juntos. O escopo é
/// opcional porque a proposta sem escopo válido deixa o que o fato já tem.
/// </summary>
internal readonly record struct EixosDaDefinicao(
    TipoDoDado Dominio,
    QuantidadeDeValores Cardinalidade,
    FonteValoresFato? FonteValores,
    FormatoTexto? Formato,
    string PontoResolucao,
    EscopoFato? Escopo,
    ClassificacaoDeProtecao Classificacao,
    string Finalidade,
    HipoteseLegalTratamento Hipotese);
