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
        Result<IdadeMaximaEmissao?> idade = IdadeMaximaEmissaoDaFolha(folha.IdadeMaximaEmissao);
        if (idade.IsFailure)
        {
            return Result<EscritaDasRestricoesDoDocumento>.ValidationFailure(idade.Errors);
        }

        Result<FormatosPermitidos> formatos = EscritaDosFormatosPermitidos.Resolver(folha.FormatosPermitidos);
        if (formatos.IsFailure)
        {
            return Result<EscritaDasRestricoesDoDocumento>.ValidationFailure(formatos.Errors);
        }

        ModeloDaExigencia? modelo = null;
        if (folha.ModeloId is { } modeloId)
        {
            Result<ModeloDaExigencia> citado = ModeloCitado(modeloId, modelosDoProcesso);
            if (citado.IsFailure)
            {
                return Result<EscritaDasRestricoesDoDocumento>.ValidationFailure(citado.Errors);
            }

            modelo = citado.Value;
        }

        return Result<EscritaDasRestricoesDoDocumento>.Success(new EscritaDasRestricoesDoDocumento(idade.Value, formatos.Value!, modelo));
    }

    private static Result<IdadeMaximaEmissao?> IdadeMaximaEmissaoDaFolha(IdadeMaximaEmissaoInput? idade) =>
        IdadeMaximaEmissao.Criar(
            idade?.Valor,
            UnidadeIdadeCodigo.FromCodigo(idade?.Unidade),
            ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(idade?.ReferenciaTipo),
            idade?.Data,
            idade?.ReferenciaFaseId);

    // O modelo que a folha cita, copiado por valor. Recusa o modelo que não é deste processo — a mesma recusa
    // do inexistente — e o que ainda não foi confirmado.
    private static Result<ModeloDaExigencia> ModeloCitado(Guid modeloId, IReadOnlyDictionary<Guid, ModeloDeDocumento> modelosDoProcesso) =>
        modelosDoProcesso.TryGetValue(modeloId, out ModeloDeDocumento? modelo)
            ? ModeloDaExigencia.Do(modelo)
            : Result<ModeloDaExigencia>.Failure(new DomainError(
                "DocumentoExigido.ModeloNaoEncontrado", $"O modelo de documento {modeloId} não existe neste Processo Seletivo."));
}
