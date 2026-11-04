namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que restringe o arquivo que o candidato envia para uma folha: a idade máxima de emissão, os formatos
/// permitidos e o modelo que a folha cita.
/// </summary>
internal sealed record RestricoesDoDocumentoDoPayload(IdadeMaximaEmissao? Idade, FormatosPermitidos Formatos, ModeloDaExigencia? Modelo)
{
    /// <summary>Resolve as três restrições nessa ordem; a primeira recusa encerra.</summary>
    public static Result<RestricoesDoDocumentoDoPayload> Resolver(
        ItemDocumentoExigidoInput folha, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso)
    {
        Result<IdadeMaximaEmissao?> idade = IdadeMaximaEmissaoDaFolha(folha.IdadeMaximaEmissao);
        if (idade.IsFailure)
        {
            return Result<RestricoesDoDocumentoDoPayload>.ValidationFailure(idade.Errors);
        }

        Result<FormatosPermitidos> formatos = FormatosPermitidosDoPayload.Resolver(folha.FormatosPermitidos);
        if (formatos.IsFailure)
        {
            return Result<RestricoesDoDocumentoDoPayload>.ValidationFailure(formatos.Errors);
        }

        Result<ModeloDaExigencia?> modelo = ModeloDaFolha(folha.ModeloId, modelosDoProcesso);
        return modelo.IsFailure
            ? Result<RestricoesDoDocumentoDoPayload>.ValidationFailure(modelo.Errors)
            : Result<RestricoesDoDocumentoDoPayload>.Success(new RestricoesDoDocumentoDoPayload(idade.Value, formatos.Value!, modelo.Value));
    }

    private static Result<IdadeMaximaEmissao?> IdadeMaximaEmissaoDaFolha(IdadeMaximaEmissaoInput? idade) =>
        IdadeMaximaEmissao.Criar(
            idade?.Valor,
            UnidadeIdadeCodigo.FromCodigo(idade?.Unidade),
            ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(idade?.ReferenciaTipo),
            idade?.Data,
            idade?.ReferenciaFaseId);

    // O modelo que a folha cita, copiado por valor; nulo quando ela não cita. Recusa o modelo que não é
    // deste processo — a mesma recusa do inexistente — e o que ainda não foi confirmado.
    private static Result<ModeloDaExigencia?> ModeloDaFolha(Guid? modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso)
    {
        if (modeloId is not { } id)
        {
            return Result<ModeloDaExigencia?>.Success(null);
        }

        if (!modelosDoProcesso.TryGetValue(id, out ModeloDeDocumento? modelo))
        {
            return Result<ModeloDaExigencia?>.Failure(new DomainError(
                "DocumentoExigido.ModeloNaoEncontrado", $"O modelo de documento {id} não existe neste Processo Seletivo."));
        }

        Result<ModeloDaExigencia> copia = ModeloDaExigencia.Do(modelo);
        return copia.IsSuccess ? Result<ModeloDaExigencia?>.Success(copia.Value) : Result<ModeloDaExigencia?>.Failure(copia.Error!);
    }
}
