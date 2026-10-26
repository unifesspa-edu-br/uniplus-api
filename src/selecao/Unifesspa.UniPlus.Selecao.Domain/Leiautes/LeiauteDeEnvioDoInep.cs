namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// O arquivo que o Uni+ gera para o INEP: uma coluna com o CPF, uma linha por inscrito, fim de
/// linha <c>\n</c> depois de cada linha (inclusive a última) e sem cabeçalho.
/// </summary>
public sealed class LeiauteDeEnvioDoInep : ILeiauteDeArquivoDoInep
{
    public const char FimDeLinha = '\n';

    private LeiauteDeEnvioDoInep(string codigo, string versao, string descricao, bool ativo)
    {
        Codigo = codigo;
        Versao = versao;
        Descricao = descricao;
        Ativo = ativo;
        Hash = DefinicaoCanonicaDoLeiaute.Hash(
            codigo, versao, Tipo, string.Empty, FimDeLinha.ToString(), 1, [ColunaDoRetorno.Cpf(0)]);
    }

    public string Codigo { get; }

    public string Versao { get; }

    public TipoDeLeiauteDoInep Tipo => TipoDeLeiauteDoInep.Envio;

    public string Descricao { get; }

    public bool Ativo { get; }

    public string Hash { get; }

    public static LeiauteDeEnvioDoInep Criar(string codigo, string versao, string descricao, bool ativo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);

        return new LeiauteDeEnvioDoInep(codigo, versao, descricao, ativo);
    }

    public LeiauteDeEnvioDoInep ComAtivo(bool ativo) => new(Codigo, Versao, Descricao, ativo);
}
