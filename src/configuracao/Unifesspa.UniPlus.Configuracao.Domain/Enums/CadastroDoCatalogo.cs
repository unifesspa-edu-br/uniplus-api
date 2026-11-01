namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Os cadastros do catálogo cujos itens passam pela aprovação da proteção de dados (ADR-0153).</summary>
public enum CadastroDoCatalogo
{
    /// <summary>Sentinela — cadastro não informado.</summary>
    Nenhum = 0,

    /// <summary>O cadastro de tipos de documento.</summary>
    TipoDocumento,

    /// <summary>O cadastro de fatos do candidato.</summary>
    Fato,
}
