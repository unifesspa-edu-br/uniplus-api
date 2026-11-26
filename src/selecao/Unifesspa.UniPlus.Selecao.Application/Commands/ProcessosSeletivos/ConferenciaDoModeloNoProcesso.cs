namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ProcessosSeletivos.Formularios;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O modelo serve a este processo: está ativo, é do tipo dele ou de todos os tipos, e os pressupostos —
/// fatos da inscrição que ele cita — são coletados pelos formulários que coletam a inscrição do processo.
/// </summary>
internal static class ConferenciaDoModeloNoProcesso
{
    public static IEnumerable<FieldError> Conferir(ModeloFormularioView modelo, ProcessoSeletivo processo) =>
        RecusasDoModelo(modelo, processo).Concat(PressupostosAusentes(modelo, processo));

    private static IEnumerable<FieldError> RecusasDoModelo(ModeloFormularioView modelo, ProcessoSeletivo processo)
    {
        if (!modelo.Ativo)
        {
            yield return new("modeloId", new DomainError(
                AplicacaoDeModeloErrorCodes.ModeloInativo, $"O modelo '{modelo.Codigo}' está desativado e não se aplica a processo novo."));
        }

        if (modelo.TipoProcessoCodigo is { } tipo && !string.Equals(tipo, processo.TipoProcesso.Codigo, StringComparison.Ordinal))
        {
            yield return new("modeloId", new DomainError(
                AplicacaoDeModeloErrorCodes.TipoDeProcessoDiferente,
                $"O modelo '{modelo.Codigo}' é do tipo de processo '{tipo}', e este processo é '{processo.TipoProcesso.Codigo}'."));
        }
    }

    private static IEnumerable<FieldError> PressupostosAusentes(ModeloFormularioView modelo, ProcessoSeletivo processo)
    {
        HashSet<string> daInscricao = CodigosColetadosNaInscricao(processo);
        IReadOnlyList<string> pressupostos = modelo.Conteudo.Pressupostos ?? [];
        for (int i = 0; i < pressupostos.Count; i++)
        {
            if (!daInscricao.Contains(pressupostos[i]))
            {
                yield return new($"modelo.pressupostos[{i}]", new DomainError(
                    AplicacaoDeModeloErrorCodes.PressupostoAusente,
                    $"O modelo cita '{pressupostos[i]}', que a inscrição do processo não coleta; colete-o na inscrição antes."));
            }
        }
    }

    private static HashSet<string> CodigosColetadosNaInscricao(ProcessoSeletivo processo)
    {
        HashSet<Guid> formularios = [.. processo.Declaracao().DaInscricao().Select(static f => f.Id)];
        return new(
            processo.FatosColetados.Where(f => f.FormularioId is { } id && formularios.Contains(id)).Select(static f => f.FatoCodigo),
            StringComparer.Ordinal);
    }
}
