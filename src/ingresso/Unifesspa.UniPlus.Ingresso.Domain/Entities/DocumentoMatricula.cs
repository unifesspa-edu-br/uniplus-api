namespace Unifesspa.UniPlus.Ingresso.Domain.Entities;

using Unifesspa.UniPlus.SharedKernel.Domain.Entities;

public sealed class DocumentoMatricula : EntityBase
{
    public Guid MatriculaId { get; private set; }
    public string TipoDocumento { get; private set; } = string.Empty;
    public string NomeArquivo { get; private set; } = string.Empty;
    public string CaminhoStorage { get; private set; } = string.Empty;
    public long TamanhoBytes { get; private set; }
    public bool Validado { get; private set; }
    public string? MotivoRejeicao { get; private set; }

    private DocumentoMatricula() { }

    public static DocumentoMatricula Criar(Guid matriculaId, string tipoDocumento, string nomeArquivo, string caminhoStorage, long tamanhoBytes)
    {
        return new DocumentoMatricula
        {
            MatriculaId = matriculaId,
            TipoDocumento = tipoDocumento,
            NomeArquivo = nomeArquivo,
            CaminhoStorage = caminhoStorage,
            TamanhoBytes = tamanhoBytes
        };
    }

    public void Validar() => Validado = true;

    public void Rejeitar(string motivo)
    {
        Validado = false;
        MotivoRejeicao = motivo;
    }
}
