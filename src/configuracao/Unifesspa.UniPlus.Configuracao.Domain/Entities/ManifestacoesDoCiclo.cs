namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os eventos de manifestação técnica de um item num ciclo de aprovação, com as vigentes derivadas: as que nenhum
/// evento substituiu (UNI-REQ-0373). Manifestações de pessoas diferentes coexistem; a nova manifestação da mesma pessoa
/// substitui a vigente dela sem apagá-la.
/// </summary>
public sealed class ManifestacoesDoCiclo
{
    private readonly List<ManifestacaoDaProtecaoDoCatalogo> _eventos;

    private ManifestacoesDoCiclo(ItemDaProtecao item, CicloDaAprovacao ciclo, List<ManifestacaoDaProtecaoDoCatalogo> eventos)
    {
        Item = item;
        Ciclo = ciclo;
        _eventos = eventos;
    }

    public ItemDaProtecao Item { get; }

    public CicloDaAprovacao Ciclo { get; }

    /// <summary>As manifestações vigentes do ciclo, das mais antigas para as mais recentes; uma cópia.</summary>
    public IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> Vigentes =>
        [.. _eventos.Where(e => !_eventos.Any(outro => outro.SubstituiId == e.Id)).OrderBy(static e => e.Instante).ThenBy(static e => e.PosicaoNoInstante)];

    /// <summary>Se alguma manifestação vigente tem parecer recomenda ajuste ou inadequada.</summary>
    public bool HaDesfavoravelVigente => Vigentes.Any(static m => m.Desfavoravel);

    /// <summary>Os eventos já gravados do item no ciclo.</summary>
    /// <exception cref="ArgumentException">Se algum evento é de outro item ou de outro ciclo.</exception>
    public static ManifestacoesDoCiclo De(
        ItemDaProtecao item, CicloDaAprovacao ciclo, IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos)
    {
        ArgumentNullException.ThrowIfNull(eventos);

        return eventos.All(e => e.Item == item && e.Ciclo == ciclo)
            ? new ManifestacoesDoCiclo(item, ciclo, [.. eventos])
            : throw new ArgumentException("Há evento de manifestação de outro item ou de outro ciclo.", nameof(eventos));
    }

    /// <summary>
    /// Registra a manifestação de <paramref name="autor"/> no ciclo que ele leu e atualiza o circuito do item na mesma
    /// transação: o instante da manifestação e se, depois dela, há manifestação desfavorável vigente. A manifestação não
    /// muda a situação da aprovação nem a proteção do item (ADR-0162).
    /// </summary>
    /// <returns>
    /// O evento a gravar; falha <c>CicloMudou</c> quando o ciclo lido difere do corrente, <c>DecisaoJaTomada</c> quando o
    /// item já foi decidido no ciclo lido, ou <c>ItemForaDeSolicitacao</c> quando ele ainda não foi solicitado nesse
    /// ciclo. Na falha, nada muda.
    /// </returns>
    /// <exception cref="InvalidOperationException">Se o circuito está em outro ciclo que não o destas manifestações.</exception>
    public Result<ManifestacaoDaProtecaoDoCatalogo> Registrar(
        AprovacaoDaProtecao circuito, CicloDaAprovacao lido, ConteudoDaManifestacao conteudo, AtorDoAto autor, DateTimeOffset instante)
    {
        DomainError? recusa = Recusa(circuito, lido);
        if (recusa is not null)
        {
            return Result<ManifestacaoDaProtecaoDoCatalogo>.Failure(recusa);
        }

        ManifestacaoDaProtecaoDoCatalogo? substituida = Vigentes.FirstOrDefault(m => m.DaMesmaPessoa(autor));
        ManifestacaoDaProtecaoDoCatalogo registrada = ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, Ciclo, conteudo, autor, substituida, instante);
        _eventos.Add(registrada);
        circuito.RegistrarManifestacao(HaDesfavoravelVigente, instante);
        return Result<ManifestacaoDaProtecaoDoCatalogo>.Success(registrada);
    }

    private DomainError? Recusa(AprovacaoDaProtecao circuito, CicloDaAprovacao lido)
    {
        ArgumentNullException.ThrowIfNull(circuito);
        if (circuito.Ciclo != Ciclo)
        {
            throw new InvalidOperationException("O circuito está em outro ciclo que não o das manifestações carregadas.");
        }

        return lido == circuito.Ciclo
            ? RecusaDaSituacao(circuito.Situacao)
            : new(ProtecaoDoCatalogoErrorCodes.CicloMudou, "O ciclo de aprovação do item mudou depois de lido; releia o item.");
    }

    // No ciclo lido, o item pendente nunca foi solicitado nele; o aprovado, o reprovado e o com ajuste recomendado já
    // foram decididos depois da leitura (UNI-REQ-0374).
    private static DomainError? RecusaDaSituacao(SituacaoDaAprovacao situacao) => situacao switch
    {
        SituacaoDaAprovacao.Solicitada => null,
        SituacaoDaAprovacao.Pendente => new(
            ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao, "Só o item que está numa solicitação recebe manifestação técnica."),
        _ => new(ProtecaoDoCatalogoErrorCodes.DecisaoJaTomada, "O item foi decidido depois da leitura; releia o item."),
    };
}
