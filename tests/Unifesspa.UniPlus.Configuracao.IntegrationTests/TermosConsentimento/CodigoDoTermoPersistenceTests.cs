namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O código do termo e o número da versão, contra Postgres real: o índice único parcial do código vale entre os termos vivos
/// e deixa reaproveitar o de um termo removido, o número da versão é único dentro do termo, e as duas violações chegam com o
/// nome que o handler traduz em recusa; o CHECK recusa o que o domínio nunca grava.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class CodigoDoTermoPersistenceTests
{
    private const string Admin = "admin-a";
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public CodigoDoTermoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Dois termos vivos com o mesmo código violam o índice único nomeado")]
    public async Task DoisVivosComOMesmoCodigo_ViolamOIndice()
    {
        string nome = NomeUnico();
        await Gravar(nome);

        Func<Task> segundo = () => Gravar(nome.ToUpperInvariant());

        DbUpdateException violacao = (await segundo.Should().ThrowAsync<DbUpdateException>()).Which;
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(violacao).Should().Be(RestricoesDoBanco.TermoDeConsentimentoCodigoAtivos);
    }

    [Fact(DisplayName = "O código de um termo removido pode ser reaproveitado por um novo")]
    public async Task CodigoDeTermoRemovido_PodeSerReaproveitado()
    {
        string nome = NomeUnico();
        Guid primeiro = await Gravar(nome);
        await Remover(primeiro);

        Func<Task> reaproveitar = () => Gravar(nome);

        await reaproveitar.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Duas versões com o mesmo número no mesmo termo violam o índice único nomeado")]
    public async Task DuasVersoesComOMesmoNumero_ViolamOIndice()
    {
        TermoConsentimento termo = TermoConsentimento.Criar(NomeUnico(), "Texto", "Lei 13.709/2018", null).Value!;
        termo.MarcarRevisado(Admin, Agora);
        TermoConsentimentoVersao primeira = termo.Promover(Admin, Agora).Value!;
        TermoConsentimentoVersao repetida = TermoConsentimentoVersao.Promover(
            termo.Id, primeira.Numero, "Outro texto", "Lei 13.709/2018", primeira.FormaAceite, Agora, Admin);

        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Admin);
        contexto.TermosConsentimento.Add(termo);
        await contexto.SaveChangesAsync();
        contexto.VersoesTermoConsentimento.Add(repetida);

        DbUpdateException violacao = (await FluentActions.Awaiting(() => contexto.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>()).Which;

        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(violacao).Should().Be(RestricoesDoBanco.VersaoDoTermoNumeroUnico);
    }

    [Fact(DisplayName = "O banco recusa código fora do formato")]
    public async Task CodigoForaDoFormato_ViolaOCheck()
    {
        Guid id = await Gravar(NomeUnico());

        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Admin);
        Func<Task> alterar = async () => await contexto.Database.ExecuteSqlAsync(
            $"UPDATE configuracao.termos_de_consentimento SET codigo = {"minusculo"} WHERE id = {id}");

        Npgsql.PostgresException violacao = (await alterar.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        violacao.ConstraintName.Should().Be("ck_termos_de_consentimento_codigo_formato");
    }

    private static string NomeUnico() => $"Termo de teste {Guid.NewGuid():N}";

    private async Task<Guid> Gravar(string nome)
    {
        TermoConsentimento termo = TermoConsentimento.Criar(nome, null, null, null).Value!;

        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Admin);
        contexto.TermosConsentimento.Add(termo);
        await contexto.SaveChangesAsync();

        return termo.Id;
    }

    private async Task Remover(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Admin);
        TermoConsentimento rastreado = await contexto.TermosConsentimento.SingleAsync(t => t.Id == id);
        contexto.TermosConsentimento.Remove(rastreado);
        await contexto.SaveChangesAsync();
    }
}
