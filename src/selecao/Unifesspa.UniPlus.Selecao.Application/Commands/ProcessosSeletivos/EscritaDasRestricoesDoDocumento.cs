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
internal sealed record EscritaDasRestricoesDoDocumento(IdadeMaximaEmissao? Idade, FormatosPermitidos Formatos, ModeloDaExigencia? Modelo)
{
    /// <summary>Resolve as três restrições nessa ordem; a primeira recusa encerra.</summary>
    public static Result<EscritaDasRestricoesDoDocumento> Resolver(
        ItemDocumentoExigidoInput folha, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso)
    {
        Result<IdadeMaximaEmissao?> idade = IdadeMaximaEmissaoDaFolha(folha.IdadeMaximaEmissao());
        Result<FormatosPermitidos> formatos = EscritaDosFormatosPermitidos.Resolver(folha.FormatosPermitidos());
        Result<ModeloDaExigencia?> modelo = ModeloDaFolha(folha.ModeloId(), modelosDoProcesso);
        IReadOnlyList<FieldError>? recusa = new[] { idade.Errors, formatos.Errors, modelo.Errors }
            .FirstOrDefault(static erros => erros.Count > 0);
        return recusa is not null
            ? Result<EscritaDasRestricoesDoDocumento>.ValidationFailure(recusa)
            : Result<EscritaDasRestricoesDoDocumento>.Success(new EscritaDasRestricoesDoDocumento(idade.Value, formatos.Value!, modelo.Value));
    }

    private static Result<IdadeMaximaEmissao?> IdadeMaximaEmissaoDaFolha(IdadeMaximaEmissaoInput? idade) =>
        IdadeMaximaEmissao.Criar(
            idade?.Valor,
            UnidadeIdadeCodigo.FromCodigo(idade?.Unidade),
            ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(idade?.ReferenciaTipo),
            idade?.Data,
            idade?.ReferenciaFaseId);

    private static Result<ModeloDaExigencia?> ModeloDaFolha(Guid? modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso)
    {
        if (modeloId is not { } id)
        {
            return Result<ModeloDaExigencia?>.Success(null);
        }

        Result<ModeloDaExigencia> citado = ModeloCitado(id, modelosDoProcesso);
        return citado.IsFailure
            ? Result<ModeloDaExigencia?>.ValidationFailure(citado.Errors)
            : Result<ModeloDaExigencia?>.Success(citado.Value);
    }

    // O modelo que a folha cita, copiado por valor. Recusa o modelo que não é deste processo — a mesma recusa
    // do inexistente — e o que ainda não foi confirmado.
    private static Result<ModeloDaExigencia> ModeloCitado(Guid modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso) =>
        modelosDoProcesso.TryGetValue(modeloId, out ModeloDeDocumento? modelo)
            ? ModeloDaExigencia.Do(modelo)
            : Result<ModeloDaExigencia>.Failure(new DomainError(
                "DocumentoExigido.ModeloNaoEncontrado", $"O modelo de documento {modeloId} não existe neste Processo Seletivo."));
}
