namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma condição da pré-condição de um <see cref="FatoColetado"/> (Story #926) — a forma
/// <b>relacional</b> (linha com ordinal de cláusula) da tripla <c>{ Fato, Operador, Valor }</c>.
/// Agrupadas por <see cref="Clausula"/> (OU entre cláusulas, E dentro), formam o predicado que
/// decide se o campo produtor do fato é apresentado ao candidato.
/// </summary>
/// <remarks>
/// Deliberadamente separada de <see cref="CondicaoGatilho"/>, apesar da forma idêntica: aquela é
/// filha de <see cref="DocumentoExigido"/> e responde "esta exigência se aplica?"; esta é filha de
/// <see cref="FatoColetado"/> e responde "este campo é apresentado?". Unificá-las sob um
/// discriminador acoplaria dois ciclos de vida distintos — a exigência é substituída com os
/// documentos, o fato coletado com o formulário — e faria a chave estrangeira de uma valer para a
/// outra. O reúso está onde importa: ambas produzem <see cref="CondicaoDnf"/> e são avaliadas pelo
/// mesmo <see cref="ValueObjects.PredicadoDnf"/>.
/// </remarks>
public sealed class CondicaoPrecondicaoFato : EntityBase
{
    public Guid FatoColetadoId { get; private set; }

    /// <summary>Ordinal da cláusula — OU entre cláusulas, E dentro da mesma cláusula.</summary>
    public int Clausula { get; private set; }

    /// <summary>Código do fato citado — sempre um fato <b>anterior</b> na ordem de coleta.</summary>
    public string Fato { get; private set; } = string.Empty;

    public Operador Operador { get; private set; }

    public JsonElement Valor { get; private set; }

    private CondicaoPrecondicaoFato() { }

    /// <summary>
    /// Cria a condição validando a <b>forma</b> via <see cref="CondicaoDnf.Criar"/>. A validação
    /// semântica — fato no vocabulário fechado, operador × domínio, valor × domínio — é do
    /// <see cref="Services.PredicadoDnfValidador"/>, resolvido pela Application, que tem acesso ao
    /// vocabulário cross-módulo. A validação <b>estrutural</b> do grafo (fato citado existe na
    /// coleta, é anterior, e não fecha ciclo) é do agregado, em
    /// <see cref="ProcessoSeletivo.DefinirFatosColetados"/>.
    /// </summary>
    public static Result<CondicaoPrecondicaoFato> Criar(int clausula, string fato, Operador operador, JsonElement valor)
    {
        if (clausula < 0)
        {
            return Result<CondicaoPrecondicaoFato>.Failure(new DomainError(
                CondicaoPrecondicaoFatoErrorCodes.ClausulaInvalida,
                "O ordinal da cláusula não pode ser negativo."));
        }

        Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(fato, operador, valor);
        if (condicaoResult.IsFailure)
        {
            return Result<CondicaoPrecondicaoFato>.Failure(condicaoResult.Error!);
        }

        CondicaoDnf condicao = condicaoResult.Value!;
        return Result<CondicaoPrecondicaoFato>.Success(new CondicaoPrecondicaoFato
        {
            Clausula = clausula,
            Fato = condicao.Fato,
            Operador = condicao.Operador,
            Valor = condicao.Valor,
        });
    }

    internal void VincularFatoColetado(Guid fatoColetadoId) => FatoColetadoId = fatoColetadoId;

    /// <summary>Reconstrói o VO já validado — a forma foi provada em <see cref="Criar"/>.</summary>
    internal CondicaoDnf ParaCondicaoDnf() => CondicaoDnf.Criar(Fato, Operador, Valor).Value!;
}

/// <summary>Códigos de erro de <see cref="CondicaoPrecondicaoFato"/>.</summary>
public static class CondicaoPrecondicaoFatoErrorCodes
{
    public const string ClausulaInvalida = "CondicaoPrecondicaoFato.ClausulaInvalida";
}
