namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a conferência do ciclo novo da retificação apurou.</summary>
/// <param name="Recusas">As recusas, acumuladas, cada uma no campo do item declarado ou exigido.</param>
/// <param name="CiclosRecursaisRefeitos">
/// Os itens declarados que têm o resultado preliminar publicado e o ciclo aberto, em ordem ordinal da chave: os
/// ciclos que a retificação refaz.
/// </param>
public sealed record ResultadoDoCicloNovo(
    IReadOnlyList<FieldError> Recusas,
    IReadOnlyList<IdentidadeDoItemDoEdital> CiclosRecursaisRefeitos);
