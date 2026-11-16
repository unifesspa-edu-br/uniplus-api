namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Os campos de um tipo de etapa que a API altera, já conferidos e normalizados.</summary>
public sealed record CamposEditaveisDoTipoDeEtapa(
    string Nome,
    string? Descricao,
    bool AdmitePontuacao,
    bool AdmiteEliminacao,
    bool ExigeCorrecaoIndependente);
