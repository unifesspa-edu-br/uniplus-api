namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato.FatoCandidatoCodigoDeTeste;

/// <summary>
/// As restrições do banco sobre <c>fatos_do_candidato</c> e <c>valores_de_dominio_do_fato</c>: CHECKs de
/// domínio, origem, coerência e proteção, e os índices únicos do código e do valor, por SQL cru.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FatoCandidatoRestricoesDoBancoTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoRestricoesDoBancoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "CHECK recusa categórico sem fonte e texto sem formato via SQL cru")]
    [InlineData("CATEGORICO")]
    [InlineData("TEXTO")]
    public async Task Check_RecusaCategoricoSemFonteETextoSemFormato(string dominio)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fatos_do_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at, descricao, protecao_situacao, protecao_ciclo, protecao_manifestacao_desfavoravel, protecao_manifestacoes_no_ciclo)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {dominio}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow}, '', 'PENDENTE', 1, false, 0)
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a fonte do categórico e o formato do texto são obrigatórios também no banco — NULL não pode passar pelo IN");
    }

    [Theory(DisplayName = "CHECK recusa texto público, inclusive o nome social de sistema, via SQL cru")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Check_RecusaTextoPublico(bool sistema)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fatos_do_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, formato, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at, descricao, protecao_situacao, protecao_ciclo, protecao_manifestacao_desfavoravel, protecao_manifestacoes_no_ciclo)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"TEXTO"}, {"DECLARADO"}, {"ESCALAR"}, {"LIVRE"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PUBLICO"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, {sistema}, true, {DateTimeOffset.UtcNow}, '', 'PENDENTE', 1, false, 0)
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "nenhum texto, nem o nome social de sistema, foge da classificação mínima");
    }

    [Fact(DisplayName = "CHECK recusa classificação de proteção fora do domínio via SQL cru")]
    public async Task Check_RecusaClassificacaoInvalida()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fatos_do_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at, descricao, protecao_situacao, protecao_ciclo, protecao_manifestacao_desfavoravel, protecao_manifestacoes_no_ciclo)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"NAO_PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow}, '', 'PENDENTE', 1, false, 0)
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_fatos_do_candidato_classificacao_protecao só admite a escala da ADR-0081");
    }

    [Fact(DisplayName = "Índice único (FatoCandidatoId, Codigo) de valores_de_dominio_do_fato recusa código duplicado no mesmo fato")]
    public async Task FatoValorDominio_IndiceUnico_RecusaDuplicataNoMesmoFato()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        FatoCandidato corRaca = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "COR_RACA");

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.valores_de_dominio_do_fato (id, fato_candidato_id, codigo, descricao, ordem, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {corRaca.Id}, {"PRETA"}, {"Duplicata"}, 99, true, {DateTimeOffset.UtcNow})
            """);

        Npgsql.PostgresException ex = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        ex.SqlState.Should().Be("23505");
        ex.ConstraintName.Should().Be(RestricoesDoBanco.ValorDeDominioDoFatoCodigoUnico);
    }

    [Fact(DisplayName = "Índice único total recusa código duplicado (insert cru de um código já semeado)")]
    public async Task IndiceUnicoTotal_RecusaDuplicata()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fatos_do_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at, descricao, protecao_situacao, protecao_ciclo, protecao_manifestacao_desfavoravel, protecao_manifestacoes_no_ciclo)
            VALUES ({Guid.CreateVersion7()}, {"COR_RACA"}, {"Duplicata"}, {"BOOLEANO"}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:DUPLICATA"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow}, '', 'PENDENTE', 1, false, 0)
            """);

        Npgsql.PostgresException ex = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        ex.SqlState.Should().Be("23505");
        ex.ConstraintName.Should().Be(RestricoesDoBanco.FatoDoCandidatoCodigoUnico);
    }

    [Fact(DisplayName = "CHECK de domínio recusa token fora do vocabulário via SQL cru")]
    public async Task Check_RecusaDominioInvalido()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fatos_do_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at, descricao, protecao_situacao, protecao_ciclo, protecao_manifestacao_desfavoravel, protecao_manifestacoes_no_ciclo)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"OUTRO"}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow}, '', 'PENDENTE', 1, false, 0)
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>("o CHECK ck_fatos_do_candidato_dominio bloqueia 'TEXTO'");
    }

    [Fact(DisplayName = "CHECK de origem recusa token fora do vocabulário via SQL cru")]
    public async Task Check_RecusaOrigemInvalida()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fatos_do_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at, descricao, protecao_situacao, protecao_ciclo, protecao_manifestacao_desfavoravel, protecao_manifestacoes_no_ciclo)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow}, '', 'PENDENTE', 1, false, 0)
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_fatos_do_candidato_origem bloqueia o token legado 'BRUTO_INFORMADO' (ADR-0116 renomeou para DECLARADO)");
    }
}
