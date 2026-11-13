namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Quem decide um item da revisão e a condição do beneficiário lida, sob a trava do papel, no instante da decisão.</summary>
/// <param name="Pessoa">Quem decide.</param>
/// <param name="BeneficiarioERepresentante">O beneficiário tem, no instante da decisão, concessão vigente de Representante do controlador.</param>
public sealed record DecisorDoItem(Identidade Pessoa, bool BeneficiarioERepresentante)
{
    /// <summary>A governança que decide o item, pela condição do beneficiário.</summary>
    public TipoDeGovernanca Como => ItemDaRevisao.QuemDecide(BeneficiarioERepresentante);
}

/// <summary>
/// Revisão periódica dos acessos a dado pessoal ou sensível (ADR-0152): um item por concessão ativa dessa natureza no
/// instante da abertura. O Representante do controlador decide os itens; os de quem é Representante no instante da
/// decisão são decididos pelos gestores de acessos (ADR-0162).
/// </summary>
/// <remarks>
/// <b>A situação não é gravada.</b> A revisão está em andamento enquanto algum item sem decisão trata de concessão ainda
/// ativa (<see cref="EstaEmAndamento"/>): a concessão revogada ou vencida fora da revisão deixa de pedir decisão. A
/// revisão aberta sem nenhum item nasce concluída.
/// </remarks>
public sealed class RevisaoDeAcessos : EntityBase
{
    private readonly List<ItemDaRevisao> _itens = [];

    public DateTimeOffset AbertaEm { get; private set; }

    public string AbertaPorEmissor { get; private set; } = string.Empty;

    public string AbertaPorSub { get; private set; } = string.Empty;

    public string? AbertaPorNome { get; private set; }

    public IReadOnlyList<ItemDaRevisao> Itens => _itens;

    public Identidade AbertaPor => new(AbertaPorEmissor, AbertaPorSub, AbertaPorNome);

    // Construtor de materialização do EF Core.
    private RevisaoDeAcessos()
    {
    }

    /// <summary>Abre a revisão com um item por concessão, sem fixar quem decide cada um.</summary>
    public static RevisaoDeAcessos Abrir(Identidade abertaPor, IEnumerable<Guid> concessaoIds, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(abertaPor);
        ArgumentNullException.ThrowIfNull(concessaoIds);

        RevisaoDeAcessos revisao = new()
        {
            AbertaEm = instante,
            AbertaPorEmissor = abertaPor.Emissor,
            AbertaPorSub = abertaPor.Sub,
            AbertaPorNome = abertaPor.Nome,
        };

        revisao._itens.AddRange(concessaoIds.Distinct().Select(id => ItemDaRevisao.Para(revisao.Id, id)));

        return revisao;
    }

    /// <summary>Quantos itens sem decisão tratam de concessão que ainda está ativa.</summary>
    /// <param name="concessoesAtivas">Concessões da revisão que estão ativas agora.</param>
    public int Pendentes(IReadOnlySet<Guid> concessoesAtivas)
    {
        ArgumentNullException.ThrowIfNull(concessoesAtivas);

        return _itens.Count(i => i.Pendente && concessoesAtivas.Contains(i.ConcessaoId));
    }

    /// <summary>Algum item sem decisão trata de concessão que ainda está ativa.</summary>
    public bool EstaEmAndamento(IReadOnlySet<Guid> concessoesAtivas) => Pendentes(concessoesAtivas) > 0;

    /// <summary>Mantém a concessão ativa e registra a decisão no item, com a governança que a tomou.</summary>
    public Result Confirmar(Guid itemId, Concessao concessao, DecisorDoItem decisor, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(decisor);

        Result<ItemDaRevisao> item = ItemDecidivel(itemId, concessao, decisor.Pessoa, instante);
        item.Value?.Decidir(ResultadoDoItemDaRevisao.Confirmada, decisor.Pessoa, decisor.Como, instante);

        return item.IsFailure ? Result.Failure(item.Error!) : Result.Success();
    }

    /// <summary>
    /// Revoga a concessão e registra a decisão no item, com a governança que a tomou. A recusa por item decidido,
    /// concessão encerrada ou decisor que é o beneficiário sai antes da do motivo, que a revogação da concessão valida.
    /// </summary>
    public Result Revogar(Guid itemId, Concessao concessao, DecisorDoItem decisor, string? motivo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(decisor);

        Result<ItemDaRevisao> item = ItemDecidivel(itemId, concessao, decisor.Pessoa, instante);
        Result revogada = item.IsFailure ? Result.Failure(item.Error!) : concessao.Revogar(new AtoDoAgente(decisor.Pessoa, instante), motivo);
        if (revogada.IsSuccess)
        {
            item.Value!.Decidir(ResultadoDoItemDaRevisao.Revogada, decisor.Pessoa, decisor.Como, instante);
        }

        return revogada;
    }

    private Result<ItemDaRevisao> ItemDecidivel(Guid itemId, Concessao concessao, Identidade decisor, DateTimeOffset instante)
    {
        ItemDaRevisao? item = _itens.FirstOrDefault(i => i.Id == itemId && i.ConcessaoId == concessao.Id);
        DomainError? recusa = item is null
            ? new DomainError(RevisaoErrorCodes.ItemNaoEncontrado, "Item da revisão não encontrado.")
            : RecusaDaDecisao(item, concessao, decisor, instante);

        return recusa is null ? Result<ItemDaRevisao>.Success(item!) : Result<ItemDaRevisao>.Failure(recusa);
    }

    private static DomainError? RecusaDaDecisao(ItemDaRevisao item, Concessao concessao, Identidade decisor, DateTimeOffset instante)
    {
        if (!item.Pendente)
        {
            return new DomainError(RevisaoErrorCodes.ItemJaDecidido, "O item da revisão já foi decidido.");
        }

        if (!concessao.EstaAtiva(instante))
        {
            return new DomainError(RevisaoErrorCodes.ConcessaoNaoAtiva, "A concessão deste item já não está ativa: foi revogada ou venceu fora da revisão.");
        }

        return decisor.Equals(new Identidade(concessao.BeneficiarioEmissor, concessao.BeneficiarioSub))
            ? new DomainError(RevisaoErrorCodes.DecisorEBeneficiario, "Ninguém decide o item da própria concessão.")
            : null;
    }
}
