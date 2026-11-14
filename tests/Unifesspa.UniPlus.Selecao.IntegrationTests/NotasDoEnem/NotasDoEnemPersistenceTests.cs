namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.LotesDoEnemDeTeste;

/// <summary>
/// Prova, contra Postgres real, as garantias que o domínio sozinho não dá sobre as notas do ENEM:
/// as restrições do banco, a seleção das inscrições da edição, a vigência do lote mais recente e a
/// regra de que quem trocou de edição deixa de ter nota vigente.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NotasDoEnemPersistenceTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly PersonasDeTeste.PessoaFicticia Fernanda = PersonasDeTeste.Candidatas[0];
    private static readonly PersonasDeTeste.PessoaFicticia Kevin = PersonasDeTeste.Candidatas[1];
    private static readonly PersonasDeTeste.PessoaFicticia Helena = PersonasDeTeste.Candidatas[2];
    private static readonly decimal Dizima = 301m / 3m;

    private readonly InscricaoDbFixture _fixture;

    public NotasDoEnemPersistenceTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private sealed record LoteComAvaliacoes(Escopo Escopo, LoteDeNotasDoEnem Lote, Guid Fernanda, Guid Kevin, Guid Oferta);

    [Fact(DisplayName = "o banco recusa nota fora de 0 a 1000, lançamento repetido, etapa divergente do lote e inscrito fora do lote")]
    public async Task Constraints_RecusamLancamentosIncoerentes()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid dentro = await InscreverAsync(_fixture, escopo, Fernanda);
        Guid fora = await InscreverAsync(_fixture, escopo, Kevin);
        Guid semLancamento = await InscreverAsync(_fixture, escopo, Helena);
        LoteDeNotasDoEnem lote = NovoLote(escopo, ComNota((dentro, "240000000001", 600m)), Hash, DateTimeOffset.UtcNow);
        await SalvarAsync(_fixture, lote);
        await ExecutarAsync(InsereInscritoSemLancamento(lote.Id, semLancamento));

        await Recusa(InsereLancamento(lote.Id, dentro, escopo.EtapaId, "1000.001"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereLancamento(lote.Id, dentro, escopo.EtapaId, "-0.001"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereLancamento(lote.Id, dentro, escopo.EtapaId, "500"), PostgresErrorCodes.UniqueViolation);
        await Recusa(InsereLancamento(lote.Id, semLancamento, Guid.CreateVersion7(), "500"), PostgresErrorCodes.ForeignKeyViolation);
        await Recusa(InsereLancamento(lote.Id, fora, escopo.EtapaId, "500"), PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact(DisplayName = "as avaliações do lote são gravadas com a nota exata e a versão")]
    public async Task Avaliacoes_GravamComANotaExataEAVersao()
    {
        LoteComAvaliacoes cenario = await GravarLoteComAvaliacoesAsync();

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        LoteDeNotasDoEnem gravado = await leitura.LotesDeNotasDoEnem.Include(l => l.Avaliacoes).SingleAsync(l => l.Id == cenario.Lote.Id);
        gravado.VersaoConfiguracaoId.Should().Be(cenario.Escopo.VersaoId);
        gravado.Avaliacoes.Single(a => a.InscricaoId == cenario.Fernanda).Nota.Should().Be(Dizima);
        gravado.Avaliacoes.Single(a => a.InscricaoId == cenario.Kevin).Nota.Should().BeNull();
    }

    [Fact(DisplayName = "o banco recusa avaliação com nota sem situação apurada, repetida ou de etapa divergente do lote")]
    public async Task Avaliacoes_BancoRecusaIncoerencia()
    {
        LoteComAvaliacoes cenario = await GravarLoteComAvaliacoesAsync();
        Guid etapa = cenario.Escopo.EtapaId;
        Guid outraOferta = Guid.CreateVersion7();

        await Recusa(InsereAvaliacao(cenario, etapa, outraOferta, 2, "500"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereAvaliacao(cenario, etapa, outraOferta, 1, "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereAvaliacao(cenario, etapa, outraOferta, 1, "1000.001"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereAvaliacao(cenario, etapa, outraOferta, 9, "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereAvaliacao(cenario, etapa, cenario.Oferta, 2, "NULL"), PostgresErrorCodes.UniqueViolation);
        await Recusa(InsereAvaliacao(cenario, Guid.CreateVersion7(), outraOferta, 2, "NULL"), PostgresErrorCodes.ForeignKeyViolation);
        await ExecutarAsync(InsereAvaliacao(cenario, etapa, outraOferta, 1, "1000"));
    }

    [Fact(DisplayName = "o banco recusa o lote de uma versão da configuração inexistente")]
    public async Task Lote_VersaoInexistente_BancoRecusa()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        LoteDeNotasDoEnem semVersao = NovoLote(escopo with { VersaoId = Guid.CreateVersion7() }, ComNota(), Hash, DateTimeOffset.UtcNow);

        Func<Task> salvar = () => SalvarAsync(_fixture, semVersao);

        await salvar.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "o banco recusa ocorrência cuja forma não bate com o tipo, inclusive com números de linhas nulos")]
    public async Task Constraints_RecusamOcorrenciaForaDeForma()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        string inscricao = $"'{await InscreverAsync(_fixture, escopo, Fernanda)}'";
        LoteDeNotasDoEnem lote = NovoLote(escopo, ComNota(), Hash, DateTimeOffset.UtcNow);
        await SalvarAsync(_fixture, lote);

        await Recusa(InsereOcorrencia(lote.Id, 2, "NULL", "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereOcorrencia(lote.Id, 3, inscricao, "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereOcorrencia(lote.Id, 3, inscricao, "ARRAY[3]"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereOcorrencia(lote.Id, 1, inscricao, "ARRAY[3]"), PostgresErrorCodes.CheckViolation);
        await Recusa(InsereOcorrencia(lote.Id, 2, inscricao, "ARRAY[3]"), PostgresErrorCodes.CheckViolation);
        await ExecutarAsync(InsereOcorrencia(lote.Id, 2, "NULL", "ARRAY[7]"));
        await ExecutarAsync(InsereOcorrencia(lote.Id, 3, inscricao, "ARRAY[3,4]"));
    }

    [Fact(DisplayName = "só entram as inscrições enviadas que escolheram a edição; rascunho, cancelada e outra edição ficam de fora")]
    public async Task InscritosDaEdicao_SomenteSubmetidasDaEdicao()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid enviada = await InscreverAsync(_fixture, escopo, Fernanda);
        await InscreverAsync(_fixture, escopo, Kevin, SituacaoInscricao.Rascunho);
        await InscreverAsync(_fixture, escopo, Helena, SituacaoInscricao.Cancelada);
        await InscreverAsync(_fixture, escopo, PersonasDeTeste.Candidatas[3], edicao: "2024");
        await InscreverAsync(_fixture, escopo, PersonasDeTeste.Candidatas[4], edicao: null);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        InscritosDaEdicaoDoEnem leitor = new(leitura, _fixture.Encryption);
        IReadOnlyList<InscritoDaEdicao> inscritos = await leitor.ListarAsync(escopo.ProcessoId, Edicao);

        inscritos.Should().ContainSingle().Which.InscricaoId.Should().Be(enviada);
        inscritos[0].Indice.Should().Be(IndiceCegoDoCpf.De(await new CalculadorDoIndiceCegoDoCpf(_fixture.BlindIndex).ComputarAsync(Fernanda.Cpf)));
        (await leitor.ListarCpfsAsync(escopo.ProcessoId, Edicao)).Should().Equal(Fernanda.Cpf);
    }

    [Fact(DisplayName = "o segundo lote da edição substitui o primeiro por inteiro: quem não está nele fica sem nota vigente")]
    public async Task Vigencia_SegundoLoteSubstituiOPrimeiroPorInteiro()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid fernanda = await InscreverAsync(_fixture, escopo, Fernanda);
        Guid kevin = await InscreverAsync(_fixture, escopo, Kevin);
        LoteDeNotasDoEnem primeiro = NovoLote(
            escopo, ComNota((fernanda, "240000000001", 500m), (kevin, "240000000002", 510m)), new string('b', 64), DateTimeOffset.UtcNow.AddMinutes(-10));
        LoteDeNotasDoEnem segundo = NovoLote(escopo, ComNota((fernanda, "240000000001", 700m)), new string('c', 64), DateTimeOffset.UtcNow);
        await SalvarAsync(_fixture, primeiro, segundo);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        LoteDeNotasDoEnemRepository repositorio = new(leitura, _fixture.Encryption);
        (await repositorio.ObterVigenteAsync(escopo.ProcessoId, escopo.EtapaId, Edicao))!.Id.Should().Be(segundo.Id);
        (await repositorio.ListarAsync(escopo.ProcessoId, escopo.EtapaId, Edicao)).Select(l => l.Id).Should().Equal(segundo.Id, primeiro.Id);
        (await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(escopo.EtapaId, [fernanda, kevin])).Should().HaveCount(5)
            .And.OnlyContain(l => l.LoteId == segundo.Id && l.InscricaoId == fernanda && l.Nota == 700m);
    }

    [Fact(DisplayName = "quem trocou de edição depois da importação deixa de ter nota vigente, e o leiaute do lote não interfere")]
    public async Task LeitorDeLancamentos_InscricaoQueMudouDeEdicao_SemNotaVigente()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid fernanda = await InscreverAsync(_fixture, escopo, Fernanda);
        Guid kevin = await InscreverAsync(_fixture, escopo, Kevin);
        await SalvarAsync(_fixture, NovoLote(
            escopo, ComNota((fernanda, "240000000001", 500m), (kevin, "240000000002", 510m)), Hash, DateTimeOffset.UtcNow, leiaute: "OUTRO_LEIAUTE"));

        await InscricoesDoEnemDeTeste.TrocarEdicaoAsync(_fixture, kevin, "2024");

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        IReadOnlyList<LancamentoVigente> vigentes = await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(escopo.EtapaId, [fernanda, kevin]);
        vigentes.Select(l => l.InscricaoId).Distinct().Should().Equal(fernanda);
    }

    [Fact(DisplayName = "quem faltou aparece com situação ausente, e o inscrito sem lançamento não aparece como nota zero")]
    public async Task LeitorDeLancamentos_AusenteAparece_SemLinhaNaoAparece()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid fernanda = await InscreverAsync(_fixture, escopo, Fernanda);
        Guid kevin = await InscreverAsync(_fixture, escopo, Kevin);
        ResultadoDoCruzamento cruzamento = new(
            [new InscritoComNota(fernanda, "240000000001", Areas(0m, SituacaoDaProvaNoEnem.Ausente))], [kevin], [], []);
        await SalvarAsync(_fixture, NovoLote(escopo, cruzamento, Hash, DateTimeOffset.UtcNow));

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        IReadOnlyList<LancamentoVigente> vigentes = await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(escopo.EtapaId, [fernanda, kevin]);

        vigentes.Should().HaveCount(5).And.OnlyContain(l => l.InscricaoId == fernanda && l.Situacao == SituacaoDaProvaNoEnem.Ausente);
    }

    private async Task<LoteComAvaliacoes> GravarLoteComAvaliacoesAsync()
    {
        Escopo escopo = await NovoEscopoAsync(_fixture);
        Guid oferta = Guid.CreateVersion7();
        Guid fernanda = await InscreverAsync(_fixture, escopo, Fernanda);
        Guid kevin = await InscreverAsync(_fixture, escopo, Kevin);
        LoteDeNotasDoEnem lote = NovoLote(
            escopo, new([new InscritoComNota(fernanda, "240000000001", Areas(600m))], [kevin], [], []), Hash, DateTimeOffset.UtcNow,
            avaliacoes:
            [
                new AvaliacaoApurada(fernanda, oferta, "TECNOLOGICA", SituacaoDaAvaliacao.Apurada, Dizima),
                new AvaliacaoApurada(kevin, oferta, "TECNOLOGICA", SituacaoDaAvaliacao.InscritoSemLinha, null),
            ]);
        await SalvarAsync(_fixture, lote);
        return new LoteComAvaliacoes(escopo, lote, fernanda, kevin, oferta);
    }

    private static string InsereInscritoSemLancamento(Guid loteId, Guid inscricao) =>
        $"INSERT INTO selecao.inscritos_do_lote_de_notas (id, lote_id, inscricao_id, numero_de_inscricao_no_enem) VALUES ('{Guid.CreateVersion7()}', '{loteId}', '{inscricao}', '240000000003')";

    private static string InsereLancamento(Guid loteId, Guid inscricao, Guid etapa, string nota) =>
        $"INSERT INTO selecao.lancamentos_de_nota (id, lote_id, inscricao_id, etapa_processo_id, item_codigo, nota, situacao) VALUES ('{Guid.CreateVersion7()}', '{loteId}', '{inscricao}', '{etapa}', 'MATEMATICA', {nota}, 1)";

    private static string InsereAvaliacao(LoteComAvaliacoes cenario, Guid etapa, Guid oferta, int situacao, string nota) =>
        $"INSERT INTO selecao.avaliacoes_da_etapa (id, lote_id, etapa_processo_id, inscricao_id, oferta_curso_id, grupo_area_enem_codigo, situacao, nota) VALUES ('{Guid.CreateVersion7()}', '{cenario.Lote.Id}', '{etapa}', '{cenario.Kevin}', '{oferta}', 'TECNOLOGICA', {situacao}, {nota})";

    private static string InsereOcorrencia(Guid loteId, int tipo, string inscricaoId, string linhas) =>
        $"INSERT INTO selecao.ocorrencias_do_lote_de_notas (id, lote_id, tipo, inscricao_id, numeros_das_linhas) VALUES ('{Guid.CreateVersion7()}', '{loteId}', {tipo}, {inscricaoId}, {linhas})";

    private async Task ExecutarAsync(string sql)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task Recusa(string sql, string estado)
    {
        Func<Task> executar = () => ExecutarAsync(sql);

        (await executar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(estado);
    }
}
