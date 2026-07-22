namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma condição do gatilho DNF de um <see cref="DocumentoExigido"/> (Story #554, PR #896) —
/// a forma <b>relacional</b> (linha com ordinal de cláusula) da tripla
/// <c>{ Fato, Operador, Valor }</c> de <see cref="ValueObjects.CondicaoDnf"/>. Agrupadas por
/// <see cref="Clausula"/> (OU entre cláusulas, E dentro — mesma convenção de
/// <c>PredicadoDnf.CriarDeCondicoesAgrupadas</c>), formam o predicado que decide se a
/// exigência se aplica a um candidato. <c>EntityBase</c> puro — filha de
/// <see cref="DocumentoExigido"/>, substituível por inteiro junto com o mesmo
/// <c>PUT {id}/documentos-exigidos</c> da PR #895.
/// </summary>
public sealed class CondicaoGatilho : EntityBase
{
    public Guid DocumentoExigidoId { get; private set; }

    /// <summary>Ordinal da cláusula — OU entre cláusulas, E dentro da mesma cláusula.</summary>
    public int Clausula { get; private set; }

    public string Fato { get; private set; } = string.Empty;

    public Operador Operador { get; private set; }

    public JsonElement Valor { get; private set; }

    private CondicaoGatilho() { }

    /// <summary>
    /// Cria a condição validando a <b>forma</b> via <see cref="CondicaoDnf.Criar"/> (mesma
    /// checagem que qualquer outro consumidor de <c>PredicadoDnf</c> já usa — fato não
    /// vazio, operador reconhecido, valor coerente com o operador). A validação
    /// <b>semântica</b> (fato no vocabulário fechado, operador × domínio, valor × domínio,
    /// integridade referencial contra a oferta do processo — CA-02/CA-03) é do
    /// <c>Selecao.Domain.Services.PredicadoDnfValidador</c>, resolvido pela Application, que
    /// tem acesso ao vocabulário cross-módulo e à oferta do processo — nunca aqui.
    /// </summary>
    public static Result<CondicaoGatilho> Criar(int clausula, string fato, Operador operador, JsonElement valor)
    {
        if (clausula < 0)
        {
            return Result<CondicaoGatilho>.Failure(new DomainError(
                "CondicaoGatilho.ClausulaInvalida", "O ordinal da cláusula não pode ser negativo."));
        }

        Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(fato, operador, valor);
        if (condicaoResult.IsFailure)
        {
            return Result<CondicaoGatilho>.Failure(condicaoResult.Error!);
        }

        CondicaoDnf condicao = condicaoResult.Value!;
        return Result<CondicaoGatilho>.Success(new CondicaoGatilho
        {
            Clausula = clausula,
            Fato = condicao.Fato,
            Operador = condicao.Operador,
            Valor = condicao.Valor,
        });
    }

    internal void VincularDocumentoExigido(Guid documentoExigidoId) =>
        DocumentoExigidoId = documentoExigidoId;

    /// <summary>
    /// Reconstrói o VO já validado — a forma foi provada em <see cref="Criar"/>, nunca
    /// revalidada aqui (mesmo espírito de <c>CondicaoDnf</c> na reidratação).
    /// </summary>
    internal CondicaoDnf ParaCondicaoDnf() => CondicaoDnf.Criar(Fato, Operador, Valor).Value!;
}
