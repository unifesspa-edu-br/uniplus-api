namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Categoria classificatória de um <see cref="Entities.TipoDocumento"/>
/// (UNI-REQ-0013). Roster fechado de sete valores — domínio fechado da #591.
/// O nome de domínio (PascalCase) é mapeado para o token de contrato/banco
/// UPPER_SNAKE por <c>CategoriaDocumentos</c> (ex.: <see cref="RacaEtnia"/> ↔
/// <c>RACA_ETNIA</c>), persistido como <c>varchar</c> com CHECK de domínio.
/// </summary>
public enum CategoriaDocumento
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Documentos de identificação (RG, CIN, CPF, passaporte).</summary>
    Identificacao = 1,

    /// <summary>Documentos de escolaridade (histórico, certificado, diploma).</summary>
    Escolaridade = 2,

    /// <summary>Documentos de comprovação de renda.</summary>
    Renda = 3,

    /// <summary>Documentos de raça/etnia (autodeclaração PPI, declaração de liderança).</summary>
    RacaEtnia = 4,

    /// <summary>Documentos de saúde (laudo médico).</summary>
    Saude = 5,

    /// <summary>Documentos de comprovação de residência.</summary>
    Residencia = 6,

    /// <summary>Demais documentos não enquadrados nas categorias anteriores.</summary>
    Outros = 7,
}
