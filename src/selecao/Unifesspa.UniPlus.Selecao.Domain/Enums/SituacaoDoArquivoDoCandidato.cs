namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Ciclo de vida do arquivo do candidato: <see cref="Pendente"/> com a URL de envio, <see cref="EmVerificacao"/> na
/// confirmação e, por fim, <see cref="Verificado"/>, <see cref="Recusado"/> ou <see cref="ReenvioNecessario"/>. Só o
/// verificado atende a exigência. O arquivo do administrador segue em <see cref="StatusArquivoEnviado"/>.
/// </summary>
public enum SituacaoDoArquivoDoCandidato
{
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "O envio do arquivo está pendente.")]
    Pendente = 0,
    [CodigoDeContrato("EM_VERIFICACAO", "Em verificação", Descricao = "O arquivo está em verificação.")]
    EmVerificacao = 1,
    [CodigoDeContrato("VERIFICADO", "Verificado", Descricao = "O arquivo foi verificado.")]
    Verificado = 2,
    [CodigoDeContrato("RECUSADO", "Recusado", Descricao = "O arquivo foi recusado.")]
    Recusado = 3,
    [CodigoDeContrato("REENVIO_NECESSARIO", "Reenvio necessário", Descricao = "O arquivo precisa ser enviado novamente.")]
    ReenvioNecessario = 4,
}
