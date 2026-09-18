namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Forma das colunas de uma <see cref="ReferenciaRegra"/> embutida, num lugar só.
/// </summary>
/// <remarks>
/// <para>
/// A referência é embutida por doze navegações, em nove entidades, e cada uma declarava as
/// mesmas três larguras por conta própria — constantes privadas repetidas arquivo a arquivo,
/// nenhuma delas apontando para a configuração do catálogo de regras, que é de onde os
/// números vêm. Doze cópias mantidas à mão não são um incômodo de estilo: são o mecanismo que
/// já produziu uma divergência real, quando um dos donos ficou com larguras próprias e nada
/// confrontava. Com a forma declarada aqui, divergir deixa de ser possível em vez de ser
/// detectável depois.
/// </para>
/// <para>
/// O que varia legitimamente entre os donos fica com eles: o <b>prefixo das colunas</b>, porque
/// há entidades que embutem mais de uma referência e precisam distingui-las na mesma tabela, e
/// os <b>comentários de coluna</b>, que dizem o que aquela referência específica significa
/// naquele certame. Comentário se acrescenta encadeando <c>HasComment</c> depois desta chamada.
/// </para>
/// <para>
/// As três partes são sempre obrigatórias, inclusive nas navegações opcionais: a referência é
/// tudo ou nada — ou o dono não aponta para regra nenhuma, ou aponta com código, versão e hash.
/// Quem decide se ela pode faltar é a <b>navegação</b>, e é dela que sai a anulabilidade das
/// colunas; afrouxar a propriedade permitiria uma referência pela metade, que o value object
/// nem consegue construir.
/// </para>
/// <para>
/// As larguras acompanham <c>RegraCatalogoConfiguration</c>, que é a tabela para onde a
/// referência aponta: código e versão têm de caber o que o catálogo aceita, e o hash é um
/// SHA-256 hexadecimal minúsculo — 64 caracteres sempre, daí o comprimento fixo.
/// </para>
/// </remarks>
internal static class ReferenciaRegraOwnedConfiguration
{
    private const int CodigoMaxLength = 128;
    private const int VersaoMaxLength = 16;
    private const int HashLength = 64;

    /// <param name="regra">A navegação embutida a configurar.</param>
    /// <param name="prefixoDaColuna">
    /// Prefixo das três colunas — <c>"regra"</c> produz <c>regra_codigo</c>, <c>regra_versao</c>
    /// e <c>regra_hash</c>. É parâmetro porque uma entidade pode embutir várias referências.
    /// </param>
    public static OwnedNavigationBuilder<TDono, ReferenciaRegra> ConfigurarReferenciaRegra<TDono>(
        this OwnedNavigationBuilder<TDono, ReferenciaRegra> regra,
        string prefixoDaColuna)
        where TDono : class
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixoDaColuna);

        regra.Property(r => r.Codigo)
            .HasColumnName($"{prefixoDaColuna}_codigo")
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        regra.Property(r => r.Versao)
            .HasColumnName($"{prefixoDaColuna}_versao")
            .HasMaxLength(VersaoMaxLength)
            .IsRequired();

        regra.Property(r => r.Hash)
            .HasColumnName($"{prefixoDaColuna}_hash")
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        return regra;
    }
}
