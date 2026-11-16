namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeRetificacaoPersistida;

/// <summary>
/// Montagem compartilhada dos testes do seletor da versão vigente (Postgres real): publica a abertura
/// e retifica em seguida pelo caminho de produção (a raiz do agregado), com o relógio que o teste
/// controla, e persiste as duas versões da cadeia. O processo conforme, o documento e os dados do
/// edital vêm de <see cref="CenarioDeRetificacaoPersistida"/>.
/// </summary>
internal sealed class CenarioDeVersaoVigentePersistida
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeVersaoVigentePersistida(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public string ConnectionString => _fixture.ConnectionString;

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public static SnapshotCanonico Canonicalizar(ProcessoSeletivo processo, DadosEdital dados, DocumentoAPublicar documento) =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, documento.HashSha256!, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

    /// <summary>
    /// Publica a abertura e retifica em seguida, avançando o relógio entre as duas. Devolve as duas
    /// versões da cadeia.
    /// </summary>
    public async Task<(Guid ProcessoId, VersaoConfiguracao V1, VersaoConfiguracao V2)> PublicarERetificarAsync(
        string nome,
        RelogioManualDaVigencia clock,
        TimeSpan avancoAteRetificacao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        VersaoConfiguracao v1 = await PublicarEPersistirAsync(processo, clock);

        clock.Avancar(avancoAteRetificacao);

        VersaoConfiguracao v2 = await RetificarAsync(processo.Id, clock);
        return (processo.Id, v1, v2);
    }

    /// <summary>Publica o processo e persiste o processo, o documento do edital e a versão da abertura.</summary>
    public async Task<VersaoConfiguracao> PublicarEPersistirAsync(ProcessoSeletivo processo, TimeProvider clock)
    {
        DocumentoAPublicar documento = DocumentoConfirmado(processo.Id);
        VersaoConfiguracao abertura = Publicar(processo, documento, clock);

        await using UniPlusDbContext escrita = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(escrita);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await escrita.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(abertura, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
        return abertura;
    }

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo, DocumentoAPublicar documento, TimeProvider clock)
    {
        DadosEdital dados = NovosDados(documento.Id);
        SnapshotCanonico canonico = Canonicalizar(processo, dados, documento);
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dados, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            documento.HashSha256!, "integration-test-user", clock, CorpusEnvelopeDadosDoEdital.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        return publicar.Value!;
    }

    private async Task<VersaoConfiguracao> RetificarAsync(Guid processoId, TimeProvider clock)
    {
        DocumentoAPublicar documento = DocumentoConfirmado(processoId);
        await using UniPlusDbContext escrita = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(escrita);
        ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processoId, CancellationToken.None))!;
        VersaoConfiguracao retificacao = Retificar(carregado, versaoAtual, documento, clock);

        await escrita.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(retificacao, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
        return retificacao;
    }

    private static VersaoConfiguracao Retificar(
        ProcessoSeletivo carregado, VersaoConfiguracao versaoAtual, DocumentoAPublicar documento, TimeProvider clock)
    {
        DadosEdital dados = NovosDados(documento.Id);
        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            carregado, dados, documento.HashSha256!, FusoInstitucional.ZoneId,
            new RetificacaoDoEdital(versaoAtual.AtoCriadorId, "Correção do prazo de inscrição"), ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(carregado), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
        Result<VersaoConfiguracao> retificar = carregado.Retificar(
            dados, versaoAtual, canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash,
            documento.HashSha256!, "integration-test-user", "Correção do prazo de inscrição", clock, CorpusEnvelopeDadosDoEdital.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
        return retificar.Value!;
    }
}
