namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Guarda o que o operador já transcreveu do Diário Oficial no passo de Revisão, para que um
/// recarregamento não o apague.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Conteudo"/> atravessa sem interpretação.</b> Seleção não conhece os campos do
/// ato (ADR-0108) e não ganha esse conhecimento por guardar o bloco — o documento é opaco na
/// ida e na volta. É por isso que acrescentar um campo ao ato não muda uma linha deste lado.
/// </para>
/// <para>
/// <b>É substituição, não mesclagem.</b> O cliente manda a seção inteira a cada gravação,
/// então um campo que ele apagou tem de sumir daqui também.
/// </para>
/// <para>
/// O dono do rascunho não é input: vem do contexto autenticado, como o ator de toda operação
/// administrativa (ADR-0033).
/// </para>
/// </remarks>
/// <param name="Versao">
/// Versão do <b>formato</b> em que o cliente escreveu, para que um cliente futuro saiba
/// distinguir o que consegue reidratar do que teria de traduzir pela metade.
/// </param>
public sealed record SalvarRascunhoDaPublicacaoCommand(
    Guid ProcessoSeletivoId,
    int Versao,
    JsonElement Conteudo) : ICommand<Result>;
