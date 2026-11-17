namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A obrigatoriedade dos formulários na retificação (UNI-REQ-0211, UNI-REQ-0144): o formulário obrigatório que já
/// recebeu envio não passa a facultativo (a retirada dele é recusa de <see cref="ConferenciaDosFormulariosComEnvio"/>).
/// O obrigatório sem envio que sai do documento, passa a facultativo ou muda de fase diminui o conjunto da fase de origem, e a conferência
/// devolve, por fase, as conclusões decorrentes, com os obrigatórios que restam, para quem passa a cumpri-la
/// (<see cref="ConclusaoDaFase"/>). As recusas se acumulam, uma por formulário e em ordem ordinal do código; as
/// conclusões, em ordem de fase de origem, só valem quando não há recusa. A recusa aponta o formulário pela posição dele na lista do
/// documento novo, que chega na ordem do documento.
/// </summary>
public static class ConferenciaDaObrigatoriedadeDosFormularios
{
    public static ResultadoDaObrigatoriedade Conferir(
        IReadOnlyCollection<FormularioNoDocumento> formulariosDaBase,
        IReadOnlyList<FormularioNoDocumento> formulariosNovos,
        FatosDaExecucao fatos)
    {
        ArgumentNullException.ThrowIfNull(formulariosDaBase);
        ArgumentNullException.ThrowIfNull(formulariosNovos);
        ArgumentNullException.ThrowIfNull(fatos);

        FormularioNoDocumento[] deixaramDeSerObrigatorios = DeixaramDeSerObrigatorios(formulariosDaBase, formulariosNovos);
        HashSet<CodigoDoFormulario> facultativosNaNova = [.. formulariosNovos.Where(static f => !f.Obrigatorio).Select(static f => f.Codigo)];

        return new ResultadoDaObrigatoriedade(
            [.. deixaramDeSerObrigatorios.Where(f => fatos.FormulariosComEnvio.Contains(f.Codigo) && facultativosNaNova.Contains(f.Codigo)).Select(formulario => Recusa(formulario, formulariosNovos))],
            ConclusoesDecorrentes(deixaramDeSerObrigatorios.Where(f => !fatos.FormulariosComEnvio.Contains(f.Codigo)), formulariosNovos));
    }

    private static FormularioNoDocumento[] DeixaramDeSerObrigatorios(
        IReadOnlyCollection<FormularioNoDocumento> formulariosDaBase, IReadOnlyCollection<FormularioNoDocumento> formulariosNovos)
    {
        HashSet<(CodigoDoFormulario, Guid)> aindaObrigatorios = [.. formulariosNovos.Where(static f => f.Obrigatorio).Select(static f => (f.Codigo, f.FaseCanonicaOrigemId))];

        return
        [
            .. formulariosDaBase
                .Where(f => f.Obrigatorio && !aindaObrigatorios.Contains((f.Codigo, f.FaseCanonicaOrigemId)))
                .OrderBy(static f => f.Codigo, CodigoDoFormulario.OrdemOrdinal),
        ];
    }

    private static IReadOnlyList<ConclusaoDecorrente> ConclusoesDecorrentes(
        IEnumerable<FormularioNoDocumento> semEnvio, IReadOnlyCollection<FormularioNoDocumento> formulariosNovos) =>
        [
            .. semEnvio
                .Select(static f => f.FaseCanonicaOrigemId)
                .Distinct()
                .Order()
                .Select(fase => new ConclusaoDecorrente(
                    fase,
                    [.. formulariosNovos.Where(f => f.Obrigatorio && f.FaseCanonicaOrigemId == fase).Select(static f => f.Codigo).Order(CodigoDoFormulario.OrdemOrdinal)])),
        ];

    private static FieldError Recusa(FormularioNoDocumento formulario, IReadOnlyList<FormularioNoDocumento> formulariosNovos) => new(
        $"formularios[{PosicaoNoDocumento(formulario, formulariosNovos)}]",
        new DomainError(
            RetificacaoErrorCodes.FormularioObrigatorioComEnvioNaoViraFacultativo,
            $"O formulário '{formulario.Codigo.Valor}' já recebeu envio de candidato e não passa de obrigatório a facultativo."));

    private static int PosicaoNoDocumento(FormularioNoDocumento formulario, IReadOnlyList<FormularioNoDocumento> formulariosNovos) =>
        formulariosNovos.Select(static (novo, posicao) => (novo.Codigo, posicao)).First(par => par.Codigo == formulario.Codigo).posicao;
}

/// <summary>As recusas da obrigatoriedade e as conclusões decorrentes que a retificação registra se não houver recusa.</summary>
public sealed record ResultadoDaObrigatoriedade(IReadOnlyList<FieldError> Recusas, IReadOnlyList<ConclusaoDecorrente> ConclusoesDecorrentes);
