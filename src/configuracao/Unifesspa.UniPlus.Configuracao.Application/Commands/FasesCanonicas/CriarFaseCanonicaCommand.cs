namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de fase (UNI-REQ-0139): código (chave natural canônica imutável), nome, origem da
/// data como código de contrato (<c>OrigemData</c>), os cinco sinalizadores, descrição, base legal e
/// unidade sugerida opcionais (<c>UnidadeSugeridaId</c>, resolvida no cadastro de unidades pelo
/// handler). O ator de auditoria (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>,
/// não no payload.
/// </summary>
/// <remarks>
/// Os campos são anuláveis (ADR-0125): sem validator FluentValidation garantindo não-nulo a
/// montante, o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar. Os cinco sinalizadores
/// não têm valor padrão: omitir um é recusado.
/// </remarks>
/// <param name="Codigo">Código do tipo de fase, escolhido no conjunto canônico.</param>
/// <param name="Nome">Nome do tipo de fase.</param>
/// <param name="OrigemData">Código de quando a data do tipo de fase é fixada.</param>
/// <param name="AgrupaEtapas">Obrigatório: informe se o tipo de fase agrupa as etapas pontuadas. Campo ausente ou nulo é recusado (422).</param>
/// <param name="PermiteComplementacao">Obrigatório: informe se o tipo de fase admite complementação documental. Campo ausente ou nulo é recusado (422).</param>
/// <param name="ColetaInscricao">Obrigatório: informe se o tipo de fase coleta inscrições. Campo ausente ou nulo é recusado (422).</param>
/// <param name="ColetaSolicitacaoIsencao">Obrigatório: informe se o tipo de fase coleta a solicitação de isenção da taxa. Campo ausente ou nulo é recusado (422).</param>
/// <param name="OrganizadaEmChamadas">Obrigatório: informe se o tipo de fase é organizada em chamadas. Campo ausente ou nulo é recusado (422).</param>
/// <param name="Descricao">Descrição opcional.</param>
/// <param name="BaseLegal">Base legal opcional.</param>
/// <param name="UnidadeSugeridaId">Id da unidade sugerida, opcional.</param>
public sealed record CriarFaseCanonicaCommand(
    string? Codigo,
    string? Nome,
    [property: AceitaCodigosDe<OrigemDataFase>] string? OrigemData,
    [property: ObrigatorioNoContrato]
    [property: Description(DescricoesDosSinalizadoresDoTipoDeFase.AgrupaEtapas)] bool? AgrupaEtapas,
    [property: ObrigatorioNoContrato]
    [property: Description(DescricoesDosSinalizadoresDoTipoDeFase.PermiteComplementacao)] bool? PermiteComplementacao,
    [property: ObrigatorioNoContrato]
    [property: Description(DescricoesDosSinalizadoresDoTipoDeFase.ColetaInscricao)] bool? ColetaInscricao,
    [property: ObrigatorioNoContrato]
    [property: Description(DescricoesDosSinalizadoresDoTipoDeFase.ColetaSolicitacaoIsencao)] bool? ColetaSolicitacaoIsencao,
    [property: ObrigatorioNoContrato]
    [property: Description(DescricoesDosSinalizadoresDoTipoDeFase.OrganizadaEmChamadas)] bool? OrganizadaEmChamadas,
    string? Descricao = null,
    string? BaseLegal = null,
    Guid? UnidadeSugeridaId = null) : ICommand<Result<Guid>>
{
    /// <summary>Os cinco sinalizadores do corpo, como chegaram, para a fase conferir.</summary>
    public SinalizadoresInformados Sinalizadores() =>
        new(AgrupaEtapas, PermiteComplementacao, ColetaInscricao, ColetaSolicitacaoIsencao, OrganizadaEmChamadas);
}
