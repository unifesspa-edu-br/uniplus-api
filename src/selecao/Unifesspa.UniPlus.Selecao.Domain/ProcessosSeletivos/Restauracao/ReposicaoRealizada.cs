namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A reposição da configuração congelada já feita nas partes que continuam vivas (atualizadas no lugar,
/// por causa do identity map do EF), com o que ainda falta aplicar: a troca nas coleções e a única âncora
/// que o grafo não traz pronta, a referência temporal com a fase remapeada para a que ficou viva.
/// </summary>
internal sealed record ReposicaoRealizada(MudancaDaConfiguracao Mudanca, ReferenciaTemporalFatos? ReferenciaTemporalFatos);
