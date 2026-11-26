namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere a coleta da cidade de prova nos formulários do processo: com prova presencial e formulário da
/// inscrição, algum item da origem "municípios da prova presencial" é pedido a todo candidato; sem prova
/// presencial, nenhum formulário a coleta; com a prova ainda por declarar, não há o que conferir.
/// </summary>
public static class CidadeDeProvaNosFormularios
{
    private const string CampoDosFormularios = "formularios";

    public static IReadOnlyList<FieldError> Conferir(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        DomainError? recusa = configuracao.ProvaPresencial switch
        {
            { TemProvaPresencial: true } => InscricaoSemCidadeDeProva(configuracao),
            { TemProvaPresencial: false } => CidadeDeProvaSemProvaPresencial(configuracao),
            _ => null,
        };
        return recusa is null ? [] : [new FieldError(CampoDosFormularios, recusa)];
    }

    private static DomainError? InscricaoSemCidadeDeProva(ConfiguracaoDeclarada configuracao) =>
        configuracao.DaInscricao() is { Count: > 0 } formulariosDaInscricao
            && !formulariosDaInscricao.Any(formulario => CamposDaCidadeDeProva(configuracao).Any(formulario.PedeATodoCandidato))
            ? new DomainError(
                ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva,
                "O formulário de inscrição deve pedir a cidade de prova a todo candidato.")
            : null;

    private static DomainError? CidadeDeProvaSemProvaPresencial(ConfiguracaoDeclarada configuracao) =>
        CamposDaCidadeDeProva(configuracao).Any()
            ? new DomainError(
                ProvaPresencialErrorCodes.CidadeDeProvaSemProvaPresencial,
                "O formulário pede a cidade de prova, mas o processo não tem prova presencial.")
            : null;

    private static IEnumerable<FatoColetado> CamposDaCidadeDeProva(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.Itens.Concat(configuracao.Coleta.GruposColetados.SelectMany(static grupo => grupo.Subitens))
            .Where(static item => item.OrigemValores == OrigemValoresColeta.MunicipiosDaProvaPresencial);
}
