namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.LotesDoEnemDeTeste;

/// <summary>
/// Dois lotes do mesmo processo, etapa e edição importados no mesmo microssegundo: vale o registrado por último, mesmo
/// quando ele tem o identificador menor. O lote, os lançamentos e as avaliações concordam sobre qual vale.
/// </summary>
[Trait("Category", "Integration")]
public sealed class VigenciaDosLotesDoEnemEmEmpateTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly DateTimeOffset MesmoInstante = new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    private readonly InscricaoDbFixture _fixture;

    public VigenciaDosLotesDoEnemEmEmpateTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private sealed record Cenario(Escopo Escopo, Guid InscricaoId, LoteDeNotasDoEnem RegistradoAntes, LoteDeNotasDoEnem RegistradoDepois);

    [Fact(DisplayName = "Com o mesmo instante de importação, o lote vigente é o registrado por último, ainda que de identificador menor")]
    public async Task MesmoInstante_VigenteEOUltimoRegistrado()
    {
        Cenario cenario = await RegistrarEmEmpateAsync();

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        LoteDeNotasDoEnemRepository repositorio = new(leitura, _fixture.Encryption);
        (await repositorio.ObterVigenteAsync(cenario.Escopo.ProcessoId, cenario.Escopo.EtapaId, Edicao))!.Id.Should().Be(cenario.RegistradoDepois.Id);
        (await repositorio.ListarAsync(cenario.Escopo.ProcessoId, cenario.Escopo.EtapaId, Edicao)).Select(l => l.Id)
            .Should().Equal(cenario.RegistradoDepois.Id, cenario.RegistradoAntes.Id);
    }

    [Fact(DisplayName = "Com o mesmo instante de importação, os lançamentos e as avaliações vigentes são os do lote registrado por último")]
    public async Task MesmoInstante_LancamentosEAvaliacoesDoUltimoRegistrado()
    {
        Cenario cenario = await RegistrarEmEmpateAsync();

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        IReadOnlyList<LancamentoVigente> lancamentos = await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(cenario.Escopo.EtapaId, [cenario.InscricaoId]);
        IReadOnlyList<AvaliacaoVigente> avaliacoes = await new AvaliacoesDaEtapaReader(leitura).ListarVigentesAsync(cenario.Escopo.EtapaId, [cenario.InscricaoId]);

        lancamentos.Should().NotBeEmpty().And.OnlyContain(l => l.LoteId == cenario.RegistradoDepois.Id);
        avaliacoes.Should().ContainSingle().Which.LoteId.Should().Be(cenario.RegistradoDepois.Id);
    }

    [Fact(DisplayName = "A ordem de registro do lote não sai no contrato")]
    public void OrdemDeRegistro_ForaDoContrato() =>
        typeof(LoteDeNotasDoEnemDto).GetProperties().Select(p => p.Name).Should().NotContain(LoteDeNotasDoEnemConfiguration.OrdemDeRegistro);

    private async Task<Cenario> RegistrarEmEmpateAsync()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid oferta = Guid.CreateVersion7();
        Guid inscricao = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(
            _fixture, escopo.ProcessoId, PersonasDeTeste.Candidatas[0], SituacaoInscricao.Submetida, Edicao, ofertaCursoId: oferta);

        (LoteDeNotasDoEnem deIdMenor, LoteDeNotasDoEnem deIdMaior) = PorIdentificador(
            LoteCom(escopo, inscricao, oferta, 500m, new string('b', 64)),
            LoteCom(escopo, inscricao, oferta, 700m, new string('c', 64)));
        await SalvarAsync(_fixture, deIdMaior, deIdMenor);
        return new Cenario(escopo, inscricao, RegistradoAntes: deIdMaior, RegistradoDepois: deIdMenor);
    }

    // O Postgres ordena o uuid pela forma textual, que é a ordem do desempate antigo pelo identificador.
    private static (LoteDeNotasDoEnem Menor, LoteDeNotasDoEnem Maior) PorIdentificador(LoteDeNotasDoEnem um, LoteDeNotasDoEnem outro) =>
        string.CompareOrdinal(um.Id.ToString(), outro.Id.ToString()) < 0 ? (um, outro) : (outro, um);

    private static LoteDeNotasDoEnem LoteCom(Escopo escopo, Guid inscricaoId, Guid ofertaId, decimal nota, string arquivoHash) =>
        NovoLote(
            escopo, ComNota((inscricaoId, "240000000001", nota)), arquivoHash, MesmoInstante,
            avaliacoes: [new AvaliacaoApurada(inscricaoId, ofertaId, "TECNOLOGICA", SituacaoDaAvaliacao.Apurada, nota)]);
}
