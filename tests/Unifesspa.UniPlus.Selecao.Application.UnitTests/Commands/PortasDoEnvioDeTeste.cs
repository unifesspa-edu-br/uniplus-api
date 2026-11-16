namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>As portas que um teste do envio troca por outra: a conferência no Geo, o gerador de número, o relógio e os documentos. A que o teste não informa fica com o padrão.</summary>
internal sealed record PortasDoEnvioDeTeste(
    IConferenciaEnderecoGeo? Geo = null,
    IGeradorDeNumeroInscricao? Gerador = null,
    TimeProvider? Relogio = null,
    IDocumentoDaInscricaoRepository? Documentos = null);
