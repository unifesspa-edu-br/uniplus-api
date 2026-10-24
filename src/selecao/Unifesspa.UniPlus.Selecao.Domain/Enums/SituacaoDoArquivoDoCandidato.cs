namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Ciclo de vida do arquivo do candidato: <see cref="Pendente"/> com a URL de envio, <see cref="EmVerificacao"/> na
/// confirmação e, por fim, <see cref="Verificado"/>, <see cref="Recusado"/> ou <see cref="ReenvioNecessario"/>. Só o
/// verificado atende a exigência. O arquivo do administrador segue em <see cref="StatusArquivoEnviado"/>.
/// </summary>
public enum SituacaoDoArquivoDoCandidato
{
    Pendente = 0,
    EmVerificacao = 1,
    Verificado = 2,
    Recusado = 3,
    ReenvioNecessario = 4,
}
