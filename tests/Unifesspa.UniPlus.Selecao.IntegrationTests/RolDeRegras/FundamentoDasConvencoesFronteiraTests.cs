namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Prova contra o banco a fronteira da substituição da base legal das três convenções de
/// contagem, quando o placeholder de pendência deu lugar ao fundamento aprovado.
/// </summary>
/// <remarks>
/// <para>
/// Duas populações referenciam essas entradas, e a guarda de configuração congelada só vê
/// uma. A outra nasceu quando o processo passou a declarar a convenção: o servidor congela
/// <c>(código, versão, hash)</c> em coluna própria de <c>processos_seletivos</c>, e um
/// rascunho que já declarou não tem versão publicada — escapa da guarda.
/// </para>
/// <para>
/// Sem o reaponte, esse processo ficaria apontando para um hash que o catálogo não tem
/// mais, já que a substituição é no lugar e não há versão sucessora.
/// </para>
/// <para>
/// O SQL vem da própria migration, não de cópia: copiado, um predicado alterado passaria
/// despercebido e o teste provaria a si mesmo.
/// </para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo vindo da migration, sem valor externo interpolado.")]
public sealed class FundamentoDasConvencoesFronteiraTests : IClassFixture<RegraCatalogoDbFixture>, IAsyncLifetime
{
    private const string ConvencaoDeclarada = AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial;

    /// <summary>Hash da definição anterior à substituição — o que um rascunho teria congelado.</summary>
    private const string HashAnterior =
        "63ef57b6b32c023cdcb4ba9406d84f9e63694d4a9a8ee46bd9b532bbedd08b72";

    private readonly RegraCatalogoDbFixture _fixture;
    private readonly List<Guid> _processosFabricados = [];

    public FundamentoDasConvencoesFronteiraTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Remove o que o fato inseriu. O reaponte varre a tabela inteira, como a migration faz,
    /// então processo deixado por um fato apareceria na contagem do seguinte.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_processosFabricados.Count == 0)
        {
            return;
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        string ids = string.Join(", ", _processosFabricados.Select(id => $"'{id}'"));
        await FronteiraAppendOnlyDoRol.ExecutarAsync(
            context, $"DELETE FROM selecao.processos_seletivos WHERE id IN ({ids});");
    }

    [Fact(DisplayName = "Reaponte_ProcessoQueDeclarouAConvencao_PassaAApontarOHashVigente")]
    public async Task Reaponte_ProcessoQueDeclarouAConvencao_PassaAApontarOHashVigente()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid processoId = await FabricarProcessoQueDeclarouAsync(context, HashAnterior);
        (await LerHashDeclaradoAsync(context, processoId)).Should().Be(
            HashAnterior, "pré-condição: o processo congelou o hash da definição anterior");

        string hashVigente = HashVigenteDe(ConvencaoDeclarada);
        await FronteiraAppendOnlyDoRol.ExecutarAsync(
            context,
            FundamentoDeclaradoPeloEditalNasConvencoesDeContagem.SqlDoReaponteDaReferenciaViva(
                ConvencaoDeclarada, hashVigente));

        (await LerHashDeclaradoAsync(context, processoId)).Should().Be(
            hashVigente,
            "sem versão sucessora, manter o hash anterior deixaria o processo apontando para definição que o catálogo não tem");
    }

    [Fact(DisplayName = "Reaponte_ProcessoQueDeclarouOutraConvencao_NaoEhTocado")]
    public async Task Reaponte_ProcessoQueDeclarouOutraConvencao_NaoEhTocado()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Guid processoId = await FabricarProcessoQueDeclarouAsync(
            context, HashAnterior, codigo: AlgoritmoContagemPrazoCodigo.AvancaDataUtil);

        await FronteiraAppendOnlyDoRol.ExecutarAsync(
            context,
            FundamentoDeclaradoPeloEditalNasConvencoesDeContagem.SqlDoReaponteDaReferenciaViva(
                ConvencaoDeclarada, HashVigenteDe(ConvencaoDeclarada)));

        (await LerHashDeclaradoAsync(context, processoId)).Should().Be(
            HashAnterior,
            "o reaponte nomeia uma convenção por vez — ampliá-lo mexeria em processo que declarou outra");
    }

    [Fact(DisplayName = "Guarda_SemConfiguracaoCongelada_NaoAborta")]
    public async Task Guarda_SemConfiguracaoCongelada_NaoAborta()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        Func<Task> avancar = () => FronteiraAppendOnlyDoRol.ExecutarAsync(
            context,
            FundamentoDeclaradoPeloEditalNasConvencoesDeContagem.SqlDaGuardaDeConfiguracaoCongelada(
                ConvencaoDeclarada));

        await avancar.Should().NotThrowAsync(
            "enquanto a entrada é vocabulário e não fato, substituir a definição no lugar é legítimo");
    }

    [Fact(DisplayName = "Guarda_ComConfiguracaoCongelada_Aborta")]
    public async Task Guarda_ComConfiguracaoCongelada_Aborta()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        await FronteiraAppendOnlyDoRol.FabricarConfiguracaoCongeladaAsync(
            context,
            "fundamento-das-convencoes",
            FronteiraAppendOnlyDoRol.TriplaDeReferencia(ConvencaoDeclarada, "v1"));

        Func<Task> avancar = () => FronteiraAppendOnlyDoRol.ExecutarAsync(
            context,
            FundamentoDeclaradoPeloEditalNasConvencoesDeContagem.SqlDaGuardaDeConfiguracaoCongelada(
                ConvencaoDeclarada));

        (await avancar.Should().ThrowAsync<DbException>(
            "a partir da primeira referência congelada a definição vira fato, e evoluir exige versão sucessora"))
            .WithMessage("*ADR-0112*");
    }

    /// <summary>
    /// O hash sai da migration, que é quem o fixa — repeti-lo aqui deixaria o teste passar
    /// mesmo se a migration passasse a gravar outro.
    /// </summary>
    private static string HashVigenteDe(string codigo) =>
        FundamentoDeclaradoPeloEditalNasConvencoesDeContagem.HashVigenteDe(codigo);

    private async Task<Guid> FabricarProcessoQueDeclarouAsync(
        SelecaoDbContext context, string hash, string codigo = ConvencaoDeclarada)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(
            $"Fundamento das convenções — {codigo}");

        processo.DefinirAlgoritmoContagemPrazo(
            ReferenciaRegra.Criar(codigo, "v1", hash).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync(CancellationToken.None);
        _processosFabricados.Add(processo.Id);

        return processo.Id;
    }

    private static async Task<string> LerHashDeclaradoAsync(SelecaoDbContext context, Guid processoId)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText =
            $"SELECT algoritmo_contagem_prazo_hash FROM selecao.processos_seletivos WHERE id = '{processoId}';";
        return (string)(await comando.ExecuteScalarAsync(CancellationToken.None))!;
    }
}
