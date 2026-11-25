namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>Campos conferidos de um tipo de ato.</summary>
public sealed record CamposDoTipoAtoPublicado(
    string Codigo,
    string Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    bool EhResultado,
    bool PublicavelComoAvulso,
    bool ExigeAtoCorrigido,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string? BaseLegal);
