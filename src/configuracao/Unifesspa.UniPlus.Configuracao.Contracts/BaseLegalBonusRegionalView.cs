namespace Unifesspa.UniPlus.Configuracao.Contracts;

public sealed record BaseLegalBonusRegionalView(
    Guid Id,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<BaseLegalBonusRegionalMunicipioView> Municipios);

public sealed record BaseLegalBonusRegionalMunicipioView(
    string CodigoIbge,
    string Nome,
    string Uf);
