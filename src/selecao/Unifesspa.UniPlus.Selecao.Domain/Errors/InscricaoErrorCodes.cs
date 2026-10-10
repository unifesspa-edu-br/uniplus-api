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
    public const string FatoForaDoFormulario = "Inscricao.FatoForaDoFormulario";
    public const string ValorForaDoTipo = "Inscricao.ValorForaDoTipo";
    public const string CpfDiferenteDoDono = "Inscricao.CpfDiferenteDoDono";
    public const string OfertaForaDoProcesso = "Inscricao.OfertaForaDoProcesso";
    public const string OfertaObrigatoria = "Inscricao.OfertaObrigatoria";

    // Veredito de submissão (issue #1859) — só avaliados no POST .../submissao, nunca no PUT do
    // rascunho, que não exige obrigatórios.
    public const string ItemObrigatorioAusente = "Inscricao.ItemObrigatorioAusente";
    public const string Impedimento = "Inscricao.Impedimento";
    public const string GrupoContagemInvalida = "Inscricao.GrupoContagemInvalida";
    public const string GrupoCandidatoAusente = "Inscricao.GrupoCandidatoAusente";
    public const string GrupoIdDuplicado = "Inscricao.GrupoIdDuplicado";
    public const string TermoNaoAceito = "Inscricao.TermoNaoAceito";

    /// <summary>
    /// A submissão tem Idempotency-Key (ADR-0119) — o handler captura a corrida de xmin
    /// localmente em vez de deixá-la propagar para o 409 central do GlobalExceptionMiddleware.
    /// </summary>
    public const string ConflitoDeConcorrencia = "Inscricao.ConflitoDeConcorrencia";

    /// <summary>O número da URL não corresponde a nenhum comprovante emitido (issue #1860).</summary>
    public const string ComprovanteNaoEncontrado = "Inscricao.ComprovanteNaoEncontrado";
}
