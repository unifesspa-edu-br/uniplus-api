namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O que a montagem da árvore de exigências precisa já lido: o processo, o cadastro de tipos de documento,
/// os modelos citados pelas folhas e, quando alguma folha declara gatilho ou fato comprovado, o catálogo de
/// fatos. Sem folha com gatilho o catálogo não é lido e vem nulo.
/// </summary>
internal sealed record ContextoDaMontagemDasExigencias(
    ProcessoSeletivo Processo,
    ITipoDocumentoReader TiposDeDocumento,
    IReadOnlyDictionary<Guid, ModeloDeDocumento> Modelos,
    FatosDoGatilho? Fatos);
