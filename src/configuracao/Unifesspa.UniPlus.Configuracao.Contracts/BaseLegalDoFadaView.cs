namespace Unifesspa.UniPlus.Configuracao.Contracts;

public sealed record BaseLegalDoFadaView(
    Guid Id,
    string Codigo,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<MunicipioDaBaseLegalDoFadaView> Municipios);

public sealed record MunicipioDaBaseLegalDoFadaView(
    string CodigoIbge,
    string Nome,
    string Uf);
