namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>Campos declarados na criação ou atualização de um tipo de ato.</summary>
public sealed record DeclaracaoDoTipoAtoPublicado(
    string? Codigo,
    string? Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    bool EhResultado,
    bool PublicavelComoAvulso,
    bool ExigeAtoCorrigido,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string? BaseLegal);
