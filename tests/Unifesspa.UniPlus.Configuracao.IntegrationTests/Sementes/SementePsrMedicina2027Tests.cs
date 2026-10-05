namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// A configuração do edital de Medicina 2027 existe ao subir o sistema, gravada pela migration
/// <c>SemeiaPsrMedicina2027</c>, e é lida pelos mesmos caminhos que a aplicação usa.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class SementePsrMedicina2027Tests
{
    private static readonly string[] CodigosDosFatos =
    [
        "TIPO_ENDERECO", "NOME_COMUNIDADE", "SOLICITA_BONUS_REGIONAL", "MUNICIPIO_EM_AREA_BONUS", "FORMA_CONCLUSAO_EM",
        "CERTIFICADO_EM_EMITIDO", "HABILITACAO_POR_PROCURADOR", "VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", "CATEGORIA_RENDA",
        "CATEGORIAS_RENDA_FAMILIA",
    ];

    private readonly ConfiguracaoDbFixture _fixture;

    public SementePsrMedicina2027Tests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Depois das migrations, o tipo PSR, os termos promovidos e os fatos do administrador, com descrição e valores, são lidos")]
    public async Task Migrations_GravamOTipoOsTermosEOsFatos()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        TipoProcesso tipo = await ctx.TiposProcesso.SingleAsync(t => t.Codigo == SementePsrMedicina2027.TipoProcessoCodigo);
        tipo.Nome.Should().Be("Processo Seletivo Regular Unificado");
        tipo.Ativo.Should().BeTrue();

        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await new TermoConsentimentoReader(ctx).ListarVersoesAsync(
            [.. SementePsrMedicina2027.Termos.Select(static t => t.VersaoId)], CancellationToken.None);
        versoes.Select(static v => v.TermoId).Should().BeEquivalentTo(SementePsrMedicina2027.Termos.Select(static t => t.TermoId));
        versoes.Should().OnlyContain(static v => v.FormaAceite == "REGISTRO_DIGITAL_COM_LOG_IP" && v.Hash.Length == 64);

        List<FatoCandidato> fatos = await ctx.FatosCandidato
            .Include(static f => f.ValoresDominioDeclarados)
            .Where(f => CodigosDosFatos.Contains(f.Codigo))
            .ToListAsync();
        fatos.Select(static f => f.Codigo).Should().BeEquivalentTo(CodigosDosFatos);
        fatos.Should().OnlyContain(static f => !f.Sistema && f.Ativo && !string.IsNullOrWhiteSpace(f.Descricao));
        fatos.Single(static f => f.Codigo == "CATEGORIA_RENDA").ValoresDominioDeclarados.Should().HaveCount(7);
        fatos.Single(static f => f.Codigo == "CATEGORIAS_RENDA_FAMILIA").Binding.Should().Be("AGREGACAO_GRUPO:CATEGORIA_RENDA");
    }

    [Fact(DisplayName = "Reaplicar a semente não duplica nem altera o que já existe")]
    public async Task Reaplicar_NaoDuplicaNemAltera()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        string antes = await RetratoAsync(ctx);

        foreach (string comando in SementePsrMedicina2027.ComandosDoTipoTermosEFatos())
        {
            await ctx.Database.ExecuteSqlRawAsync(comando);
        }

        (await RetratoAsync(ctx)).Should().Be(antes);
    }

    [Fact(DisplayName = "Fato que o domínio recusa derruba a geração da semente, em vez de virar linha que a API não aceita")]
    public void FatoRecusadoPeloDominio_DerrubaAGeracao()
    {
        SementePsrMedicina2027.FatoDaSemente invalido = new(
            99,
            _ => FatoCandidato.CriarDoAdministrador(
                "codigo fora do padrao", "Fato inválido", "Descrição", DominioFato.Booleano, CardinalidadeFato.Escalar,
                null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, "Finalidade",
                HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            []);

        Action gerar = () => SementePsrMedicina2027.ComandosDosFatos([invalido]);

        gerar.Should().Throw<InvalidOperationException>().WithMessage("*fato 99*");
    }

    /// <summary>A contagem e o conteúdo das linhas que a semente grava, para comparar antes e depois.</summary>
    private static async Task<string> RetratoAsync(ConfiguracaoDbContext ctx)
    {
        List<string> linhas = await ctx.Database.SqlQueryRaw<string>(
            """
            SELECT concat_ws('|', 'tipo', codigo, nome) AS "Value" FROM configuracao.tipos_processo WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'termo', nome, texto_rascunho) FROM configuracao.termo_consentimento WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'versao', hash) FROM configuracao.termo_consentimento_versao WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'fato', codigo, nome, descricao) FROM configuracao.rol_de_fatos_candidato WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'valor', codigo, descricao) FROM configuracao.fato_valor_dominio WHERE id::text LIKE '5e3d%'
            """).ToListAsync();
        return string.Join("\n", linhas.Order(StringComparer.Ordinal));
    }
}
