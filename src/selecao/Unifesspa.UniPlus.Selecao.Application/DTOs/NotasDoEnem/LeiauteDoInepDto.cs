namespace Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;

using Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>Um leiaute de arquivo do INEP que o administrador pode escolher.</summary>
/// <param name="Codigo">O código do leiaute, que junto da versão o nomeia.</param>
/// <param name="Versao">A versão do leiaute.</param>
/// <param name="Tipo"><c>ENVIO</c> ou <c>RETORNO</c>.</param>
/// <param name="Descricao">Para que formato de arquivo o leiaute serve.</param>
/// <param name="Hash">SHA-256 da definição do leiaute.</param>
/// <param name="QuantidadeDeCampos">Quantas colunas cada linha do arquivo tem.</param>
public sealed record LeiauteDoInepDto(
    string Codigo,
    string Versao,
    string Tipo,
    string Descricao,
    string Hash,
    int QuantidadeDeCampos)
{
    public static LeiauteDoInepDto De(ILeiauteDeArquivoDoInep leiaute)
    {
        ArgumentNullException.ThrowIfNull(leiaute);

        return new LeiauteDoInepDto(
            leiaute.Codigo,
            leiaute.Versao,
            leiaute.Tipo == TipoDeLeiauteDoInep.Envio ? "ENVIO" : "RETORNO",
            leiaute.Descricao,
            leiaute.Hash,
            leiaute.QuantidadeDeCampos);
    }
}
