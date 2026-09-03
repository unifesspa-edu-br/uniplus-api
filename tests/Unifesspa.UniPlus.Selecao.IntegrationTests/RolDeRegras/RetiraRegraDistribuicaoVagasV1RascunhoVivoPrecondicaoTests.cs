namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova executável de que a precondição ADR-0112 da migration
/// <c>RetiraRegraDistribuicaoVagasV1DuplicadaDaV2</c> (issue #1408) também
/// aborta diante de um RASCUNHO VIVO em <c>configuracoes_distribuicao_vagas</c>
/// referenciando a v1 — não só diante de um snapshot já congelado em
/// <c>versoes_configuracao</c>.
/// </summary>
/// <remarks>
/// <see cref="ReferenciaRegra"/> é cópia por valor sem FK (ADR-0061): um
/// rascunho gravado sob v1 antes desta migration carrega código/versão
/// próprios, e a publicação (<c>SnapshotPublicacaoCanonicalizer</c>)
/// serializa esse estado já persistido sem reconsultar <c>rol_de_regras</c>.
/// Sem esta checagem, o rascunho publicaria DEPOIS da remoção e congelaria
/// uma referência a uma linha que já não existe.
/// </remarks>
/// <remarks>
/// Classe própria, com fixture própria: o cenário irmão
/// (<see cref="RetiraRegraDistribuicaoVagasV1PrecondicaoTests"/>) fabrica um
/// congelamento forense e permanente por gatilho no banco compartilhado da
/// sua classe — se este cenário dividisse a mesma fixture, a primeira
/// asserção aqui ("sem rascunho algum, a precondição passa") quebraria por
/// um motivo que não é deste teste.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste, sem valor externo interpolado.")]
public sealed class RetiraRegraDistribuicaoVagasV1RascunhoVivoPrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    // 64 caracteres hex minúsculos — shape de SHA-256 exigido por
    // HashCanonicalComputer.IsValidHashShape; valor arbitrário (não é
    // recomputado, só precisa satisfazer o formato).
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private readonly RegraCatalogoDbFixture _fixture;

    public RetiraRegraDistribuicaoVagasV1RascunhoVivoPrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A precondição aborta diante de RASCUNHO VIVO em v1 — a publicação não reconsulta o catálogo")]
    public async Task Precondicao_AbortaDianteDeRascunhoVivo()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // Sem rascunho algum, a precondição passa: nenhuma linha viva embute a
        // v1 que a migration pretende retirar.
        await ExecutarPrecondicaoAsync(context);

        Guid processoId = await FabricarRascunhoVivoAsync(context, "DISTRIB-VAGAS-INSTITUCIONAL", "v1");
        try
        {
            Func<Task> remocao = () => ExecutarPrecondicaoAsync(context);

            (await remocao.Should().ThrowAsync<DbException>(
                "a precondição aborta diante do rascunho vivo em v1, não só do snapshot congelado"))
                .WithMessage("*ADR-0112*");
        }
        finally
        {
            await using SelecaoDbContext cleanupContext = _fixture.CreateDbContext();
            await cleanupContext.ProcessosSeletivos
                .Where(p => p.Id == processoId)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = "Rascunho vivo sob v2 não aborta — só a v1 duplicada é retirada")]
    public async Task Precondicao_RascunhoVivoSobV2_NaoAborta()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid processoId = await FabricarRascunhoVivoAsync(context, "DISTRIB-VAGAS-INSTITUCIONAL", "v2");
        try
        {
            await ExecutarPrecondicaoAsync(context);
        }
        finally
        {
            await using SelecaoDbContext cleanupContext = _fixture.CreateDbContext();
            await cleanupContext.ProcessosSeletivos
                .Where(p => p.Id == processoId)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }

    private static Task ExecutarPrecondicaoAsync(SelecaoDbContext context) =>
        FronteiraAppendOnlyDoRol.ExecutarAsync(context, RetiraRegraDistribuicaoVagasV1PrecondicaoTests.PrecondicaoDaMigration);

    /// <summary>
    /// Persiste um <see cref="ProcessoSeletivo"/> minimamente conforme com uma
    /// única <see cref="ConfiguracaoDistribuicaoVagas"/> institucional
    /// referenciando <paramref name="codigo"/>/<paramref name="versao"/> — o
    /// bastante para materializar a linha em <c>configuracoes_distribuicao_vagas</c>
    /// que a guarda precisa enxergar, sem depender do ramo federal (Lei
    /// 12.711 exige as 8 modalidades federais + AC, irrelevante aqui).
    /// </summary>
    private static async Task<Guid> FabricarRascunhoVivoAsync(SelecaoDbContext context, string codigo, string versao)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Fronteira append-only — rascunho vivo {codigo}/{versao}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(codigo, versao, HashFixo).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]);
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);

        Result definirResult = processo.DefinirDistribuicaoVagas([distribuicaoResult.Value!], PrecondicaoIfMatch.Ausente);
        definirResult.IsSuccess.Should().BeTrue(definirResult.Error?.Message);

        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync(CancellationToken.None);

        return processo.Id;
    }
}
