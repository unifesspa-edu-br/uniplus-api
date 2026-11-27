namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As recusas de quem julga o recurso. Citam a fase pelo nome, a etapa pela posição, a banca pelo rótulo do tipo e pela posição
/// entre as do mesmo tipo, e o papel e o tipo de decisão pelo rótulo fixo; nunca o nome da etapa nem o que o autor digitou.
/// </summary>
internal static class RecusasDoRecurso
{
    internal static DomainError SemBancaNaFase(CitacaoDaFase fase, TipoMotivoDeDecisao? tipoDeDecisao) =>
        tipoDeDecisao is { } tipo
            ? new(
                BancaErrorCodes.RecursoSemBanca,
                $"A fase {fase} admite recurso de {tipo.Rotulo()}, e nenhuma banca prevista o julga em 1ª instância: a fase não tem banca com o papel '{PapelQueJulga}', e nem a fase nem as etapas dela têm banca que tenha avaliado esse item. Acrescente uma dessas bancas.")
            : SemProcedimento($"fase {fase}");

    internal static DomainError SemBancaNaEtapa(int posicao, CitacaoDaFase? fase, TipoMotivoDeDecisao? tipoDeDecisao) =>
        tipoDeDecisao is { } tipo
            ? new(
                BancaErrorCodes.RecursoSemBanca,
                $"A etapa na posição {posicao + 1} admite recurso de {tipo.Rotulo()}, e nenhuma banca prevista o julga em 1ª instância: nem a etapa nem {CitacaoDaFase.ComArtigo(fase)}, em que ela acontece, têm banca com o papel '{PapelQueJulga}', e a etapa não tem banca que tenha avaliado esse item. Acrescente uma dessas bancas.")
            : SemProcedimento($"etapa na posição {posicao + 1}");

    internal static DomainError InstanciaSuperiorNaoAdmitida(string onde, BancaDeclarada avaliadora, TipoMotivoDeDecisao tipoDeDecisao) =>
        new(
            BancaErrorCodes.TipoNaoAdmiteInstanciaSuperior,
            $"O recurso da {onde} admite instância superior, mas o recurso do item avaliado pela banca '{avaliadora.Citacao}' ({tipoDeDecisao.Rotulo()}) se encerra na 1ª instância. Declare que o recurso não admite instância superior.");

    private static DomainError SemProcedimento(string onde) =>
        new(
            BancaErrorCodes.RecursoSemBanca,
            $"A {onde} admite recurso sem procedimento declarado, e só uma banca com o papel '{PapelQueJulga}' pode julgá-lo. Acrescente essa banca.");

    private static string PapelQueJulga => PapelDaBanca.JulgaAPrimeiraInstancia.Rotulo();
}
