namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>A classificação nova e o que a troca faz além de gravá-la: as edições do ENEM que deixam de existir.</summary>
internal sealed record PlanoDaClassificacao(ConfiguracaoClassificacao Classificacao, MudancaDaConfiguracao Mudanca);
