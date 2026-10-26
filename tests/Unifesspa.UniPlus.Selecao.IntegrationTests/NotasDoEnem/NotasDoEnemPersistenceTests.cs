namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Regras.Services;
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

/// <summary>
/// Prova, contra Postgres real, as garantias que o domínio sozinho não dá sobre as notas do ENEM:
/// as restrições do banco, a seleção das inscrições da edição, a vigência do lote mais recente e a
/// regra de que quem trocou de edição deixa de ter nota vigente.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NotasDoEnemPersistenceTests : IClassFixture<InscricaoDbFixture>
{
    private const string Edicao = "2025";
    private static readonly string Hash = new('a', 64);
    private static readonly PersonasDeTeste.PessoaFicticia Fernanda = PersonasDeTeste.Candidatas[0];
    private static readonly PersonasDeTeste.PessoaFicticia Kevin = PersonasDeTeste.Candidatas[1];
    private static readonly PersonasDeTeste.PessoaFicticia Helena = PersonasDeTeste.Candidatas[2];

    private readonly InscricaoDbFixture _fixture;

    public NotasDoEnemPersistenceTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private static ResultadoDaAreaNoRetorno[] Areas(decimal nota = 600m, SituacaoDaProvaNoEnem situacao = SituacaoDaProvaNoEnem.Presente) =>
        [.. AreaDoEnem.Todas.Select(a => new ResultadoDaAreaNoRetorno(a, nota, situacao))];

    private static LoteDeNotasDoEnem NovoLote(
        Guid processoId,
        Guid versaoId,
        Guid etapaId,
        ResultadoDoCruzamento cruzamento,
        string arquivoHash,
        DateTimeOffset em,
        string edicao = Edicao,
        string leiaute = "INEP_ENEM_RETORNO",
        IReadOnlyList<AvaliacaoApurada>? avaliacoes = null) =>
        LoteDeNotasDoEnem.Registrar(
            processoId, etapaId, edicao, leiaute, "v1", Hash, arquivoHash, "sub-admin", em,
            cruzamento.ComNota.Count + cruzamento.LinhasSemInscrito.Count, versaoId, cruzamento, avaliacoes ?? []);

    private static ResultadoDoCruzamento ComNota(params (Guid Inscricao, string NumeroEnem, decimal Nota)[] inscritos) =>
        new([.. inscritos.Select(i => new InscritoComNota(i.Inscricao, i.NumeroEnem, Areas(i.Nota)))], [], [], []);

    private async Task SalvarAsync(LoteDeNotasDoEnem lote)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        await new LoteDeNotasDoEnemRepository(context, _fixture.Encryption).AdicionarAsync(lote);
        await context.SaveChangesAsync();
    }

    private async Task ExecutarAsync(string sql, params NpgsqlParameter[] parametros)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(sql, parametros);
    }

    [Fact(DisplayName = "o banco recusa nota fora de 0 a 1000, lançamento repetido, etapa divergente do lote e inscrito fora do lote")]
    public async Task Constraints_RecusamLancamentosIncoerentes()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid etapa = Guid.CreateVersion7();
        Guid dentro = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        Guid fora = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Kevin, SituacaoInscricao.Submetida, Edicao);
        Guid semLancamento = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Helena, SituacaoInscricao.Submetida, Edicao);
        LoteDeNotasDoEnem lote = NovoLote(processo.Id, versao, etapa, ComNota((dentro, "240000000001", 600m)), Hash, DateTimeOffset.UtcNow);
        await SalvarAsync(lote);
        await ExecutarAsync(
            $"INSERT INTO selecao.inscritos_do_lote_de_notas (id, lote_id, inscricao_id, numero_de_inscricao_no_enem) VALUES ('{Guid.CreateVersion7()}', '{lote.Id}', '{semLancamento}', '240000000003')");

        string Insere(Guid inscricao, Guid etapaDoLancamento, string item, string nota) =>
            $"INSERT INTO selecao.lancamentos_de_nota (id, lote_id, inscricao_id, etapa_processo_id, item_codigo, nota, situacao) VALUES ('{Guid.CreateVersion7()}', '{lote.Id}', '{inscricao}', '{etapaDoLancamento}', '{item}', {nota}, 1)";

        await Recusa(Insere(dentro, etapa, "MATEMATICA", "1000.001"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(dentro, etapa, "MATEMATICA", "-0.001"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(dentro, etapa, "MATEMATICA", "500"), PostgresErrorCodes.UniqueViolation);
        await Recusa(Insere(semLancamento, Guid.CreateVersion7(), "MATEMATICA", "500"), PostgresErrorCodes.ForeignKeyViolation);
        await Recusa(Insere(fora, etapa, "MATEMATICA", "500"), PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact(DisplayName = "as avaliações do lote são gravadas com a nota exata e a versão, e o banco recusa nota sem situação apurada, avaliação repetida e versão inexistente")]
    public async Task Avaliacoes_GravamComOLoteEOBancoRecusaIncoerencia()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid etapa = Guid.CreateVersion7();
        Guid oferta = Guid.CreateVersion7();
        Guid fernanda = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        Guid kevin = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Kevin, SituacaoInscricao.Submetida, Edicao);
        ResultadoDoCruzamento cruzamento = new([new InscritoComNota(fernanda, "240000000001", Areas(600m))], [kevin], [], []);
        decimal dizima = 301m / 3m;
        LoteDeNotasDoEnem lote = NovoLote(
            processo.Id, versao, etapa, cruzamento, Hash, DateTimeOffset.UtcNow,
            avaliacoes:
            [
                new AvaliacaoApurada(fernanda, oferta, "TECNOLOGICA", SituacaoDaAvaliacao.Apurada, dizima),
                new AvaliacaoApurada(kevin, oferta, "TECNOLOGICA", SituacaoDaAvaliacao.InscritoSemLinha, null),
            ]);
        await SalvarAsync(lote);

        await using (SelecaoDbContext leitura = _fixture.CreateDbContext())
        {
            LoteDeNotasDoEnem gravado = await leitura.LotesDeNotasDoEnem.Include(l => l.Avaliacoes).SingleAsync(l => l.Id == lote.Id);
            gravado.VersaoConfiguracaoId.Should().Be(versao);
            gravado.Avaliacoes.Single(a => a.InscricaoId == fernanda).Nota.Should().Be(dizima);
            gravado.Avaliacoes.Single(a => a.InscricaoId == kevin).Nota.Should().BeNull();
        }

        string Insere(Guid inscricao, Guid etapaDaAvaliacao, Guid ofertaDaAvaliacao, int situacao, string nota) =>
            $"INSERT INTO selecao.avaliacoes_da_etapa (id, lote_id, etapa_processo_id, inscricao_id, oferta_curso_id, grupo_area_enem_codigo, situacao, nota) VALUES ('{Guid.CreateVersion7()}', '{lote.Id}', '{etapaDaAvaliacao}', '{inscricao}', '{ofertaDaAvaliacao}', 'TECNOLOGICA', {situacao}, {nota})";
        Guid outraOferta = Guid.CreateVersion7();

        await Recusa(Insere(kevin, etapa, outraOferta, 2, "500"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(kevin, etapa, outraOferta, 1, "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(kevin, etapa, outraOferta, 1, "1000.001"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(kevin, etapa, outraOferta, 9, "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(kevin, etapa, oferta, 2, "NULL"), PostgresErrorCodes.UniqueViolation);
        await Recusa(Insere(kevin, Guid.CreateVersion7(), outraOferta, 2, "NULL"), PostgresErrorCodes.ForeignKeyViolation);
        await ExecutarAsync(Insere(kevin, etapa, outraOferta, 1, "1000"));

        LoteDeNotasDoEnem semVersao = NovoLote(processo.Id, Guid.CreateVersion7(), etapa, ComNota(), new string('d', 64), DateTimeOffset.UtcNow);
        Func<Task> salvar = () => SalvarAsync(semVersao);
        await salvar.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "o banco recusa ocorrência cuja forma não bate com o tipo, inclusive com números de linhas nulos")]
    public async Task Constraints_RecusamOcorrenciaForaDeForma()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid inscricao = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        LoteDeNotasDoEnem lote = NovoLote(processo.Id, versao, Guid.CreateVersion7(), ComNota(), Hash, DateTimeOffset.UtcNow);
        await SalvarAsync(lote);

        string Insere(int tipo, string inscricaoId, string linhas) =>
            $"INSERT INTO selecao.ocorrencias_do_lote_de_notas (id, lote_id, tipo, inscricao_id, numeros_das_linhas) VALUES ('{Guid.CreateVersion7()}', '{lote.Id}', {tipo}, {inscricaoId}, {linhas})";
        string id = $"'{inscricao}'";

        await Recusa(Insere(2, "NULL", "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(3, id, "NULL"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(3, id, "ARRAY[3]"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(1, id, "ARRAY[3]"), PostgresErrorCodes.CheckViolation);
        await Recusa(Insere(2, id, "ARRAY[3]"), PostgresErrorCodes.CheckViolation);
        await ExecutarAsync(Insere(2, "NULL", "ARRAY[7]"));
        await ExecutarAsync(Insere(3, id, "ARRAY[3,4]"));
    }

    private async Task Recusa(string sql, string estado)
    {
        Func<Task> executar = () => ExecutarAsync(sql);

        (await executar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(estado);
    }

    [Fact(DisplayName = "só entram as inscrições enviadas que escolheram a edição; rascunho, cancelada e outra edição ficam de fora")]
    public async Task InscritosDaEdicao_SomenteSubmetidasDaEdicao()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid enviada = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Kevin, SituacaoInscricao.Rascunho, Edicao);
        await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Helena, SituacaoInscricao.Cancelada, Edicao);
        await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, PersonasDeTeste.Candidatas[3], SituacaoInscricao.Submetida, "2024");
        await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, PersonasDeTeste.Candidatas[4], SituacaoInscricao.Submetida, edicao: null);

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        InscritosDaEdicaoDoEnem leitor = new(leitura, _fixture.Encryption);

        IReadOnlyList<InscritoDaEdicao> inscritos = await leitor.ListarAsync(processo.Id, Edicao);
        IReadOnlyList<string> cpfs = await leitor.ListarCpfsAsync(processo.Id, Edicao);

        inscritos.Should().ContainSingle().Which.InscricaoId.Should().Be(enviada);
        inscritos[0].Indice.Should().Be(IndiceCegoDoCpf.De(await new CalculadorDoIndiceCegoDoCpf(_fixture.BlindIndex).ComputarAsync(Fernanda.Cpf)));
        cpfs.Should().Equal(Fernanda.Cpf);
    }

    [Fact(DisplayName = "o segundo lote da edição substitui o primeiro por inteiro: quem não está nele fica sem nota vigente")]
    public async Task Vigencia_SegundoLoteSubstituiOPrimeiroPorInteiro()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid etapa = Guid.CreateVersion7();
        Guid fernanda = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        Guid kevin = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Kevin, SituacaoInscricao.Submetida, Edicao);
        DateTimeOffset agora = DateTimeOffset.UtcNow;

        LoteDeNotasDoEnem primeiro = NovoLote(processo.Id, versao, etapa, ComNota((fernanda, "240000000001", 500m), (kevin, "240000000002", 510m)), new string('b', 64), agora.AddMinutes(-10));
        LoteDeNotasDoEnem segundo = NovoLote(processo.Id, versao, etapa, ComNota((fernanda, "240000000001", 700m)), new string('c', 64), agora);
        await SalvarAsync(primeiro);
        await SalvarAsync(segundo);

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        LoteDeNotasDoEnemRepository repositorio = new(leitura, _fixture.Encryption);
        (await repositorio.ObterVigenteAsync(processo.Id, etapa, Edicao))!.Id.Should().Be(segundo.Id);
        (await repositorio.ListarAsync(processo.Id, etapa, Edicao)).Select(l => l.Id).Should().Equal(segundo.Id, primeiro.Id);

        IReadOnlyList<LancamentoVigente> vigentes = await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(etapa, [fernanda, kevin]);
        vigentes.Should().OnlyContain(l => l.LoteId == segundo.Id && l.InscricaoId == fernanda && l.Nota == 700m);
        vigentes.Should().HaveCount(5);
    }

    [Fact(DisplayName = "quem trocou de edição depois da importação deixa de ter nota vigente, e o leiaute do lote não interfere")]
    public async Task LeitorDeLancamentos_InscricaoQueMudouDeEdicao_SemNotaVigente()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid etapa = Guid.CreateVersion7();
        Guid fernanda = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        Guid kevin = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Kevin, SituacaoInscricao.Submetida, Edicao);
        await SalvarAsync(NovoLote(
            processo.Id, versao, etapa, ComNota((fernanda, "240000000001", 500m), (kevin, "240000000002", 510m)), Hash, DateTimeOffset.UtcNow, leiaute: "OUTRO_LEIAUTE"));

        await InscricoesDoEnemDeTeste.TrocarEdicaoAsync(_fixture, kevin, "2024");

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        IReadOnlyList<LancamentoVigente> vigentes = await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(etapa, [fernanda, kevin]);
        vigentes.Select(l => l.InscricaoId).Distinct().Should().Equal(fernanda);
    }

    [Fact(DisplayName = "quem faltou aparece com situação ausente, e o inscrito sem lançamento não aparece como nota zero")]
    public async Task LeitorDeLancamentos_AusenteAparece_SemLinhaNaoAparece()
    {
        await using SelecaoDbContext semente = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(_fixture, processo.Id);
        Guid etapa = Guid.CreateVersion7();
        Guid fernanda = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Fernanda, SituacaoInscricao.Submetida, Edicao);
        Guid kevin = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(_fixture, processo.Id, Kevin, SituacaoInscricao.Submetida, Edicao);
        ResultadoDoCruzamento cruzamento = new(
            [new InscritoComNota(fernanda, "240000000001", Areas(0m, SituacaoDaProvaNoEnem.Ausente))], [kevin], [], []);
        await SalvarAsync(NovoLote(processo.Id, versao, etapa, cruzamento, Hash, DateTimeOffset.UtcNow));

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        IReadOnlyList<LancamentoVigente> vigentes = await new LancamentosDeNotaReader(leitura).ListarVigentesAsync(etapa, [fernanda, kevin]);

        vigentes.Should().HaveCount(5).And.OnlyContain(l => l.InscricaoId == fernanda && l.Situacao == SituacaoDaProvaNoEnem.Ausente);
    }
}
