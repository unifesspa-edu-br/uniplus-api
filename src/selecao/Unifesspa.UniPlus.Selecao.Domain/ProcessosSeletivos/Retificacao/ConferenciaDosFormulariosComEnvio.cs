namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// O formulário que já recebeu envio de candidato fica no documento: retirá-lo apagaria o que o candidato respondeu
/// e o que as regras citam. O rascunho não conta como envio. Uma recusa por formulário retirado, em ordem ordinal do
/// código (ADR-0125); o formulário retirado não tem posição no documento, e a recusa aponta a coleção dos formulários.
/// </summary>
public static class ConferenciaDosFormulariosComEnvio
{
    private const string FormulariosNoDocumento = "formularios";

    public static IReadOnlyList<FieldError> Conferir(IEnumerable<CodigoDoFormulario> formulariosNovos, FatosDaExecucao fatos)
    {
        ArgumentNullException.ThrowIfNull(formulariosNovos);
        ArgumentNullException.ThrowIfNull(fatos);

        HashSet<CodigoDoFormulario> novos = [.. formulariosNovos];

        return
        [
            .. fatos.FormulariosComEnvio
                .Where(codigo => !novos.Contains(codigo))
                .OrderBy(static codigo => codigo, CodigoDoFormulario.OrdemOrdinal)
                .Select(Recusa),
        ];
    }

    private static FieldError Recusa(CodigoDoFormulario codigo) => new(
        FormulariosNoDocumento,
        new DomainError(RetificacaoErrorCodes.FormularioComEnvioNaoSai, $"O formulário '{codigo.Valor}' já recebeu envio de candidato e não sai do documento."));
}
