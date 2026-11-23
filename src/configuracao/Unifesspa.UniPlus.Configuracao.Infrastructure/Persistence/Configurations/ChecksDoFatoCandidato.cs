namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal static class ChecksDoFatoCandidato
{
    internal static void Configurar(TableBuilder<FatoCandidato> table)
    {
        table.HasComment("Catálogo de dados solicitados ou calculados para candidatos e suas regras de uso.");
        ConfigurarChecksDosDominiosFechados(table);
        ConfigurarChecksDeCoerenciaDoDominio(table);
        AprovacaoDaProtecaoOwnedConfiguration.ConfigurarChecks(table, "dados_do_candidato");
    }

    // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
    private static void ConfigurarChecksDosDominiosFechados(TableBuilder<FatoCandidato> table)
    {
        AdicionarCheckDeCodigos(table, "tipo", DominiosFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "origem", OrigensFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "quantidade_de_valores", CardinalidadesFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "escopo", EscoposFato.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "classificacao_protecao", ClassificacoesDeProtecao.TokensCanonicos);
        AdicionarCheckDeCodigos(table, "hipotese_legal", HipotesesLegaisTratamento.TokensCanonicos);
        table.HasCheckConstraint(
            "ck_dados_do_candidato_componente_exibicao",
            $"componente_exibicao IS NULL OR componente_exibicao IN ({CodigosSql(ComponentesDeExibicao.TokensCanonicos)})");
    }

    private static void ConfigurarChecksDeCoerenciaDoDominio(TableBuilder<FatoCandidato> table)
    {
        // Coerência fonte_das_opcoes × domínio (invariante da factory, replicada no banco): o
        // categórico declara uma das fontes; os demais domínios não têm fonte.
        table.HasCheckConstraint(
            "ck_dados_do_candidato_fonte_das_opcoes_coerente",
            $"(tipo = 'CATEGORICO' AND fonte_das_opcoes IS NOT NULL AND fonte_das_opcoes IN ({CodigosSql(FontesValoresFato.TokensCanonicos)})) "
            + "OR (tipo <> 'CATEGORICO' AND fonte_das_opcoes IS NULL)");

        table.HasCheckConstraint(
            "ck_dados_do_candidato_formato_coerente",
            $"(tipo = 'TEXTO' AND formato IS NOT NULL AND formato IN ({CodigosSql(FormatosTexto.TokensCanonicos)})) "
            + "OR (tipo <> 'TEXTO' AND formato IS NULL)");

        ConfigurarCheckDaClassificacaoMinimaDoDominio(table);
    }

    // Texto, data e endereço nunca são menos que dado pessoal (invariante da factory), inclusive
    // o nome social (ADR-0082, ADR-0153). Só o domínio texto também
    // aceita `IDENTIFICADOR` — número de documento é sempre texto, nunca data, endereço nem os
    // demais domínios (ADR-0136, emenda de #1857).
    private static void ConfigurarCheckDaClassificacaoMinimaDoDominio(TableBuilder<FatoCandidato> table)
    {
        table.HasCheckConstraint(
            "ck_dados_do_candidato_classificacao_minima_do_tipo",
            "(tipo NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') "
            + "OR (tipo = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) "
            + "OR (tipo IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
    }

    private static void AdicionarCheckDeCodigos(TableBuilder<FatoCandidato> table, string coluna, IReadOnlyList<string> codigos) =>
        table.HasCheckConstraint(
            $"ck_dados_do_candidato_{coluna}",
            $"{coluna} IN ({CodigosSql(codigos)})");

    private static string CodigosSql(IReadOnlyList<string> codigos) =>
        string.Join(", ", codigos.Select(codigo => $"'{codigo}'"));

}
