namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma fase canônica existente, substituindo tudo o que é editável pelo corpo: o que ele
/// omite fica falso ou vazio, inclusive os sinalizadores. O <c>Codigo</c> e o <c>Id</c> são
/// <b>imutáveis</b>: o comando <b>não</b> aceita código — o handler carrega a
/// entidade e a atualiza sem alterá-lo. A origem da data chega como código de contrato e a unidade sugerida como id (nulo retira a sugestão). O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarFaseCanonicaCommand(
    Guid Id,
    string? Nome = null,
    string? Descricao = null,
    bool AgrupaEtapas = false,
    bool PermiteComplementacao = false,
    string? BaseLegal = null,
    bool ColetaInscricao = false,
    bool ColetaSolicitacaoIsencao = false,
    bool OrganizadaEmChamadas = false,
    [property: AceitaCodigosDe<OrigemDataFase>] string? OrigemData = null,
    Guid? UnidadeSugeridaId = null) : ICommand<Result>
{
    /// <summary>Os cinco sinalizadores do corpo, como o valor que a fase confere contra o código.</summary>
    public SinalizadoresDaFase Sinalizadores() =>
        new(AgrupaEtapas, PermiteComplementacao, ColetaInscricao, ColetaSolicitacaoIsencao, OrganizadaEmChamadas);
}
