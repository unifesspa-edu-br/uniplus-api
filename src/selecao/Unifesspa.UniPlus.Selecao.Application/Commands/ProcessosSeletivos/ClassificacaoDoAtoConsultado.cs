namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

/// <summary>O que a edição apurou sobre o tipo de ato que um produto declara.</summary>
internal abstract record ClassificacaoDoAtoConsultado
{
    /// <summary>
    /// O tipo consta na versão publicada ou tem versão vigente no catálogo: a sua classificação e o nome que a unidade
    /// viu, o da cópia guardada na versão publicada ou, no tipo novo, o do catálogo.
    /// </summary>
    internal sealed record Conhecida(ClassificacaoDoTipoDeAto Classificacao, string Nome) : ClassificacaoDoAtoConsultado;

    /// <summary>O tipo não consta na versão publicada e não tem versão vigente no catálogo de hoje.</summary>
    internal sealed record SemVersaoVigente : ClassificacaoDoAtoConsultado;
}
