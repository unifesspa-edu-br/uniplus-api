namespace Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Desfecho do confronto entre a referência que a regra cita por código e as
/// contrapartes congeladas no processo, decidido por identidade (ADR-0129).
/// </summary>
internal enum Casamento
{
    /// <summary>Alguma contraparte designa o mesmo item de catálogo que a regra exige.</summary>
    Casa,

    /// <summary>
    /// Nenhuma contraparte designa o item exigido, mas alguma carrega o mesmo código
    /// apontando para outro item — o código foi reatribuído depois que a regra foi escrita.
    /// </summary>
    CodigoReatribuido,

    /// <summary>Nenhuma contraparte corresponde, nem por identidade nem por código.</summary>
    NaoCasa,
}
