namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

internal static class ChecksDoFatoCandidato
{
    private static readonly string Opcao = SqlDosCodigosDeContrato.Literal(TipoDoDado.Opcao);
    private static readonly string Texto = SqlDosCodigosDeContrato.Literal(TipoDoDado.Texto);
    private static readonly string Data = SqlDosCodigosDeContrato.Literal(TipoDoDado.Data);
    private static readonly string Endereco = SqlDosCodigosDeContrato.Literal(TipoDoDado.Endereco);

    internal static void Configurar(TableBuilder<FatoCandidato> table)
    {
        table.HasComment("Catálogo de dados solicitados ou calculados para candidatos e suas regras de uso.");
        ConfigurarChecksDosDominiosFechados(table);
        ConfigurarChecksDeCoerenciaDoDominio(table);
        ConfigurarCheckDoMecanismoDoVinculo(table);
        AprovacaoDaProtecaoOwnedConfiguration.ConfigurarChecks(table, "dados_do_candidato");
    }

    // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
    private static void ConfigurarChecksDosDominiosFechados(TableBuilder<FatoCandidato> table)
    {
        AdicionarCheckDeCodigos(table, "tipo", SqlDosCodigosDeContrato.Lista<TipoDoDado>());
        AdicionarCheckDeCodigos(table, "origem", SqlDosCodigosDeContrato.Lista<OrigemFato>());
        AdicionarCheckDeCodigos(table, "quantidade_de_valores", SqlDosCodigosDeContrato.Lista<QuantidadeDeValores>());
        AdicionarCheckDeCodigos(table, "escopo", SqlDosCodigosDeContrato.Lista<EscopoFato>());
        AdicionarCheckDeCodigos(table, "classificacao_protecao", SqlDosCodigosDeContrato.Lista<ClassificacaoDeProtecao>());
        AdicionarCheckDeCodigos(table, "hipotese_legal", SqlDosCodigosDeContrato.Lista<HipoteseLegalTratamento>());
        table.HasCheckConstraint(
            "ck_dados_do_candidato_componente_exibicao",
            $"componente_exibicao IS NULL OR componente_exibicao IN ({SqlDosCodigosDeContrato.Lista<ComponenteDeExibicao>()})");
    }

    // O vínculo grava MECANISMO:REFERENCIA; só o prefixo é vocabulário fechado, a referência é livre.
    private static void ConfigurarCheckDoMecanismoDoVinculo(TableBuilder<FatoCandidato> table) =>
        table.HasCheckConstraint(
            "ck_dados_do_candidato_mecanismo_do_vinculo",
            $"split_part(fonte_do_valor, ':', 1) IN ({SqlDosCodigosDeContrato.Lista<MecanismoDoVinculo>()})");

    private static void ConfigurarChecksDeCoerenciaDoDominio(TableBuilder<FatoCandidato> table)
    {
        // Coerência fonte_das_opcoes × domínio (invariante da factory, replicada no banco): o
        // categórico declara uma das fontes; os demais domínios não têm fonte.
        table.HasCheckConstraint(
            "ck_dados_do_candidato_fonte_das_opcoes_coerente",
            $"(tipo = {Opcao} AND fonte_das_opcoes IS NOT NULL AND fonte_das_opcoes IN ({SqlDosCodigosDeContrato.Lista<FonteValoresFato>()})) "
            + $"OR (tipo <> {Opcao} AND fonte_das_opcoes IS NULL)");

        table.HasCheckConstraint(
            "ck_dados_do_candidato_formato_coerente",
            $"(tipo = {Texto} AND formato IS NOT NULL AND formato IN ({SqlDosCodigosDeContrato.Lista<FormatoTexto>()})) "
            + $"OR (tipo <> {Texto} AND formato IS NULL)");

        ConfigurarCheckDaClassificacaoMinimaDoDominio(table);
    }

    // Texto, data e endereço nunca são menos que dado pessoal (invariante da factory), inclusive
    // o nome social (ADR-0082, ADR-0153). Só o domínio texto também
    // aceita `IDENTIFICADOR` — número de documento é sempre texto, nunca data, endereço nem os
    // demais domínios (ADR-0136).
    private static void ConfigurarCheckDaClassificacaoMinimaDoDominio(TableBuilder<FatoCandidato> table)
    {
        table.HasCheckConstraint(
            "ck_dados_do_candidato_classificacao_minima_do_tipo",
            $"(tipo NOT IN ({Texto}, {Data}, {Endereco}) AND classificacao_protecao <> {SqlDosCodigosDeContrato.Literal(ClassificacaoDeProtecao.Identificador)}) "
            + $"OR (tipo = {Texto} AND classificacao_protecao IN ({SqlDosCodigosDeContrato.Literal(ClassificacaoDeProtecao.Pessoal)}, {SqlDosCodigosDeContrato.Literal(ClassificacaoDeProtecao.Identificador)}, {SqlDosCodigosDeContrato.Literal(ClassificacaoDeProtecao.Sensivel)})) "
            + $"OR (tipo IN ({Data}, {Endereco}) AND classificacao_protecao IN ({SqlDosCodigosDeContrato.Literal(ClassificacaoDeProtecao.Pessoal)}, {SqlDosCodigosDeContrato.Literal(ClassificacaoDeProtecao.Sensivel)}))");
    }

    private static void AdicionarCheckDeCodigos(TableBuilder<FatoCandidato> table, string coluna, string codigosSql) =>
        table.HasCheckConstraint(
            $"ck_dados_do_candidato_{coluna}",
            $"{coluna} IN ({codigosSql})");
}
