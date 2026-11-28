namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Campos declarados na criação ou atualização de um tipo de ato. Os atributos e o início da vigência são
/// anuláveis para que a ausência chegue à conferência e seja recusada junto das demais recusas.
/// </summary>
public sealed record DeclaracaoDoTipoAtoPublicado(
    string? Codigo,
    string? Nome,
    bool? CongelaConfiguracao,
    bool? UnicoPorObjeto,
    bool? EfeitoIrreversivel,
    bool? EhResultado,
    bool? PublicavelComoAvulso,
    bool? ExigeAtoCorrigido,
    DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim,
    string? BaseLegal);
