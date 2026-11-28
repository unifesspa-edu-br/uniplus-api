namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Cria uma versão de tipo de ato: código (UPPER_SNAKE), nome, os três atributos de
/// consequência, a janela semiaberta de vigência e a base legal opcional. O ator de
/// auditoria (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>,
/// não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c>/<c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125): sem
/// valor default, para o schema OpenAPI continuar listando-os como obrigatórios;
/// nulos, para o campo ausente escapar do <c>[ApiController]</c> e chegar à
/// validação de domínio, que acumula toda violação no mesmo lote.
/// <para>
/// Os seis atributos e o início da vigência são <c>bool?</c>/<c>DateOnly?</c> sem valor padrão e sem
/// <c>[JsonRequired]</c>: um valor implícito (<c>false</c>, ano 1) faria o cliente que não os conhece
/// ligar ou desligar o atributo em silêncio. Ausentes ou nulos, chegam ao domínio e são recusados com 422,
/// acumulados com as demais recusas; o contrato os publica como obrigatórios e sem <c>null</c>.
/// </para>
/// </remarks>
public sealed record CriarTipoAtoPublicadoCommand(
    [property: NoFormatoDoCodigo(TipoAtoPublicado.TamanhoMaximoDoCodigo)] string? Codigo,
    string? Nome,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.CongelaConfiguracao)] bool? CongelaConfiguracao,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.UnicoPorObjeto)] bool? UnicoPorObjeto,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.EfeitoIrreversivel)] bool? EfeitoIrreversivel,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.EhResultado)] bool? EhResultado,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.PublicavelComoAvulso)] bool? PublicavelComoAvulso,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.ExigeAtoCorrigido)] bool? ExigeAtoCorrigido,
    [property: ObrigatorioNoContrato, Description(DescricoesDosAtributosDoTipoDeAto.VigenciaInicio)] DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim = null,
    string? BaseLegal = null) : ICommand<Result<Guid>>
{
    public DeclaracaoDoTipoAtoPublicado Declaracao() => new(
        Codigo, Nome, CongelaConfiguracao, UnicoPorObjeto, EfeitoIrreversivel, EhResultado,
        PublicavelComoAvulso, ExigeAtoCorrigido, VigenciaInicio, VigenciaFim, BaseLegal);
}
