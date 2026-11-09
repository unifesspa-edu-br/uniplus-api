namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Por que uma versão com o documento verificado não entrou em vigor.</summary>
public enum MotivoDaRecusaDaVersao
{
    /// <summary>Acrescentou permissão pessoal ou sensível a perfil que já tem concessão não revogada.</summary>
    AmpliacaoExigeProtecaoDeDados = 1,

    /// <summary>O alcance (unidade ou instituição) difere do da versão vigente.</summary>
    AlcanceAlterado = 2,

    /// <summary>Outro perfil passou a usar o nome enquanto a versão esperava o documento.</summary>
    NomeIndisponivel = 3,
}

/// <summary>Código textual canônico de <see cref="MotivoDaRecusaDaVersao"/>, o valor gravado e exposto.</summary>
public static class MotivoDaRecusaDaVersaoCodigo
{
    public const string AmpliacaoExigeProtecaoDeDados = "ampliacao_exige_protecao_de_dados";
    public const string AlcanceAlterado = "alcance_alterado";
    public const string NomeIndisponivel = "nome_indisponivel";

    public static string ToCodigo(this MotivoDaRecusaDaVersao motivo) => motivo switch
    {
        MotivoDaRecusaDaVersao.AmpliacaoExigeProtecaoDeDados => AmpliacaoExigeProtecaoDeDados,
        MotivoDaRecusaDaVersao.AlcanceAlterado => AlcanceAlterado,
        MotivoDaRecusaDaVersao.NomeIndisponivel => NomeIndisponivel,
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    public static MotivoDaRecusaDaVersao FromCodigo(string codigo) => codigo switch
    {
        AmpliacaoExigeProtecaoDeDados => MotivoDaRecusaDaVersao.AmpliacaoExigeProtecaoDeDados,
        AlcanceAlterado => MotivoDaRecusaDaVersao.AlcanceAlterado,
        NomeIndisponivel => MotivoDaRecusaDaVersao.NomeIndisponivel,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
