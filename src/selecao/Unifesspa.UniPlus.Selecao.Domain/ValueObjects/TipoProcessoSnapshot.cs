namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cópia por valor do tipo de processo resolvido em Configuração no momento da
/// criação. O processo nunca relê a configuração de tipos para mudar a própria identidade.
/// </summary>
public sealed record TipoProcessoSnapshot
{
    private TipoProcessoSnapshot() { }

    private TipoProcessoSnapshot(Guid origemId, string codigo, string nome)
    {
        OrigemId = origemId;
        Codigo = codigo;
        Nome = nome;
    }

    /// <remarks>Usado apenas na construção; a identidade é persistida no próprio snapshot.</remarks>
    public Guid OrigemId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    public static Result<TipoProcessoSnapshot> Criar(Guid origemId, string codigo, string nome)
    {
        if (origemId == Guid.Empty)
        {
            return Falha("TipoProcessoSnapshot.OrigemIdObrigatorio", "Origem do tipo de processo seletivo é obrigatória.");
        }
        return CriarComOrigem(origemId, codigo, nome);
    }

    private static Result<TipoProcessoSnapshot> CriarComOrigem(Guid origemId, string codigo, string nome)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Falha("TipoProcessoSnapshot.CodigoObrigatorio", "Código do tipo de processo seletivo é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha("TipoProcessoSnapshot.NomeObrigatorio", "Nome do tipo de processo seletivo é obrigatório.");
        }
        return codigo.Length > FormatoDoCodigo.ComprimentoMaximo || nome.Trim().Length > 200
            ? Falha("TipoProcessoSnapshot.TamanhoInvalido", "Snapshot do tipo de processo seletivo excede o tamanho permitido.")
            : Result<TipoProcessoSnapshot>.Success(new TipoProcessoSnapshot(origemId, codigo, nome.Trim()));
    }

    public override string ToString() => Codigo;

    private static Result<TipoProcessoSnapshot> Falha(string code, string message) =>
        Result<TipoProcessoSnapshot>.Failure(new DomainError(code, message));
}
