namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A retificação não desfaz o ciclo recursal que o preliminar já publicado abriu e que o definitivo ainda não
/// fechou (UNI-REQ-0157, UNI-REQ-0282).
/// </summary>
/// <remarks>
/// <para>
/// A matéria, que é o tipo de ato do item, tem o ciclo aberto quando o preliminar da versão publicada foi
/// publicado e nenhum dos definitivos que o fecham nela foi. Enquanto está aberto, o preliminar e esses
/// definitivos precisam continuar no cronograma novo, com os mesmos definitivos fechando o ciclo do preliminar.
/// Retirar um dos dois, mover o definitivo para outro lugar ou trocar a fase concluinte do preliminar desfaz o
/// ciclo.
/// </para>
/// <para>
/// O pareamento entre preliminar e definitivo vem de <see cref="PreliminaresDaMateria"/>, que o cronograma também
/// consulta para liberar o definitivo. O item se identifica, na versão publicada e no cronograma novo, por
/// <see cref="IdentidadeDoItemDoEdital"/> (lugar, tipo de ato e papel), e não pelo <c>Id</c> do produto, que muda
/// sem que o ciclo mude quando o descarte de uma sessão recria os produtos. As recusas se acumulam (ADR-0125), uma
/// por matéria desfeita, em ordem ordinal do código do tipo de ato.
/// </para>
/// </remarks>
public static class ConferenciaDoCicloRecursalAberto
{
    /// <param name="fasesPublicadas">As fases da versão publicada vigente.</param>
    /// <param name="etapasPublicadas">As etapas da versão publicada vigente.</param>
    /// <param name="fasesNovas">As fases do cronograma que a retificação propõe.</param>
    /// <param name="etapasNovas">As etapas do cronograma que a retificação propõe.</param>
    /// <param name="itensPublicados">As identidades, da versão publicada, cuja primeira publicação existe.</param>
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyCollection<FaseCronograma> fasesPublicadas,
        IReadOnlyCollection<EtapaProcesso> etapasPublicadas,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas,
        IReadOnlySet<IdentidadeDoItemDoEdital> itensPublicados)
    {
        ArgumentNullException.ThrowIfNull(fasesPublicadas);
        ArgumentNullException.ThrowIfNull(etapasPublicadas);
        ArgumentNullException.ThrowIfNull(fasesNovas);
        ArgumentNullException.ThrowIfNull(etapasNovas);
        ArgumentNullException.ThrowIfNull(itensPublicados);

        return [.. MateriasDesfeitas(fasesPublicadas, etapasPublicadas, fasesNovas, etapasNovas, itensPublicados)
            .Order(StringComparer.Ordinal)
            .Select(Recusa)];
    }

    private static IEnumerable<string> MateriasDesfeitas(
        IReadOnlyCollection<FaseCronograma> fasesPublicadas,
        IReadOnlyCollection<EtapaProcesso> etapasPublicadas,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas,
        IReadOnlySet<IdentidadeDoItemDoEdital> itensPublicados)
    {
        Dictionary<Guid, IdentidadeDoItemDoEdital> novos = IdentidadeDoItemDoEdital.Dos(fasesNovas, etapasNovas);

        return CiclosAbertos(fasesPublicadas, etapasPublicadas, itensPublicados)
            .Where(ciclo => !MantemOCiclo(ciclo, novos, fasesNovas, etapasNovas))
            .Select(static ciclo => ciclo.Preliminar.AtoCodigo)
            .Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<CicloAberto> CiclosAbertos(
        IReadOnlyCollection<FaseCronograma> fasesPublicadas,
        IReadOnlyCollection<EtapaProcesso> etapasPublicadas,
        IReadOnlySet<IdentidadeDoItemDoEdital> itensPublicados)
    {
        Dictionary<Guid, IdentidadeDoItemDoEdital> publicados = IdentidadeDoItemDoEdital.Dos(fasesPublicadas, etapasPublicadas);

        return publicados
            .Where(i => i.Value.Papel == PapelProdutoFase.Preliminar && itensPublicados.Contains(i.Value))
            .Select(i => new CicloAberto(
                i.Value,
                [.. PreliminaresDaMateria.DefinitivosQueFecham(fasesPublicadas, etapasPublicadas, i.Key).Select(d => publicados[d])]))
            .Where(ciclo => !ciclo.Definitivos.Any(itensPublicados.Contains));
    }

    private static bool MantemOCiclo(
        CicloAberto ciclo,
        Dictionary<Guid, IdentidadeDoItemDoEdital> novos,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas)
    {
        Guid? preliminarNovo = novos.Where(i => i.Value == ciclo.Preliminar).Select(static i => (Guid?)i.Key).FirstOrDefault();
        if (preliminarNovo is null)
        {
            return false;
        }

        HashSet<IdentidadeDoItemDoEdital> quemFecha =
        [
            .. PreliminaresDaMateria.DefinitivosQueFecham(fasesNovas, etapasNovas, preliminarNovo.Value).Select(d => novos[d]),
        ];

        return ciclo.Definitivos.All(quemFecha.Contains);
    }

    private static FieldError Recusa(string atoCodigo) => new(
        $"cronograma.ciclosRecursais[{atoCodigo}]",
        new DomainError(
            ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto,
            $"A retificação desfaz o ciclo de recurso da matéria '{atoCodigo}': o resultado preliminar já foi publicado, o definitivo ainda não, "
            + "e a configuração nova retira ou muda de ciclo um dos dois. Publique o definitivo antes de retificar, ou mantenha o preliminar e o definitivo da matéria no cronograma."));

    private sealed record CicloAberto(IdentidadeDoItemDoEdital Preliminar, IReadOnlyList<IdentidadeDoItemDoEdital> Definitivos);
}
