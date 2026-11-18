namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>A declaração do ato e os valores conferidos que serão registrados na mesma publicação.</summary>
internal sealed record AtoAPublicar(
    IDadosDoAtoPublicado Dados,
    ProcessoDivulgadoConferido Processo,
    TipoAtoPublicadoView Tipo,
    DocumentosDoAtoConferidos Documentos,
    DateTimeOffset PublicadoEm,
    IdentidadeDoItemDoEdital? ItemDoEdital = null,
    string? PoliticaConfirmadaPorContaId = null);
