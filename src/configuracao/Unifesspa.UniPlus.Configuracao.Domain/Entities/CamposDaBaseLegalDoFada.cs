namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Os campos de uma base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) depois de conferidos.</summary>
internal sealed record CamposDaBaseLegalDoFada(
    TipoInstrumentoNormativo TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<MunicipioDaBaseLegalDoFada> Municipios);
