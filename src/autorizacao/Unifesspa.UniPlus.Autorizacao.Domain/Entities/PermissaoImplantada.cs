namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que o catálogo registrava de uma permissão ao ser implantado: a classificação, o escopo e a reserva à governança. É a
/// linha de <see cref="ImplantacaoDasPermissoes"/>, só acrescentada.
/// </summary>
public sealed class PermissaoImplantada
{
    public Guid ImplantacaoId { get; private init; }

    public string Codigo { get; private init; } = string.Empty;

    public ClassificacaoDeProtecao Classificacao { get; private init; }

    public bool DeUnidade { get; private init; }

    public bool Reservada { get; private init; }

    // Construtor de materialização do EF Core.
    private PermissaoImplantada()
    {
    }

    internal PermissaoImplantada(Guid implantacaoId, string codigo, ClassificacaoDeProtecao classificacao, bool deUnidade, bool reservada)
    {
        ImplantacaoId = implantacaoId;
        Codigo = codigo;
        Classificacao = classificacao;
        DeUnidade = deUnidade;
        Reservada = reservada;
    }
}
