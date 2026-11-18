namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

public static class PrazoDeEmissaoErrorCodes
{
    /// <summary>A exigência obriga o prazo máximo de emissão e não o declara.</summary>
    public const string PrazoObrigatorio = "IdadeMaximaEmissao.PrazoObrigatorio";

    /// <summary>A fase âncora do prazo, ou a fase de inscrição que ele pressupõe, não é do cronograma do processo.</summary>
    public const string FaseNaoPertenceAoProcesso = "IdadeMaximaEmissao.FaseNaoPertenceAoProcesso";

    /// <summary>O extremo da fase que o prazo usa como âncora não está definido.</summary>
    public const string FaseExtremoAusente = "IdadeMaximaEmissao.FaseExtremoAusente";
}
