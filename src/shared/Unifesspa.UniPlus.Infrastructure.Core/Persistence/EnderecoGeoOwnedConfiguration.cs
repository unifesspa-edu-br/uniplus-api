namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// Mapeamento EF Core compartilhado do value object
/// <see cref="ReferenciaEnderecoGeo"/> como owned type opcional (table splitting,
/// ADR-0096): colunas <c>endereco_*</c> na mesma tabela do agregado. Centralizado
/// aqui para que Configuração (Campus/LocalOferta) e Organização (Instituicao)
/// mapeiem o endereço de forma idêntica — sem drift de schema entre módulos.
/// </summary>
public static class EnderecoGeoOwnedConfiguration
{
    /// <summary>
    /// Nome do CHECK de coerência cidade↔CEP por tabela (CA-04). Use
    /// <see cref="CoerenciaCidadeCheckSql"/> como expressão.
    /// </summary>
    public static string CoerenciaCidadeCheckName(string tabela) =>
        $"ck_{tabela}_endereco_cidade_coerente";

    /// <summary>
    /// Expressão SQL NULL-safe do CHECK de coerência cidade↔CEP: quando há cidade
    /// no endereço e cidade no nível raiz, ambos devem coincidir (código IBGE + UF).
    /// Os ramos <c>IS NULL</c> impedem que comparações com <c>UNKNOWN</c> passem.
    /// </summary>
    public const string CoerenciaCidadeCheckSql =
        "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR "
        + "(endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)";

    /// <summary>
    /// Nome do CHECK de completude do endereço por tabela: garante a regra
    /// all-or-nothing dos campos <strong>obrigatórios</strong> do owned type.
    /// </summary>
    public static string CompletudeCheckName(string tabela) =>
        $"ck_{tabela}_endereco_completo";

    /// <summary>
    /// Expressão SQL all-or-nothing dos campos obrigatórios do endereço: ou todos
    /// nulos (endereço ausente) ou todos presentes. Espelha no banco a regra do VO
    /// (CEP, cidade, nível de resolução e origem obrigatórios quando há endereço) —
    /// impede que escritas cruas deixem o owned type parcial: o
    /// <c>endereco_cep</c> é o sentinela de presença, então uma linha com CEP mas
    /// cidade/nível/origem nulos seria materializada incoerente pelo EF.
    /// </summary>
    public const string CompletudeCheckSql =
        "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL "
        + "AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL) "
        + "OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL "
        + "AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)";

    /// <summary>
    /// Configura as colunas do endereço estruturado. CEP, cidade, nível de
    /// resolução e origem são <c>required</c> no dependente — EF os usa como
    /// sentinela de presença do owned type opcional (sem endereço ⇒
    /// <c>endereco_cep IS NULL</c>), mantendo as colunas anuláveis no banco.
    /// </summary>
    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, ReferenciaEnderecoGeo> e)
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(e);

        e.Property(p => p.Cep)
            .HasColumnName("endereco_cep")
            .HasMaxLength(ReferenciaEnderecoGeo.CepLength)
            .IsFixedLength()
            .IsRequired();
        e.Property(p => p.Logradouro)
            .HasColumnName("endereco_logradouro")
            .HasMaxLength(ReferenciaEnderecoGeo.LogradouroMaxLength);
        e.Property(p => p.Numero)
            .HasColumnName("endereco_numero")
            .HasMaxLength(ReferenciaEnderecoGeo.NumeroMaxLength);
        e.Property(p => p.Complemento)
            .HasColumnName("endereco_complemento")
            .HasMaxLength(ReferenciaEnderecoGeo.ComplementoMaxLength);
        e.Property(p => p.Bairro)
            .HasColumnName("endereco_bairro")
            .HasMaxLength(ReferenciaEnderecoGeo.BairroMaxLength);
        e.Property(p => p.Distrito)
            .HasColumnName("endereco_distrito")
            .HasMaxLength(ReferenciaEnderecoGeo.DistritoMaxLength);
        e.Property(p => p.CidadeCodigoIbge)
            .HasColumnName("endereco_cidade_codigo_ibge")
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength()
            .IsRequired();
        e.Property(p => p.CidadeNome)
            .HasColumnName("endereco_cidade_nome")
            .HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
            .IsRequired();
        e.Property(p => p.CidadeUf)
            .HasColumnName("endereco_cidade_uf")
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength()
            .IsRequired();
        e.Property(p => p.Latitude).HasColumnName("endereco_latitude").HasPrecision(9, 6);
        e.Property(p => p.Longitude).HasColumnName("endereco_longitude").HasPrecision(9, 6);
        e.Property(p => p.NivelResolucao)
            .HasColumnName("endereco_nivel_resolucao")
            .HasMaxLength(NivelResolucaoEndereco.MaxLength)
            .IsRequired();
        e.Property(p => p.Origem)
            .HasColumnName("endereco_origem")
            .HasMaxLength(ReferenciaCidadeGeo.OrigemMaxLength)
            .IsRequired();
        e.Property(p => p.DisplayAtualizadoEm).HasColumnName("endereco_display_atualizado_em");
    }
}
