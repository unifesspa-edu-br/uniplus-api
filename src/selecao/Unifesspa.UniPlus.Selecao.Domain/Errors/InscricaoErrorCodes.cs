namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro da inscrição do candidato (issue #1858).</summary>
public static class InscricaoErrorCodes
{
    public const string SubObrigatorio = "Inscricao.SubObrigatorio";
    public const string EmissorObrigatorio = "Inscricao.EmissorObrigatorio";
    public const string PrecondicaoRequerida = "Inscricao.PrecondicaoRequerida";
    public const string PrecondicaoFalhou = "Inscricao.PrecondicaoFalhou";
    public const string DonoDivergente = "Inscricao.DonoDivergente";
    public const string NaoEncontrada = "Inscricao.NaoEncontrada";
    public const string RespostaInvalida = "Inscricao.RespostaInvalida";
}
