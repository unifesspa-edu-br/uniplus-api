namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Publica e divulga o processo com os repositórios de produção, e grava e divulga a retificação que o sucede.</summary>
internal static class PublicacaoDoProcessoDeTeste
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    /// <summary>O congelamento do processo como a publicação o faz, com os valores selecionáveis do conjunto básico e os dos campos.</summary>
    private static SnapshotCanonico CongelarProcesso(
        ProcessoSeletivo processo,
        DadosEdital dados,
        string hash64,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresDosCampos,
        RetificacaoDoEdital? retificacao = null)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresDoConjuntoBasico = ValoresDoConjuntoBasico();
        foreach ((string campo, IReadOnlyList<ValorDominioDeclaradoCongelado>? valores) in valoresDosCampos ?? new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>())
        {
            valoresDoConjuntoBasico[campo] = valores;
        }

        AcrescentarOpcoesDoProcesso(processo, valoresDoConjuntoBasico);
        return Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, hash64, FusoInstitucional.ZoneId, Retificacao: retificacao, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: valoresDoConjuntoBasico, TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));
    }

    // Cada fato de seleção do conjunto básico precisa de uma entrada não vazia.
    private static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresDoConjuntoBasico()
    {
        return new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
        {
            ["NACIONALIDADE"] = [new ValorDominioDeclaradoCongelado("BRASILEIRA", "Nacionalidade brasileira.", 0)],
            ["ESTADO_CIVIL"] = [new ValorDominioDeclaradoCongelado("SOLTEIRO", "Solteiro(a).", 0)],
            ["SEXO"] = [new ValorDominioDeclaradoCongelado("FEMININO", "Feminino.", 0)],
            ["SEXO_CIVIL"] = [new ValorDominioDeclaradoCongelado("FEMININO", "Feminino.", 0)],
            ["IDENTIDADE_GENERO"] = [new ValorDominioDeclaradoCongelado("PREFIRO_NAO_INFORMAR", "Prefiro não informar.", 0)],
            ["ORIENTACAO_SEXUAL"] = [new ValorDominioDeclaradoCongelado("PREFIRO_NAO_INFORMAR", "Prefiro não informar.", 0)],
            ["COR_RACA"] = [new ValorDominioDeclaradoCongelado("NAO_DECLARADO", "Prefere não declarar.", 0)],
            ["TIPO_ENDERECO"] = [new ValorDominioDeclaradoCongelado("URBANO", "Endereço urbano.", 0)],
            ["RG_ORGAO_EMISSOR"] = [new ValorDominioDeclaradoCongelado("SSP", "Secretaria de Segurança Pública (SSP)", 0)],
            ["RG_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
            ["NATURALIDADE_UF"] = [new ValorDominioDeclaradoCongelado("PA", "Pará.", 0)],
        };
    }

    private static void AcrescentarOpcoesDoProcesso(
        ProcessoSeletivo processo, Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresDoConjuntoBasico)
    {
        // O campo cujas opções o processo oferta congela essas opções, pela mesma leitura que o
        // resolvedor da publicação usa.
        foreach (FatoColetado campo in processo.Campos.Where(static c => RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(c) == OrigemValoresColeta.OpcoesDoProcesso))
        {
            valoresDoConjuntoBasico[campo.FatoCodigo] = [.. processo.OpcoesDoProcesso(campo.FatoCodigo)
                .Select(static o => new ValorDominioDeclaradoCongelado(o.Codigo, o.Rotulo, o.Ordem))];
        }
    }

    /// <summary>Publica e divulga o processo com os repositórios de produção, no mesmo <paramref name="db"/>.</summary>
    internal static async Task<(Guid ProcessoId, DadosEdital Dados)> PublicarEDivulgarAsync(
        UniPlusDbContext db,
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresDosCampos = null)
    {
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        DadosEdital dados = DadosDoEdital();
        SnapshotCanonico congelado = CongelarProcesso(processo, dados, HashDoDocumento, valoresDosCampos);
        VersaoConfiguracao versao = Publicar(processo, dados, congelado, HashDoDocumento);
        ProcessoSeletivoRepository processoRepository = ProcessoSeletivoRepositoryDeTeste.Criar(db);
        await processoRepository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
        await DivulgarAsync(db, processo, versao, dados, HashDoDocumento);
        await db.SaveChangesAsync();

        return (processo.Id, dados);
    }

    private const string HashDoDocumento = "3333333333333333333333333333333333333333333333333333333333333333";

    private static DadosEdital DadosDoEdital() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado, string hash64)
    {
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hash64), AtorUsuarioContaId = "integration-test-user", Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico(), MotivosEscolhidos = MotivosDoProcessoDeTeste.Cobrindo(processo) });
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        return publicar.Value!;
    }

    private static async Task DivulgarAsync(UniPlusDbContext db, ProcessoSeletivo processo, VersaoConfiguracao versao, DadosEdital dados, string hash64)
    {
        await new CertameDivulgadoRepository(db).AdicionarAsync(
            CertameDivulgado.Criar(
                processo.Id, versao.NumeroVersao, versao.AtoCriadorId, hash64, versaoProjecao: "1",
                new FacetasDoCertameDivulgado(
                    processo.IdentificadorLegivel!.Value.Valor, processo.Nome, dados.Numero, ["AC"],
                    dados.PeriodoInscricaoInicio, dados.PeriodoInscricaoFim),
                """{"nome":"documento"}""", TimeProvider.System.GetUtcNow()),
            CancellationToken.None);
    }

    /// <summary>Grava <paramref name="retificado"/> como a versão seguinte do processo, sem divulgá-la: a retificação fechada ainda sem publicidade.</summary>
    internal static async Task<VersaoConfiguracao> GravarRetificacaoAsync(UniPlusDbContext db, Guid processoId, ProcessoSeletivo retificado, DadosEdital dados)
    {
        string hash = new('4', 64);
        ProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(db);
        VersaoConfiguracao anterior = (await processos.ObterVersoesAsync(processoId, CancellationToken.None)).MaxBy(static v => v.NumeroVersao)!;
        SnapshotCanonico congelado = CongelarProcesso(retificado, dados, hash, valoresDosCampos: null, new RetificacaoDoEdital(anterior.AtoCriadorId, "Retificação de teste"));
        VersaoConfiguracao sucessora = VersaoConfiguracao.Suceder(
            anterior, new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hash),
            Guid.CreateVersion7(), "integration-test-user", TimeProvider.System.GetUtcNow());
        await processos.AdicionarVersaoConfiguracaoAsync(sucessora, CancellationToken.None);
        await db.SaveChangesAsync();
        return sucessora;
    }

    /// <summary>Faz o certame divulgar <paramref name="versao"/>, a que dá publicidade à retificação.</summary>
    internal static async Task DivulgarAsync(UniPlusDbContext db, Guid processoId, VersaoConfiguracao versao)
    {
        CertameDivulgado divulgado = (await new CertameDivulgadoRepository(db).ObterParaMutacaoAsync(processoId, CancellationToken.None))!;
        divulgado.Avancar(
            versao.NumeroVersao, versao.AtoCriadorId, new string('4', 64), "1",
            new FacetasDoCertameDivulgado(
                divulgado.IdentificadorLegivel, divulgado.Nome, divulgado.Numero, ["AC"], divulgado.InscricoesDe, divulgado.InscricoesAte),
            """{"nome":"documento"}""", TimeProvider.System.GetUtcNow());
        await db.SaveChangesAsync();
    }

    internal static async Task DivulgarRetificacaoAsync(UniPlusDbContext db, Guid processoId, ProcessoSeletivo retificado, DadosEdital dados) =>
        await DivulgarAsync(db, processoId, await GravarRetificacaoAsync(db, processoId, retificado, dados));
}
