namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDaPersistenciaDeOfertaCurso;

/// <summary>
/// O texto que a oferta grava nas colunas do programa e do formato é o código de contrato, e a leitura de um código que
/// o enum não declara falha, sem devolver valor padrão.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class OfertaCursoCodigosNoBancoTests
{
    private const string LeituraDoPrograma = "SELECT programa_de_oferta AS \"Value\" FROM configuracao.ofertas_de_curso WHERE id = {0}";
    private const string LeituraDoFormato = "SELECT formato_pedagogico AS \"Value\" FROM configuracao.ofertas_de_curso WHERE id = {0}";
    private const string RemoveRestricaoDoPrograma = "ALTER TABLE configuracao.ofertas_de_curso DROP CONSTRAINT ck_ofertas_de_curso_programa_de_oferta";
    private const string RemoveRestricaoDoFormato = "ALTER TABLE configuracao.ofertas_de_curso DROP CONSTRAINT ck_ofertas_de_curso_formato_pedagogico";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeOfertaCurso _apoio;

    public OfertaCursoCodigosNoBancoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeOfertaCurso(fixture);
    }

    [Theory(DisplayName = "O programa gravado em SQL cru é o código de contrato e a leitura devolve o mesmo programa")]
    [InlineData("REGULAR", ProgramaDeOferta.Regular)]
    [InlineData("FORMA_PARA", ProgramaDeOferta.FormaPara)]
    [InlineData("PARFOR", ProgramaDeOferta.Parfor)]
    [InlineData("PRONERA", ProgramaDeOferta.Pronera)]
    [InlineData("PEPETI", ProgramaDeOferta.Pepeti)]
    [InlineData("CONVENIO_OUTRO", ProgramaDeOferta.OutroConvenio)]
    [InlineData("OUTRO", ProgramaDeOferta.Outro)]
    public async Task Programa_GravadoComoCodigo_VoltaAoMesmoValor(string codigo, ProgramaDeOferta esperado)
    {
        Guid ofertaId = await GravarOfertaAsync(codigo, "PRESENCIAL");

        (await TextoDaColunaAsync(LeituraDoPrograma, ofertaId)).Should().Be(codigo);
        (await LerAsync(ofertaId)).ProgramaDeOferta.Should().Be(esperado);
    }

    [Theory(DisplayName = "O formato gravado em SQL cru é o código de contrato e a leitura devolve o mesmo formato")]
    [InlineData("PRESENCIAL", FormatoPedagogico.Presencial)]
    [InlineData("SEMIPRESENCIAL", FormatoPedagogico.Semipresencial)]
    [InlineData("EAD", FormatoPedagogico.Ead)]
    public async Task Formato_GravadoComoCodigo_VoltaAoMesmoValor(string codigo, FormatoPedagogico esperado)
    {
        Guid ofertaId = await GravarOfertaAsync("REGULAR", codigo);

        (await TextoDaColunaAsync(LeituraDoFormato, ofertaId)).Should().Be(codigo);
        (await LerAsync(ofertaId)).FormatoPedagogico.Should().Be(esperado);
    }

    [Theory(DisplayName = "Programa gravado com código que o enum não declara faz a leitura falhar, sem valor padrão")]
    [InlineData("PROUNI")]
    [InlineData("regular")]
    [InlineData("0")]
    public async Task Leitura_ProgramaComCodigoInexistente_Falha(string codigoInexistente) =>
        await ConferirFalhaDaLeituraAsync(RemoveRestricaoDoPrograma, codigoInexistente, "PRESENCIAL");

    [Theory(DisplayName = "Formato gravado com código que o enum não declara faz a leitura falhar, sem valor padrão")]
    [InlineData("HIBRIDO")]
    [InlineData("ead")]
    [InlineData("")]
    public async Task Leitura_FormatoComCodigoInexistente_Falha(string codigoInexistente) =>
        await ConferirFalhaDaLeituraAsync(RemoveRestricaoDoFormato, "REGULAR", codigoInexistente);

    // A restrição cai só dentro da transação, que nunca é confirmada: o banco volta a recusar o código para os demais testes.
    private async Task ConferirFalhaDaLeituraAsync(string comandoQueRemoveARestricao, string programa, string formato)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync(comandoQueRemoveARestricao);
        await InserirCruNoContextoAsync(
            ctx, cursoId, localId, new LinhaCruDeOferta(programa, formato, "REGULAR", ["MATUTINO"], null, "Lei de teste"));

        Func<Task> act = () => ctx.OfertasCurso.AsNoTracking().Where(o => o.CursoId == cursoId).ToListAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private async Task<Guid> GravarOfertaAsync(string programa, string formato)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso oferta = OfertaCurso.Criar(
            cursoId,
            localId,
            NovaUnidade(),
            new DeclaracaoDaOfertaDeCurso(CodigoCru(), programa, formato, "EXTENSIVO", "REGULAR", ["MATUTINO"], BaseLegal: "Lei de teste")).ValorEsperado();
        await _apoio.GravarAsync(oferta);

        return oferta.Id;
    }

    private async Task<string> TextoDaColunaAsync(string consulta, Guid ofertaId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        return await ctx.Database.SqlQueryRaw<string>(consulta, ofertaId).SingleAsync();
    }

    private async Task<OfertaCurso> LerAsync(Guid ofertaId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        return await ctx.OfertasCurso.AsNoTracking().SingleAsync(o => o.Id == ofertaId);
    }
}
