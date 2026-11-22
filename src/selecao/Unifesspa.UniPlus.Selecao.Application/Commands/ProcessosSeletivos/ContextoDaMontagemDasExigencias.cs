namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O que a montagem das regras de comprovação precisa já lido: o processo, o cadastro de tipos de documento,
/// os modelos citados pelas regras de documento e, quando uma regra declara gatilho ou fato comprovado, o catálogo de
/// fatos. Sem regra de documento com gatilho o catálogo não é lido e vem nulo.
/// </summary>
internal sealed record ContextoDaMontagemDasExigencias(
    ProcessoSeletivo Processo,
    ITipoDocumentoReader TiposDeDocumento,
    IReadOnlyDictionary<Guid, ModeloDeDocumento> Modelos,
    FatosDoGatilho? Fatos);
