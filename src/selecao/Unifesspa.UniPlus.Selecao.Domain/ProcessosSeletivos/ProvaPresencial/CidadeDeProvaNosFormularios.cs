namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A cidade de prova nos formulários do processo (UNI-REQ-0407, ADR-0171): com prova presencial, algum
/// formulário da inscrição a pede a todo candidato, e a escolha só aceita município declarado; sem prova
/// presencial, ou ainda por declarar, nenhum formulário a pede. Quem chama diz quais campos coletam a
/// cidade de prova, pela origem dos valores, sem que a regra nomeie o fato.
/// </summary>
public static class CidadeDeProvaNosFormularios
{
    private const string CampoDosFormularios = "formularios";

    public static IReadOnlyList<FieldError> Conferir(
        DeclaracaoDaProvaPresencial? declaracao,
        IReadOnlyList<FormularioDeclarado> formularios,
        IEnumerable<FaseCronograma> cronograma,
        IReadOnlyList<CampoDoFormulario> camposDaCidadeDeProva)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(camposDaCidadeDeProva);

        return declaracao is { TemProvaPresencial: true }
            ? ConferirComProva(formularios, cronograma, camposDaCidadeDeProva)
            : ConferirSemProva(camposDaCidadeDeProva);
    }

    private static List<FieldError> ConferirComProva(
        IReadOnlyList<FormularioDeclarado> formularios, IEnumerable<FaseCronograma> cronograma, IReadOnlyList<CampoDoFormulario> campos)
    {
        HashSet<CodigoDoFormulario> daInscricao = [.. FormulariosDaInscricao.De(formularios, cronograma).Select(static formulario => formulario.Codigo)];
        return campos.Any(campo => campo.PedidoATodoCandidato && daInscricao.Contains(campo.Formulario))
            ? []
            : [new FieldError(CampoDosFormularios, new DomainError(
                ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva,
                "O processo tem prova presencial, e o formulário de inscrição não pede a cidade de prova a todo candidato: o campo é obrigatório sempre, sem exibição condicional, fora de grupo e numa seção que não se esconde."))];
    }

    private static List<FieldError> ConferirSemProva(IReadOnlyList<CampoDoFormulario> campos) =>
        campos.Count == 0
            ? []
            : [new FieldError(CampoDosFormularios, new DomainError(
                ProvaPresencialErrorCodes.CidadeDeProvaSemProvaPresencial,
                "Um formulário pede a cidade de prova, mas o processo não declara prova presencial: retire o campo ou declare a prova presencial."))];
}
