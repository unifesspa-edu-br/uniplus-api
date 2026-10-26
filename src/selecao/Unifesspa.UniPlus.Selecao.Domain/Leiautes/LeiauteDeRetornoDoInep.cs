namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O arquivo que o INEP devolve: texto UTF-8 separado por <c>;</c>, sem cabeçalho, lido pela
/// posição da coluna. Aceita fim de linha <c>\n</c> ou <c>\r\n</c> e uma marca BOM no início.
/// </summary>
public sealed class LeiauteDeRetornoDoInep : ILeiauteDeArquivoDoInep
{
    public const char Separador = ';';

    private LeiauteDeRetornoDoInep(
        string codigo, string versao, string descricao, bool ativo, IReadOnlyList<ColunaDoRetorno> colunas)
    {
        Codigo = codigo;
        Versao = versao;
        Descricao = descricao;
        Ativo = ativo;
        Colunas = colunas;
        QuantidadeDeCampos = colunas.Count;
        Hash = DefinicaoCanonicaDoLeiaute.Hash(
            codigo, versao, Tipo, Separador.ToString(), "LF|CRLF", QuantidadeDeCampos, colunas);
    }

    public string Codigo { get; }

    public string Versao { get; }

    public TipoDeLeiauteDoInep Tipo => TipoDeLeiauteDoInep.Retorno;

    public string Descricao { get; }

    public bool Ativo { get; }

    public string Hash { get; }

    public int QuantidadeDeCampos { get; }

    public IReadOnlyList<ColunaDoRetorno> Colunas { get; }

    /// <summary>
    /// Monta o leiaute conferindo a definição: posições contíguas desde zero, uma coluna de CPF, uma
    /// de número de inscrição e a nota e a situação de cada uma das cinco áreas, cada uma uma só vez.
    /// Uma definição incoerente é erro de programação do catálogo, não de quem importa o arquivo.
    /// </summary>
    public static LeiauteDeRetornoDoInep Criar(
        string codigo, string versao, string descricao, bool ativo, IReadOnlyList<ColunaDoRetorno> colunas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);
        ArgumentNullException.ThrowIfNull(colunas);

        ColunaDoRetorno[] ordenadas = [.. colunas.OrderBy(static c => c.Posicao)];
        for (int i = 0; i < ordenadas.Length; i++)
        {
            if (ordenadas[i].Posicao != i)
            {
                throw new ArgumentException("As posições das colunas devem ser contíguas a partir de zero.", nameof(colunas));
            }
        }

        ConferirCampoUnico(ordenadas, CampoDoRetorno.Cpf, area: null);
        ConferirCampoUnico(ordenadas, CampoDoRetorno.NumeroDeInscricaoNoEnem, area: null);
        foreach (string area in AreaDoEnem.Todas)
        {
            ConferirCampoUnico(ordenadas, CampoDoRetorno.Nota, area);
            ConferirCampoUnico(ordenadas, CampoDoRetorno.Situacao, area);
        }

        return new LeiauteDeRetornoDoInep(codigo, versao, descricao, ativo, ordenadas);
    }

    public LeiauteDeRetornoDoInep ComAtivo(bool ativo) => new(Codigo, Versao, Descricao, ativo, Colunas);

    private static void ConferirCampoUnico(ColunaDoRetorno[] colunas, CampoDoRetorno campo, string? area)
    {
        if (colunas.Count(c => c.Campo == campo && c.Area == area) != 1)
        {
            throw new ArgumentException($"O leiaute deve ter exatamente uma coluna para {campo}{(area is null ? string.Empty : $" ({area})")}.", nameof(colunas));
        }
    }
}
