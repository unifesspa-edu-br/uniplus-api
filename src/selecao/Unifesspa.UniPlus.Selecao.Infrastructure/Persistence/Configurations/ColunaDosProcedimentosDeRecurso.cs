namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Linq.Expressions;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A coluna <c>procedimentos_de_recurso</c> (<c>jsonb</c>) da janela de recurso da etapa e da fase: os procedimentos
/// declarados, em ordem canônica, num documento só. Não há tabela filha porque nunca se consulta por procedimento: a
/// coleção é lida e gravada com a janela que a declara.
/// </summary>
internal static class ColunaDosProcedimentosDeRecurso
{
    private static readonly JsonSerializerOptions Opcoes = OpcoesJsonDeDocumento.Criar(new(JsonSerializerDefaults.Web));

    internal static readonly ValueConverter<ProcedimentosDeRecurso, string> Conversor = new(
        procedimentos => Escrever(procedimentos),
        documento => Ler(documento));

    internal static readonly ValueComparer<ProcedimentosDeRecurso> Comparador = new(
        (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.MesmoConteudo(b)),
        procedimentos => Escrever(procedimentos).GetHashCode(StringComparison.Ordinal),
        procedimentos => procedimentos);

    internal static void MapearProcedimentosDeRecurso<TEntidade>(
        this EntityTypeBuilder<TEntidade> builder, Expression<Func<TEntidade, ProcedimentosDeRecurso>> propriedade)
        where TEntidade : class =>
        builder.Property(propriedade)
            .HasColumnName("procedimentos_de_recurso")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasComment("Procedimentos de recurso declarados para a janela, um por matéria, em ordem canônica.")
            .HasConversion(Conversor, Comparador)
            .IsRequired();

    private static string Escrever(ProcedimentosDeRecurso procedimentos) =>
        JsonSerializer.Serialize(procedimentos.Itens.Select(Persistido), Opcoes);

    private static ProcedimentosDeRecurso Ler(string documento) =>
        ProcedimentosDeRecurso.De((JsonSerializer.Deserialize<List<ProcedimentoPersistido>>(documento, Opcoes) ?? []).Select(Declarado));

    private static ProcedimentoPersistido Persistido(ProcedimentoDeRecursoDeclarado procedimento) => new(
        procedimento.TipoDeDecisao.ToCodigo(),
        procedimento.Alvo is null ? null : (int)procedimento.Alvo.Tipo,
        procedimento.Alvo?.Valor,
        procedimento.Regra.Codigo,
        procedimento.Regra.Versao,
        procedimento.Regra.Hash);

    private static ProcedimentoDeRecursoDeclarado Declarado(ProcedimentoPersistido persistido) => ProcedimentoDeRecursoDeclarado.De(
        TipoMotivoDeDecisaoCodigo.FromCodigo(persistido.TipoDeDecisao),
        Alvo(persistido),
        ReferenciaRegra.De(persistido.RegraCodigo, persistido.RegraVersao, persistido.RegraHash));

    private static AlvoDoProcedimentoDeRecurso? Alvo(ProcedimentoPersistido persistido) => (TipoDeAlvoDoProcedimentoDeRecurso?)persistido.AlvoTipo switch
    {
        TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao => AlvoDoProcedimentoDeRecurso.DoItem(Guid.ParseExact(persistido.AlvoValor!, "D")),
        TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem => AlvoDoProcedimentoDeRecurso.DaArea(persistido.AlvoValor!),
        _ => null,
    };

    private sealed record ProcedimentoPersistido(
        string TipoDeDecisao, int? AlvoTipo, string? AlvoValor, string RegraCodigo, string RegraVersao, string RegraHash);
}
