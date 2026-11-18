namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a retificação recusa numa fase ou numa etapa que já começou: a troca da unidade responsável, a remoção da
/// fase do cronograma e a remoção da etapa. A comparação é sempre com a versão publicada vigente, que
/// <see cref="FasesIniciadas"/> traz, e não com o rascunho anterior. As recusas se acumulam, uma por fase trocada,
/// uma por fase removida e uma por etapa removida (ADR-0125), nessa ordem. A fase trocada, que continua no documento, aponta a
/// unidade dela pela posição da fase na ordem canônica do documento, e não na lista como veio.
/// </summary>
public static class ConferenciaDasFasesIniciadas
{
    // O item retirado não tem posição no documento: a recusa aponta a coleção que traz a fase ou a etapa.
    private const string UnidadeDaFase = "unidadeResponsavel";
    private const string FasesNoDocumento = "cronograma.fases";
    private const string EtapasNoDocumento = "etapas";

    public static IReadOnlyList<FieldError> Conferir(
        FasesIniciadas iniciadas,
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(iniciadas);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            .. RecusasDaTroca(iniciadas, [.. OrdemCanonicaDaConfiguracao.Fases(fases)]),
            .. RecusasDaRemocaoDaFase(iniciadas, fases),
            .. RecusasDaRemocaoDaEtapa(iniciadas, etapas),
        ];
    }

    // A unidade que conduz a fase decide as ações dela, e a fase que já começou tem ato e registro feitos sob a
    // unidade publicada: trocá-la deixaria o que já aconteceu sem a unidade que o conduziu. A comparação é com a
    // versão publicada, e não com o rascunho anterior, para que retirar e reinserir a fase não contorne a regra.
    private static IEnumerable<FieldError> RecusasDaTroca(FasesIniciadas iniciadas, IReadOnlyList<FaseCronograma> fasesNoDocumento) =>
        fasesNoDocumento
            .Select(static (fase, posicao) => (fase, posicao))
            .Where(par => iniciadas.UnidadeTrocada(par.fase))
            .Select(static par => Recusa(
                $"fases[{par.posicao}].{UnidadeDaFase}",
                "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda",
                $"A fase '{par.fase.Codigo}' (ordem {par.fase.Ordem}) já começou, e a unidade responsável por ela não muda."));

    // Remover a fase numa retificação e recolocá-la com outra unidade na seguinte contornaria a recusa da troca. Só conta
    // a fase que a versão publicada vigente ainda traz: a que já saiu de uma versão anterior não é removida agora.
    private static IEnumerable<FieldError> RecusasDaRemocaoDaFase(FasesIniciadas iniciadas, IReadOnlyCollection<FaseCronograma> fases) =>
        iniciadas.CodigosDasFasesRemovidas(fases).Select(static codigo => Recusa(
            FasesNoDocumento,
            "FaseCronograma.FaseIniciadaNaoSaiDoCronograma",
            $"A fase '{codigo}' já começou, e ela não sai do cronograma."));

    private static IEnumerable<FieldError> RecusasDaRemocaoDaEtapa(FasesIniciadas iniciadas, IReadOnlyCollection<EtapaProcesso> etapas) =>
        iniciadas.EtapasRemovidas(etapas).Select(static etapa => Recusa(
            EtapasNoDocumento,
            "FaseCronograma.EtapaIniciadaNaoSaiDoCronograma",
            $"A etapa '{etapa.Nome}' já começou, e ela não sai do cronograma."));

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
