namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que uma operação sobre a versão de um perfil produziu: a recusa, se houve, e se o agregado mudou de estado mesmo
/// assim, o que quem chama precisa gravar. É a diferença entre recusar sem tocar em nada e recusar depois de
/// materializar o fim do prazo ou o descarte das aprovações.
/// </summary>
public sealed record DesfechoDaVersao
{
    private static readonly IReadOnlyList<DecisaoDaVersao> SemDecisoes = [];

    /// <summary>A recusa da operação; nulo quando ela foi concluída.</summary>
    public DomainError? Erro { get; private init; }

    /// <summary>O agregado mudou e precisa ser gravado, ainda que a operação tenha sido recusada.</summary>
    public bool Gravar { get; private init; }

    /// <summary>A troca de vigência que a operação fez; nula quando ela não trocou a versão em vigor.</summary>
    public TrocaDeVigencia? Troca { get; private init; }

    /// <summary>O recálculo da classe que descartou as aprovações do ciclo; nulo quando a classe não mudou.</summary>
    public RecalculoDaAprovacao? Recalculo { get; private init; }

    /// <summary>As decisões do ciclo que o recálculo descartou.</summary>
    public IReadOnlyList<DecisaoDaVersao> Descartadas { get; private init; } = SemDecisoes;

    /// <summary>A versão pendente que a remoção cancelou por contenção; nula quando não havia.</summary>
    public VersaoDoPerfil? Cancelada { get; private init; }

    /// <summary>A versão pendente que a operação criou; nula quando ela não criou nenhuma.</summary>
    public VersaoDoPerfil? Criada { get; private init; }

    public bool Concluida => Erro is null;

    /// <summary>Recusada sem alterar nada.</summary>
    public static DesfechoDaVersao Recusa(DomainError erro) => new() { Erro = erro };

    /// <summary>Recusada depois de alterar o agregado, como o fim do prazo que a operação materializou.</summary>
    public static DesfechoDaVersao RecusaGravada(DomainError erro) => new() { Erro = erro, Gravar = true };

    public static DesfechoDaVersao AprovacoesDescartadas(
        DomainError erro, RecalculoDaAprovacao recalculo, IReadOnlyList<DecisaoDaVersao> descartadas) =>
        new() { Erro = erro, Gravar = true, Recalculo = recalculo, Descartadas = descartadas };

    public static DesfechoDaVersao Concluido(TrocaDeVigencia? troca = null) => new() { Gravar = true, Troca = troca };

    /// <summary>Concluída com a versão pendente que a proposta criou, sem troca de vigência.</summary>
    public static DesfechoDaVersao ConcluidoComProposta(VersaoDoPerfil criada) => new() { Gravar = true, Criada = criada };

    /// <summary>Concluída pela remoção que vale na hora e, quando a proposta mudava mais, pela pendente que ela criou.</summary>
    public static DesfechoDaVersao ConcluidoPelaRemocao(TrocaDeVigencia troca, VersaoDoPerfil? cancelada, VersaoDoPerfil? criada = null) =>
        new() { Gravar = true, Troca = troca, Cancelada = cancelada, Criada = criada };
}
