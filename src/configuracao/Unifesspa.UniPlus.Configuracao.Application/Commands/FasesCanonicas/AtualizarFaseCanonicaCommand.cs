namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza um tipo de fase existente, substituindo tudo o que é editável pelo corpo: nome, origem
/// da data e os cinco sinalizadores são obrigatórios; descrição, base legal e unidade sugerida
/// omitidas são apagadas. O <c>Codigo</c> e o <c>Id</c> são <b>imutáveis</b>: o comando
/// <b>não</b> aceita código — o handler carrega a entidade e a atualiza sem alterá-lo. A origem da
/// data chega como código de contrato e a unidade sugerida como id (nulo retira a sugestão). O ator
/// (<c>updated_by</c>) é carimbado server-side via <c>IUserContext</c>.
/// </summary>
/// <param name="Id">Id do tipo de fase, o mesmo da rota.</param>
/// <param name="Nome">Nome do tipo de fase.</param>
/// <param name="OrigemData">Código de quando a data do tipo de fase é fixada.</param>
/// <param name="AgrupaEtapas">Obrigatório: informe se o tipo de fase agrupa as etapas pontuadas. Campo ausente ou nulo é recusado (422).</param>
/// <param name="PermiteComplementacao">Obrigatório: informe se o tipo de fase admite complementação documental. Campo ausente ou nulo é recusado (422).</param>
/// <param name="ColetaInscricao">Obrigatório: informe se o tipo de fase coleta inscrições. Campo ausente ou nulo é recusado (422).</param>
/// <param name="ColetaSolicitacaoIsencao">Obrigatório: informe se o tipo de fase coleta a solicitação de isenção da taxa. Campo ausente ou nulo é recusado (422).</param>
/// <param name="OrganizadaEmChamadas">Obrigatório: informe se o tipo de fase é organizada em chamadas. Campo ausente ou nulo é recusado (422).</param>
/// <param name="Descricao">Descrição; omitida, é apagada.</param>
/// <param name="BaseLegal">Base legal; omitida, é apagada.</param>
/// <param name="UnidadeSugeridaId">Id da unidade sugerida; omitido, a sugestão é retirada.</param>
public sealed record AtualizarFaseCanonicaCommand(
    Guid Id,
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
    Guid? UnidadeSugeridaId = null) : ICommand<Result>
{
    /// <summary>Os cinco sinalizadores do corpo, como chegaram, para a fase conferir.</summary>
    public SinalizadoresInformados Sinalizadores() =>
        new(AgrupaEtapas, PermiteComplementacao, ColetaInscricao, ColetaSolicitacaoIsencao, OrganizadaEmChamadas);
}
