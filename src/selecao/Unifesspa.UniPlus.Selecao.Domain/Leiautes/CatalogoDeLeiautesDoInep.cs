namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os leiautes dos arquivos do INEP, versionados no código: um formato novo é uma versão nova,
/// com teste antes de chegar ao administrador. Uma versão publicada nunca muda; o que deixa de
/// valer é desativado.
/// </summary>
public sealed class CatalogoDeLeiautesDoInep
{
    public const string CodigoDoEnvioDoEnem = "INEP_ENEM_ENVIO";
    public const string CodigoDoRetornoDoEnem = "INEP_ENEM_RETORNO";
    public const string VersaoV1 = "v1";

    private const string CodigoDaSituacaoPresente = "7";
    private const string CodigoDaSituacaoAusente = "6";

    private readonly IReadOnlyList<ILeiauteDeArquivoDoInep> _leiautes;

    public CatalogoDeLeiautesDoInep(IEnumerable<ILeiauteDeArquivoDoInep> leiautes)
    {
        ArgumentNullException.ThrowIfNull(leiautes);

        _leiautes = [.. leiautes];
    }

    /// <summary>Envio dos CPFs da edição ao INEP.</summary>
    public static LeiauteDeEnvioDoInep EnemEnvioV1 { get; } = LeiauteDeEnvioDoInep.Criar(
        CodigoDoEnvioDoEnem, VersaoV1, "Um CPF por linha, sem cabeçalho", ativo: true);

    /// <summary>Retorno do INEP com 20 campos, vigente a partir da edição de 2024 do ENEM.</summary>
    public static LeiauteDeRetornoDoInep EnemRetornoV1 { get; } = LeiauteDeRetornoDoInep.Criar(
        CodigoDoRetornoDoEnem,
        VersaoV1,
        "Retorno do INEP com 20 campos, a partir da edição de 2024 do ENEM",
        ativo: true,
        [
            ColunaDoRetorno.NumeroDeInscricaoNoEnem(0),
            ColunaDoRetorno.Cpf(1),
            ColunaDoRetorno.Nome(2),
            ColunaDoRetorno.Nota(3, AreaDoEnem.CienciasDaNatureza),
            ColunaDoRetorno.Nota(4, AreaDoEnem.CienciasHumanas),
            ColunaDoRetorno.Nota(5, AreaDoEnem.Linguagens),
            ColunaDoRetorno.Nota(6, AreaDoEnem.Matematica),
            ColunaDoRetorno.Nota(7, AreaDoEnem.Redacao),
            ColunaDoRetorno.Situacao(8, AreaDoEnem.CienciasDaNatureza, DicionarioDeSituacao()),
            ColunaDoRetorno.Situacao(9, AreaDoEnem.CienciasHumanas, DicionarioDeSituacao()),
            ColunaDoRetorno.Situacao(10, AreaDoEnem.Linguagens, DicionarioDeSituacao()),
            ColunaDoRetorno.Situacao(11, AreaDoEnem.Matematica, DicionarioDeSituacao()),
            ColunaDoRetorno.Situacao(12, AreaDoEnem.Redacao, DicionarioDeSituacao()),
            ColunaDoRetorno.Ignorada(13),
            ColunaDoRetorno.Sexo(14),
            ColunaDoRetorno.Ignorada(15),
            ColunaDoRetorno.Ignorada(16),
            ColunaDoRetorno.Ignorada(17),
            ColunaDoRetorno.Ignorada(18),
            ColunaDoRetorno.Vazia(19),
        ]);

    /// <summary>O catálogo em vigor. Declarado depois dos leiautes porque a inicialização estática segue a ordem do texto.</summary>
    public static CatalogoDeLeiautesDoInep Padrao { get; } = new([EnemEnvioV1, EnemRetornoV1]);

    /// <summary>Os leiautes que o administrador pode escolher para o tipo de arquivo.</summary>
    public IReadOnlyList<ILeiauteDeArquivoDoInep> Ativos(TipoDeLeiauteDoInep tipo) =>
        [.. _leiautes.Where(l => l.Tipo == tipo && l.Ativo)];

    /// <summary>
    /// O leiaute pelo código, versão e tipo, mesmo desativado: o lote que o usou continua a citá-lo.
    /// </summary>
    public ILeiauteDeArquivoDoInep? Obter(string codigo, string versao, TipoDeLeiauteDoInep tipo) =>
        _leiautes.FirstOrDefault(l =>
            l.Tipo == tipo
            && string.Equals(l.Codigo, codigo, StringComparison.Ordinal)
            && string.Equals(l.Versao, versao, StringComparison.Ordinal));

    private static LeitorDeCodigoDeSituacaoDaProva DicionarioDeSituacao() => new(
        new Dictionary<string, SituacaoDaProvaNoEnem>
        {
            [CodigoDaSituacaoPresente] = SituacaoDaProvaNoEnem.Presente,
            [CodigoDaSituacaoAusente] = SituacaoDaProvaNoEnem.Ausente,
        });
}
