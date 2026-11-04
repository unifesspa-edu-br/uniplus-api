namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;

using Kernel.Results;

/// <summary>
/// Confere o payload das etapas sem consultar cadastro algum: o <c>Id</c> repetido e a forma de TODAS as
/// etapas, acumulada (ADR-0125). Roda antes de qualquer leitura externa — a validação vence o I/O —, para
/// que uma etapa com nome vazio não fique escondida atrás da recusa de catálogo de outra.
/// </summary>
internal static class ConferenciaDoPayloadDasEtapas
{
    public static Result Conferir(IReadOnlyList<EtapaProcessoInput> etapas)
    {
        List<Guid> idsInformados = [.. etapas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value)];
        if (idsInformados.Distinct().Count() != idsInformados.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode ser informado mais de uma vez no mesmo payload."));
        }

        List<FieldError> erros = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            erros.AddRange(EtapaDoPayload.ComIndice(FormaDaEtapa(etapas[indice]), indice));
        }

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    private static List<FieldError> FormaDaEtapa(EtapaProcessoInput etapa) =>
        EtapaProcesso.ValidarFormaBasica(
            etapa.Nome, etapa.Carater, etapa.Peso, etapa.NotaMinima, etapa.Ordem,
            etapa.NotaMaxima, etapa.PassoDoLancamento,
            EtapaDoPayload.ItensDeclarados(etapa), EtapaDoPayload.NiveisDeclarados(etapa));
}
