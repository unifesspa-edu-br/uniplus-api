namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma categoria de documento que uma <see cref="BancaRequerida"/> julga (0..*) —
/// snapshot-copy (ADR-0061) de uma <c>CategoriaDocumento</c> do módulo Configuração no
/// momento em que o recorte de competência foi declarado.
/// </summary>
/// <remarks>
/// <para>
/// O conjunto destas categorias é o <b>recorte de competência</b> da banca: o que separa
/// duas bancas do mesmo tipo dentro da mesma fase, como a que analisa critério de renda da
/// que analisa requisito étnico-racial numa mesma análise documental. Sem ele, as duas são
/// indistinguíveis no snapshot publicado, e parecer, recurso e exigência documental ficam
/// sem responsável identificável.
/// </para>
/// <para>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete) e <b>não</b> tem o
/// <see cref="EntityBase.Id"/> congelado no envelope, mesma disciplina de
/// <see cref="BancaRequerida"/>: nada referencia a linha de fora dela, e a configuração em
/// rascunho é substituível por inteiro (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>).
/// </para>
/// </remarks>
public sealed class CategoriaJulgada : EntityBase
{
    public Guid BancaRequeridaId { get; private set; }

    /// <summary>Id (Guid v7) da <c>CategoriaDocumento</c> viva de origem, no momento do congelamento.</summary>
    public Guid CategoriaDocumentoOrigemId { get; private set; }

    /// <summary>Código classificatório congelado (ex.: <c>"RENDA"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    private CategoriaJulgada() { }

    /// <summary>
    /// Cria a categoria julgada. Que a categoria exista e siga viva no cadastro é I/O —
    /// resolvido em Application (ADR-0042), onde a view do cadastro está em mãos.
    /// </summary>
    public static CategoriaJulgada Criar(Guid categoriaDocumentoOrigemId, string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        if (categoriaDocumentoOrigemId == Guid.Empty)
        {
            throw new ArgumentException(
                "O id de origem da categoria de documento é obrigatório.",
                nameof(categoriaDocumentoOrigemId));
        }

        return new CategoriaJulgada
        {
            CategoriaDocumentoOrigemId = categoriaDocumentoOrigemId,
            Codigo = codigo.Trim(),
        };
    }

    internal void VincularBanca(Guid bancaRequeridaId) => BancaRequeridaId = bancaRequeridaId;
}
