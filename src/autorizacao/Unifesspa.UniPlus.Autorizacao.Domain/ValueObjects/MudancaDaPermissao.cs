namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>O que mudou numa permissão entre dois catálogos: a classificação e o escopo, cada eixo por conta própria.</summary>
public sealed record MudancaDaPermissao(
    ClassificacaoDeProtecao ClassificacaoAnterior, ClassificacaoDeProtecao ClassificacaoNova, bool DeUnidadeAnterior, bool DeUnidadeNovo)
{
    /// <summary>Algum eixo mudou.</summary>
    public bool Mudou => ClassificacaoAnterior != ClassificacaoNova || DeUnidadeAnterior != DeUnidadeNovo;
}
