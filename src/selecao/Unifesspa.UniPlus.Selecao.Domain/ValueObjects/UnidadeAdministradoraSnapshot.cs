namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Runtime.CompilerServices;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Snapshot-copy por valor (ADR-0061) da Unidade administradora viva do módulo
/// Organização Institucional (<c>IUnidadeReader</c>, ADR-0056) no momento em que o
/// Processo Seletivo é criado — os dados de identificação ficam congelados
/// independentemente de atualizações futuras do cadastro de origem.
/// </summary>
/// <remarks>
/// <para>
/// Ao contrário de <see cref="ReferenciaReservaDemograficaSnapshot"/>, não carrega
/// <c>OrigemId</c> internamente: essa identidade já vive em
/// <c>ProcessoSeletivo.UnidadeAdministradoraOrigemId</c> (escalar de topo), e duplicá-la
/// aqui criaria duas fontes para a mesma grandeza.
/// </para>
/// <para>
/// A cidade (issue #1114) é copiada por valor no mesmo instante — <c>Unidade</c> pode
/// ter cidade opcional, mas este VO em si só representa dois estados: com cidade
/// completa (trio válido) ou sem nenhuma. A obrigatoriedade de a Unidade administradora
/// TER cidade para um processo NOVO é regra de negócio do handler que monta este
/// snapshot (<c>CriarProcessoSeletivoCommandHandler</c>, CA-02), não deste VO — que
/// também precisa representar snapshots antigos (pré-#1114), legitimamente sem cidade.
/// </para>
/// </remarks>
public sealed record UnidadeAdministradoraSnapshot
{
    private UnidadeAdministradoraSnapshot() { }

    private UnidadeAdministradoraSnapshot(string sigla, string codigo, string identificadorNoEnderecoWeb, string nome, string tipo, CidadeCopiadaDaUnidade cidade)
    {
        Sigla = sigla;
        Codigo = codigo;
        IdentificadorNoEnderecoWeb = identificadorNoEnderecoWeb;
        Nome = nome;
        Tipo = tipo;
        CidadeCodigoIbge = cidade.CodigoIbge;
        CidadeNome = cidade.Nome;
        CidadeUf = cidade.Uf;
    }

    public string Sigla { get; private set; } = string.Empty;
    public string Codigo { get; private set; } = string.Empty;
    public string IdentificadorNoEnderecoWeb { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Tipo { get; private set; } = string.Empty;
    public string? CidadeCodigoIbge { get; private set; }
    public string? CidadeNome { get; private set; }
    public string? CidadeUf { get; private set; }

    /// <summary>
    /// Sigla, código, identificador no endereço web, nome e tipo vêm do cadastro vivo da unidade, que já os exige: vazios são defeito de quem chama, não recusa
    /// a um cliente, e lançam <see cref="ArgumentException"/>. A referência de cidade é a que pode falhar como resultado.
    /// </summary>
    public static Result<UnidadeAdministradoraSnapshot> Criar(
        string sigla, string codigo, string identificadorNoEnderecoWeb, string nome, string tipo, CidadeCopiadaDaUnidade? cidade = null)
    {
        CidadeCopiadaDaUnidade informada = cidade ?? CidadeCopiadaDaUnidade.Ausente;
        Result cidadeValidacao = informada.Validar();
        if (cidadeValidacao.IsFailure)
        {
            return Result<UnidadeAdministradoraSnapshot>.Failure(cidadeValidacao.Error!);
        }

        return Result<UnidadeAdministradoraSnapshot>.Success(new UnidadeAdministradoraSnapshot(
            Obrigatorio(sigla), Obrigatorio(codigo), Obrigatorio(identificadorNoEnderecoWeb), Obrigatorio(nome), Obrigatorio(tipo), informada.Normalizada()));
    }

    private static string Obrigatorio(string? valor, [CallerArgumentExpression(nameof(valor))] string? campo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, campo);
        return valor.Trim();
    }
}
