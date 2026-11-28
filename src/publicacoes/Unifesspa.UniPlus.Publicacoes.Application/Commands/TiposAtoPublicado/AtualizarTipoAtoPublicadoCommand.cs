namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Atualiza uma versão de tipo de ato. O <c>Codigo</c> é <b>imutável</b> — é a
/// identidade do tipo (a série de vigências agrupa-se por ele, e a vaga de uma
/// linhagem de atos únicos por objeto é chaveada por ele, ADR-0107); o payload
/// reapresenta o mesmo valor, e o agregado recusa qualquer divergência.
/// </summary>
/// <remarks>
/// <c>Codigo</c>/<c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125) —
/// mesma justificativa de <see cref="CriarTipoAtoPublicadoCommand"/>.
/// <para>
/// Os seis atributos e o início da vigência são <c>bool?</c>/<c>DateOnly?</c> sem valor padrão e sem
/// <c>[JsonRequired]</c>: um valor implícito (<c>false</c>, ano 1) faria o cliente que não os conhece
/// ligar ou desligar o atributo em silêncio. Ausentes ou nulos, chegam ao domínio e são recusados com 422,
/// acumulados com as demais recusas; o contrato os publica como obrigatórios e sem <c>null</c>.
/// </para>
/// </remarks>
public sealed record AtualizarTipoAtoPublicadoCommand(
    Guid Id,
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
    string? BaseLegal = null) : ICommand<Result>
{
    public DeclaracaoDoTipoAtoPublicado Declaracao() => new(
        Codigo, Nome, CongelaConfiguracao, UnicoPorObjeto, EfeitoIrreversivel, EhResultado,
        PublicavelComoAvulso, ExigeAtoCorrigido, VigenciaInicio, VigenciaFim, BaseLegal);
}
