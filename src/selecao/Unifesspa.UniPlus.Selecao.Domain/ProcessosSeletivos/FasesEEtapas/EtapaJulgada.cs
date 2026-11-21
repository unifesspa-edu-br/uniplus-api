namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

/// <summary>Uma etapa e a posição dela na lista das etapas, que as mensagens e os campos das recusas citam.</summary>
internal sealed record EtapaJulgada(EtapaDeclarada Etapa, int Posicao);
