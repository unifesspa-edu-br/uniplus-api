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
    public const string DocumentoObrigatorioAusente = "Inscricao.DocumentoObrigatorioAusente";
    public const string DocumentoObrigatorioEmVerificacao = "Inscricao.DocumentoObrigatorioEmVerificacao";
    public const string RespostaInvalida = "Inscricao.RespostaInvalida";
    public const string DataForaDaFaixa = "Inscricao.DataForaDaFaixa";
    public const string FatoForaDoFormulario = "Inscricao.FatoForaDoFormulario";
    public const string GrupoForaDoFormulario = "Inscricao.GrupoForaDoFormulario";
    public const string FatoForaDoGrupo = "Inscricao.FatoForaDoGrupo";
    public const string FatoSoDeGrupo = "Inscricao.FatoSoDeGrupo";
    public const string GrupoMalFormado = "Inscricao.GrupoMalFormado";
    public const string AceiteMalFormado = "Inscricao.AceiteMalFormado";
    public const string AceiteRepetido = "Inscricao.AceiteRepetido";
    public const string AceiteForaDoFormulario = "Inscricao.AceiteForaDoFormulario";
    public const string AceiteDeOutraVersao = "Inscricao.AceiteDeOutraVersao";
    public const string ValorForaDoTipo = "Inscricao.ValorForaDoTipo";
    public const string CpfDiferenteDoDono = "Inscricao.CpfDiferenteDoDono";
    public const string NomeDiferenteDoLogin = "Inscricao.NomeDiferenteDoLogin";
    public const string NomeAusenteNoLogin = "Inscricao.NomeAusenteNoLogin";
    public const string NomeSocialDiferenteDaConta = "Inscricao.NomeSocialDiferenteDaConta";
    public const string NomeSocialAusenteNaConta = "Inscricao.NomeSocialAusenteNaConta";
    public const string EnderecoSemCep = "Inscricao.EnderecoSemCep";
    public const string EnderecoComplementoLongo = "Inscricao.EnderecoComplementoLongo";
    public const string EnderecoCepNaoEncontrado = "Inscricao.EnderecoCepNaoEncontrado";
    public const string EnderecoCepDeOutraCidade = "Inscricao.EnderecoCepDeOutraCidade";
    public const string EnderecoCepNaoConfirmado = "Inscricao.EnderecoCepNaoConfirmado";
    public const string EnderecoLogradouroObrigatorio = "Inscricao.EnderecoLogradouroObrigatorio";
    public const string EnderecoBairroObrigatorio = "Inscricao.EnderecoBairroObrigatorio";
    public const string OfertaForaDoProcesso = "Inscricao.OfertaForaDoProcesso";
    public const string OfertaObrigatoria = "Inscricao.OfertaObrigatoria";

    /// <summary>A versão fixada da inscrição declara uma só opção de curso, e a inscrição traz a 2ª.</summary>
    public const string SegundaOpcaoNaoAdmitida = "Inscricao.SegundaOpcaoNaoAdmitida";

    /// <summary>A 2ª opção de curso é a mesma oferta da 1ª.</summary>
    public const string SegundaOpcaoIgualAPrimeira = "Inscricao.SegundaOpcaoIgualAPrimeira";

    /// <summary>A inscrição traz a 2ª opção de curso e não traz a 1ª.</summary>
    public const string SegundaOpcaoSemPrimeira = "Inscricao.SegundaOpcaoSemPrimeira";
    public const string RascunhoSoAntesDoEnvio = "Inscricao.RascunhoSoAntesDoEnvio";
    public const string DescarteSoDeRascunho = "Inscricao.DescarteSoDeRascunho";
    public const string CancelamentoSoDeEnviada = "Inscricao.CancelamentoSoDeEnviada";
    public const string InscricaoCancelada = "Inscricao.InscricaoCancelada";
    public const string MotivoDoCancelamentoLongo = "Inscricao.MotivoDoCancelamentoLongo";
    public const string ForaDoPeriodo = "Inscricao.ForaDoPeriodo";
    public const string ProcessoSemInscricao = "Inscricao.ProcessoSemInscricao";

    // Veredito de submissão (issue #1859) — só avaliados no POST .../submissao, nunca no PUT do
    // rascunho, que não exige obrigatórios.
    public const string ItemObrigatorioAusente = "Inscricao.ItemObrigatorioAusente";
    public const string Impedimento = "Inscricao.Impedimento";
    public const string GrupoContagemInvalida = "Inscricao.GrupoContagemInvalida";
    public const string GrupoCandidatoAusente = "Inscricao.GrupoCandidatoAusente";
    public const string GrupoIdDuplicado = "Inscricao.GrupoIdDuplicado";
    public const string TermoNaoAceito = "Inscricao.TermoNaoAceito";

    /// <summary>A versão fixada deriva a modalidade, e as respostas não dão nenhuma das ofertadas (UNI-REQ-0076).</summary>
    public const string NenhumaModalidade = "Inscricao.NenhumaModalidade";

    /// <summary>
    /// A versão fixada deriva a modalidade, e as respostas, sem nenhum obrigatório em falta, ainda não
    /// a decidem: o envio não sai sem saber em que o candidato concorre.
    /// </summary>
    public const string ModalidadeNaoDecidida = "Inscricao.ModalidadeNaoDecidida";

    /// <summary>
    /// A submissão tem Idempotency-Key (ADR-0119) — o handler captura a corrida de xmin
    /// localmente em vez de deixá-la propagar para o 409 central do GlobalExceptionMiddleware.
    /// </summary>
    public const string ConflitoDeConcorrencia = "Inscricao.ConflitoDeConcorrencia";

    /// <summary>A fase pedida na avaliação da inscrição não está no cronograma da versão que a inscrição fixou.</summary>
    public const string FaseNaoEncontrada = "Inscricao.FaseNaoEncontrada";

    /// <summary>O número da URL não corresponde a nenhum comprovante emitido (issue #1860).</summary>
    public const string ComprovanteNaoEncontrado = "Inscricao.ComprovanteNaoEncontrado";
}
