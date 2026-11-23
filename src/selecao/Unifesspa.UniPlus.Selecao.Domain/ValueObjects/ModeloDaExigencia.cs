namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using Enums;

using Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O modelo de documento que a exigência oferece ao candidato, copiado por valor do modelo
/// confirmado (ADR-0061): o id, o nome que o candidato recebe no download, o formato e o hash do
/// arquivo. O modelo confirmado é imutável, e é o hash que congela no edital.
/// </summary>
public sealed record ModeloDaExigencia(Guid ModeloId, string NomeArquivo, FormatoDeDocumento Formato, string HashSha256)
{
    /// <summary>Resolve o modelo entre os enviados ao dono e recusa o que não foi confirmado.</summary>
    public static Result<ModeloDaExigencia> DoCitado(Guid modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoDono)
    {
        ArgumentNullException.ThrowIfNull(modelosDoDono);
        return modelosDoDono.TryGetValue(modeloId, out ModeloDeDocumento? modelo)
            ? Do(modelo)
            : Result<ModeloDaExigencia>.Failure(new DomainError(
                ModeloDaExigenciaErrorCodes.ModeloNaoEncontrado,
                "O modelo de documento citado não foi enviado para este processo."));
    }

    /// <summary>A cópia do modelo; recusa o que ainda não foi confirmado.</summary>
    public static Result<ModeloDaExigencia> Do(ModeloDeDocumento modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        return modelo.Situacao == StatusArquivoEnviado.Confirmado && modelo.HashSha256 is { } hash
            ? Result<ModeloDaExigencia>.Success(new ModeloDaExigencia(modelo.Id, modelo.NomeArquivo, modelo.Formato, hash))
            : Result<ModeloDaExigencia>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.NaoConfirmado,
                "O modelo de documento ainda não foi confirmado: confirme o envio antes de vinculá-lo à exigência."));
    }
}
