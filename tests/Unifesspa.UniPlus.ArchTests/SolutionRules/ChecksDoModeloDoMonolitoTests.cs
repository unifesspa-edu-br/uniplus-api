namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// Fixa o nome e o texto SQL de cada restrição de verificação das tabelas de enum com código de contrato. A leitura é do
/// modelo de design-time do <c>MonolitoDbContext</c> (o que o EF usa para gerar a migration, e que guarda os CHECKs que o
/// modelo de execução pode descartar) e do instantâneo da migration. O texto fica escrito aqui, literal, para que a troca
/// da forma de montá-lo na configuração não mude o que o banco recebe sem que este teste acuse.
/// </summary>
public sealed class ChecksDoModeloDoMonolitoTests
{
    private static readonly RestricaoEsperada[] Esperadas =
    [
        new("fases_canonicas", "ck_fases_canonicas_origem_data", "origem_data IN ('NO_EDITAL', 'NA_EXECUCAO')"),
        new("modalidades", "ck_modalidades_acao_quando_indeferido", "acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ('RECLASSIFICAR_AC', 'RECLASSIFICAR_REGRA_EDITAL')"),
        new("modalidades", "ck_modalidades_composicao_vagas", "composicao_vagas IN ('RESTANTE_DAS_VAGAS_OFERTADAS', 'DENTRO_DA_RESERVA_LEGAL', 'RETIRADA_DE_OUTRA_MODALIDADE', 'SUPLEMENTAR_AO_TOTAL')"),
        new("modalidades", "ck_modalidades_natureza_legal", "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'ACAO_AFIRMATIVA')"),
        new("modalidades", "ck_modalidades_regra_remanejamento", "regra_remanejamento IS NULL OR regra_remanejamento IN ('SEGUE_CASCATA', 'DESTINO_UNICO', 'CRUZADO')"),
        new("modalidades", "ck_modalidades_retirada_de_outra_modalidade_origem", "(composicao_vagas = 'RETIRADA_DE_OUTRA_MODALIDADE') = (composicao_origem IS NOT NULL)"),
        new("termos_de_consentimento", "ck_termos_de_consentimento_forma_aceite_rascunho", "forma_aceite_rascunho IN ('A_DEFINIR', 'REGISTRO_DIGITAL_SEM_LOG_IP', 'REGISTRO_DIGITAL_COM_LOG_IP')"),
        new("dias_nao_uteis", "ck_dias_nao_uteis_municipio_coerente", "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)"),
        new("dias_nao_uteis", "ck_dias_nao_uteis_uf_coerente", "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)"),
        new("versoes_do_termo_consentimento", "ck_versoes_do_termo_consentimento_forma_aceite", "forma_aceite IN ('A_DEFINIR', 'REGISTRO_DIGITAL_SEM_LOG_IP', 'REGISTRO_DIGITAL_COM_LOG_IP')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_base_legal_programa", "programa_de_oferta = 'REGULAR' OR base_legal IS NOT NULL"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_formato_pedagogico", "formato_pedagogico IN ('PRESENCIAL', 'SEMIPRESENCIAL', 'EAD')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_funcionamento_regime_de_turno", "(regime_de_funcionamento <> 'INTENSIVO' OR regime_de_turno = 'INTEGRAL')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_programa_de_oferta", "programa_de_oferta IN ('REGULAR', 'FORMA_PARA', 'PARFOR', 'PRONERA', 'PEPETI', 'CONVENIO_OUTRO', 'OUTRO')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_regime_de_funcionamento", "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO', 'ALTERNANCIA_PEDAGOGICA')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_regime_de_turno", "regime_de_turno IN ('REGULAR', 'INTEGRAL')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_turnos_dominio", "turnos::text[] <@ ARRAY['MATUTINO', 'VESPERTINO', 'NOTURNO']::text[]"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_turnos_regime", "coalesce(array_ndims(turnos), 0) = 1 AND coalesce(array_lower(turnos, 1), 1) = 1 AND ((regime_de_turno = 'REGULAR' AND cardinality(turnos) = 1) OR (regime_de_turno = 'INTEGRAL' AND cardinality(turnos) = 2 AND turnos[1] <> turnos[2]))"),
    ];

    private static readonly Lazy<(IModel Projeto, IModel InstantaneoDaMigration)> Modelos = new(Construir);

    public static TheoryData<string, string, string> RestricoesEsperadas()
    {
        TheoryData<string, string, string> dados = [];
        foreach (RestricaoEsperada esperada in Esperadas)
        {
            dados.Add(esperada.Tabela, esperada.Nome, esperada.Sql);
        }

        return dados;
    }

    [Fact(DisplayName = "A lista esperada tem as 18 restrições de enum com código de contrato")]
    public void ListaEsperada_TemAsDezoitoRestricoes() =>
        Esperadas.Select(esperada => esperada.Nome).Should().OnlyHaveUniqueItems().And.HaveCount(18);

    [Theory(DisplayName = "O modelo de design-time guarda a restrição com o nome e o texto literais")]
    [MemberData(nameof(RestricoesEsperadas))]
    public void ModeloDeProjeto_GuardaARestricaoLiteral(string tabela, string nome, string sql) =>
        RestricoesDa(Modelos.Value.Projeto, tabela).Should().ContainSingle(restricao => restricao.Nome == nome)
            .Which.Sql.Should().Be(sql);

    [Theory(DisplayName = "O instantâneo da migration guarda a restrição com o nome e o texto literais")]
    [MemberData(nameof(RestricoesEsperadas))]
    public void InstantaneoDaMigration_GuardaARestricaoLiteral(string tabela, string nome, string sql) =>
        RestricoesDa(Modelos.Value.InstantaneoDaMigration, tabela).Should().ContainSingle(restricao => restricao.Nome == nome)
            .Which.Sql.Should().Be(sql);

    private static List<(string? Nome, string Sql)> RestricoesDa(IModel modelo, string tabela) =>
    [
        .. modelo.GetEntityTypes()
            .Where(entidade => entidade.GetTableName() == tabela)
            .SelectMany(entidade => entidade.GetCheckConstraints())
            .Select(restricao => (restricao.Name, restricao.Sql)),
    ];

    private static (IModel Projeto, IModel InstantaneoDaMigration) Construir()
    {
        (Type fabrica, _, System.Reflection.MethodInfo criar) = ModelosDaSolucao.FabricasDeContexto()
            .Single(f => f.Contexto.Name == "MonolitoDbContext");
        object instanciaDaFabrica = Activator.CreateInstance(fabrica)
            ?? throw new InvalidOperationException("A fábrica do contexto do monólito não pôde ser instanciada.");
        using DbContext contexto = criar.Invoke(instanciaDaFabrica, [Array.Empty<string>()]) as DbContext
            ?? throw new InvalidOperationException("A fábrica do contexto do monólito não devolveu um contexto.");
        IModel instantaneo = contexto.GetService<IMigrationsAssembly>().ModelSnapshot?.Model
            ?? throw new InvalidOperationException("A migration do monólito não tem instantâneo do modelo.");

        return (contexto.GetService<IDesignTimeModel>().Model, instantaneo);
    }

    private sealed record RestricaoEsperada(string Tabela, string Nome, string Sql);
}
