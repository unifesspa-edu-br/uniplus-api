namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// O que se lê do documento antes de gravar o rascunho, sem segurar a trava da linha: as declarações que a leitura copiou do
/// documento e a prova presencial já conferida no Geo.
/// </summary>
/// <param name="Declaracoes">As declarações copiadas do documento, lidas uma vez.</param>
/// <param name="ProvaPresencial">A prova presencial lida e conferida.</param>
public sealed record DocumentoConferidoAntesDeGravar(DeclaracoesDoDocumento Declaracoes, ProvaPresencialConferida ProvaPresencial);
