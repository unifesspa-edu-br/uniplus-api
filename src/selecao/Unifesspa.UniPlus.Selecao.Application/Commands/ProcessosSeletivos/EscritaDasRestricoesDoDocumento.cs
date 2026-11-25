namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O que restringe o arquivo que o candidato envia para uma regra de documento: o prazo de emissão, a validade, os
/// formatos permitidos e o modelo citado pela regra.
/// </summary>
internal sealed record EscritaDasRestricoesDoDocumento(
    PrazoDeEmissao? Prazo, ValidadeDoDocumento? Validade, FormatosPermitidos Formatos, ModeloDaExigencia? Modelo)
{
    /// <summary>Resolve as restrições na ordem de prazo, validade, formatos e modelo, juntando todas as recusas.</summary>
    public static Result<EscritaDasRestricoesDoDocumento> Resolver(
        ItemDocumentoExigidoInput documento, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso)
    {
        Result<PrazoDeEmissao?> prazo = EscritaDoPrazoEDaValidadeDoDocumento.Prazo(documento.PrazoDeEmissao());
        Result<ValidadeDoDocumento?> validade = EscritaDoPrazoEDaValidadeDoDocumento.Validade(documento.Validade());
        Result<FormatosPermitidos> formatos = EscritaDosFormatosPermitidos.Resolver(documento.FormatosPermitidos());
        Result<ModeloDaExigencia?> modelo = ModeloDaRegra(documento.ModeloId(), modelosDoProcesso);
        List<FieldError> recusa = [.. prazo.Errors, .. validade.Errors,
            .. formatos.Errors.Select(erro => erro.NoCaminhoDoItem("requisitos.formatosPermitidos")),
            .. modelo.Errors.Select(erro => erro.NoCaminhoDoItem("requisitos.modelo.modeloId"))];
        return recusa.Count > 0
            ? Result<EscritaDasRestricoesDoDocumento>.ValidationFailure(recusa)
            : Result<EscritaDasRestricoesDoDocumento>.Success(new EscritaDasRestricoesDoDocumento(prazo.Value, validade.Value, formatos.Value!, modelo.Value));
    }

    private static Result<ModeloDaExigencia?> ModeloDaRegra(Guid? modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso)
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

    // O modelo citado pela regra, copiado por valor. Recusa o modelo que não é deste processo — a mesma recusa
    // do inexistente — e o que ainda não foi confirmado.
    private static Result<ModeloDaExigencia> ModeloCitado(Guid modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso) =>
        modelosDoProcesso.TryGetValue(modeloId, out ModeloDeDocumento? modelo)
            ? ModeloDaExigencia.Do(modelo)
            : Result<ModeloDaExigencia>.Failure(new DomainError(
                "DocumentoExigido.ModeloNaoEncontrado", $"O modelo de documento {modeloId} não existe neste Processo Seletivo."));
}
