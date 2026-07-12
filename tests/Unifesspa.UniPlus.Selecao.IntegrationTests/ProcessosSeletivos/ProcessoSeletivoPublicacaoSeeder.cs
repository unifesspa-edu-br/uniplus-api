namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Semeia um Processo Seletivo conforme e o publica pelo caminho real do
/// agregado (Publicar → Edital + <see cref="VersaoConfiguracao"/>), persistindo
/// tudo numa transação. É o ponto de partida dos testes que precisam de um
/// certame já publicado para exercitar o que vem DEPOIS — em especial os que
/// atacam a tabela de versões com SQL cru.
/// </summary>
internal static class ProcessoSeletivoPublicacaoSeeder
{
    private static readonly string HashDocumento = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    internal sealed record Resultado(Guid ProcessoId, Guid EditalId, Guid VersaoId);

    private static ReferenciaRegra Regra(string codigo, char hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar, 64)).Value!;

    /// <summary>
    /// Processo com as quatro dimensões estruturalmente obrigatórias
    /// preenchidas (etapas, atendimento, distribuição de vagas, classificação)
    /// — o mínimo que <c>AvaliarConformidade</c> exige para publicar.
    /// </summary>
    public static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021").Value!;

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao]).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>
    /// Publica o processo pelo agregado e persiste raiz, documento e versão 1
    /// da configuração — o mesmo caminho do handler, sem a pipeline HTTP.
    /// </summary>
    public static async Task<Resultado> PublicarAsync(ProcessoSeletivoDbFixture fixture, string nome)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        ProcessoSeletivo processo = NovoProcessoConforme(nome);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashDocumento, TimeProvider.System).IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: documento.Id).Value!;

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(processo, dados, documento.HashSha256!);

        Result<PublicacaoResultado> publicarResult = processo.Publicar(
            dados,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "integration-test-user",
            new DateTimeOffset(2026, 3, 13, 0, 0, 0, TimeSpan.Zero), TimeProvider.System);
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        await using SelecaoDbContext context = fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await context.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(publicarResult.Value!.Versao, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Resultado(
            processo.Id,
            publicarResult.Value!.Edital.Id,
            publicarResult.Value!.Versao.Id);
    }
}
