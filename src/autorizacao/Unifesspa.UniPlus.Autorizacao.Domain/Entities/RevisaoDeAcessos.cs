namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Concessão ativa de perfil pessoal ou sensível que entra na revisão.</summary>
/// <param name="BeneficiarioTemProtecaoDeDados">O beneficiário tem hoje concessão ativa do perfil da proteção de dados, seja qual for o perfil desta concessão.</param>
public sealed record ConcessaoARever(Guid ConcessaoId, bool BeneficiarioTemProtecaoDeDados);

/// <summary>
/// Revisão periódica dos acessos a dado pessoal ou sensível (ADR-0152): um item por concessão ativa dessa natureza no
/// instante da abertura. A proteção de dados decide os itens; os da própria proteção de dados são decididos pelos
/// gestores de acessos.
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

    public static RevisaoDeAcessos Abrir(Identidade abertaPor, IEnumerable<ConcessaoARever> concessoes, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(abertaPor);
        ArgumentNullException.ThrowIfNull(concessoes);

        RevisaoDeAcessos revisao = new()
        {
            AbertaEm = instante,
            AbertaPorEmissor = abertaPor.Emissor,
            AbertaPorSub = abertaPor.Sub,
            AbertaPorNome = abertaPor.Nome,
        };

        revisao._itens.AddRange(concessoes.DistinctBy(static c => c.ConcessaoId).Select(c => ItemDaRevisao.Para(revisao.Id, c)));

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

    /// <summary>Mantém a concessão ativa e registra a decisão no item.</summary>
    public Result Confirmar(Guid itemId, Concessao concessao, Identidade decisor, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(decisor);

        Result<ItemDaRevisao> item = ItemDecidivel(itemId, concessao, decisor, instante);
        if (item.IsFailure)
        {
            return Result.Failure(item.Error!);
        }

        item.Value!.Decidir(ResultadoDoItemDaRevisao.Confirmada, decisor, instante);

        return Result.Success();
    }

    /// <summary>
    /// Revoga a concessão e registra a decisão no item. A recusa por item decidido, concessão encerrada ou decisor que é
    /// o beneficiário sai antes da do motivo, que a revogação da concessão valida.
    /// </summary>
    public Result Revogar(Guid itemId, Concessao concessao, Identidade decisor, string? motivo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(decisor);

        Result<ItemDaRevisao> item = ItemDecidivel(itemId, concessao, decisor, instante);
        if (item.IsFailure)
        {
            return Result.Failure(item.Error!);
        }

        Result revogada = concessao.Revogar(decisor, motivo, instante);
        if (revogada.IsFailure)
        {
            return revogada;
        }

        item.Value!.Decidir(ResultadoDoItemDaRevisao.Revogada, decisor, instante);

        return Result.Success();
    }

    private Result<ItemDaRevisao> ItemDecidivel(Guid itemId, Concessao concessao, Identidade decisor, DateTimeOffset instante)
    {
        ItemDaRevisao? item = _itens.FirstOrDefault(i => i.Id == itemId && i.ConcessaoId == concessao.Id);
        if (item is null)
        {
            return Result<ItemDaRevisao>.Failure(new DomainError(
                RevisaoErrorCodes.ItemNaoEncontrado, "Item da revisão não encontrado."));
        }

        if (!item.Pendente)
        {
            return Result<ItemDaRevisao>.Failure(new DomainError(
                RevisaoErrorCodes.ItemJaDecidido, "O item da revisão já foi decidido."));
        }

        if (!concessao.EstaAtiva(instante))
        {
            return Result<ItemDaRevisao>.Failure(new DomainError(
                RevisaoErrorCodes.ConcessaoNaoAtiva,
                "A concessão deste item já não está ativa: foi revogada ou venceu fora da revisão."));
        }

        if (decisor.Equals(new Identidade(concessao.BeneficiarioEmissor, concessao.BeneficiarioSub)))
        {
            return Result<ItemDaRevisao>.Failure(new DomainError(
                RevisaoErrorCodes.DecisorEBeneficiario, "Ninguém decide o item da própria concessão."));
        }

        return Result<ItemDaRevisao>.Success(item);
    }
}
