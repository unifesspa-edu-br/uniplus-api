namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Xunit;

/// <summary>
/// Os limites que o decoder impõe <b>são os das colunas</b> — e continuam sendo (Story #859).
/// </summary>
/// <remarks>
/// <para>
/// O decoder recusa um <c>etapas[].nome</c> de 301 caracteres porque a coluna comporta 300.
/// Mas o valor <c>300</c> está escrito em <b>dois</b> lugares — na
/// <c>EtapaProcessoConfiguration</c> e em <c>LimitesDoEnvelope</c> — e duas listas manuais
/// divergem. No dia em que alguém alargar a coluna para 500 e esquecer do codec, o decoder
/// passa a recusar configuração <b>legítima</b>; no dia em que alguém a <b>estreitar</b>
/// para 100, o decoder passa a aceitar o que o banco recusa, e o descarte volta a morrer
/// com <c>DbUpdateException</c> — <b>500 não tratado</b>, que é exatamente o que os limites
/// existem para evitar.
/// </para>
/// <para>
/// Este teste lê o <b>modelo do EF Core</b> — a fonte de verdade do schema — e compara
/// coluna a coluna. Não é um teste de banco: <c>DbContext.Model</c> é construído em memória,
/// sem conexão. Mexer num <c>HasMaxLength</c> ou num <c>HasPrecision</c> sem mexer no codec
/// <b>quebra o build</b>.
/// </para>
/// </remarks>
public sealed class LimitesDoEnvelopeBatemComOSchemaTests
{
    /// <summary>
    /// Os limites que o decoder aplica, e a coluna de onde cada um tem de vir. Os nomes são
    /// os das constantes de <c>LimitesDoEnvelope</c> — referenciadas aqui, nunca recopiadas.
    /// </summary>
    private static readonly (string Nome, int ValorNoCodec, Type Entidade, string Propriedade)[] Comprimentos =
    [
        ("EtapaNome", LimitesDoEnvelope.EtapaNome, typeof(EtapaProcesso), nameof(EtapaProcesso.Nome)),
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.Codigo)),
        ("ModalidadeDescricao", LimitesDoEnvelope.ModalidadeDescricao, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.Descricao)),
        ("Token", LimitesDoEnvelope.Token, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.AcaoQuandoIndeferido)),
        ("BaseLegal", LimitesDoEnvelope.BaseLegal, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.BaseLegal)),
        ("CondicaoCodigo", LimitesDoEnvelope.CondicaoCodigo, typeof(OfertaCondicao), nameof(OfertaCondicao.CondicaoCodigo)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(OfertaCondicao), nameof(OfertaCondicao.CondicaoNome)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(OfertaRecurso), nameof(OfertaRecurso.RecursoNome)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(OfertaTipoDeficiencia), nameof(OfertaTipoDeficiencia.TipoDeficienciaNome)),
        ("MunicipioConvenio", LimitesDoEnvelope.MunicipioConvenio, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.MunicipioConvenio)),
        ("BaseLegal", LimitesDoEnvelope.BaseLegal, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.BaseLegal)),

        // Story #851 — cronograma de fases.
        ("FaseCodigo", LimitesDoEnvelope.FaseCodigo, typeof(FaseCronograma), nameof(FaseCronograma.Codigo)),
        ("DonoInstitucional", LimitesDoEnvelope.DonoInstitucional, typeof(FaseCronograma), nameof(FaseCronograma.DonoInstitucional)),
        ("TipoAtoCodigo", LimitesDoEnvelope.TipoAtoCodigo, typeof(FaseCronograma), nameof(FaseCronograma.AtoProduzidoCodigo)),
        ("TipoBancaCodigo", LimitesDoEnvelope.TipoBancaCodigo, typeof(BancaRequerida), nameof(BancaRequerida.Codigo)),

        // Story #554 (PR-e) — exigencias[] real.
        ("TipoDocumentoCodigo", LimitesDoEnvelope.TipoDocumentoCodigo, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCodigo)),
        ("TipoDocumentoNome", LimitesDoEnvelope.TipoDocumentoNome, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoNome)),
        ("TipoDocumentoCategoria", LimitesDoEnvelope.TipoDocumentoCategoria, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCategoria)),
        ("Token", LimitesDoEnvelope.Token, typeof(DocumentoExigido), nameof(DocumentoExigido.ConsequenciaIndeferimento)),
        ("BaseLegal", LimitesDoEnvelope.BaseLegal, typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Referencia)),
        ("ObservacaoBaseLegal", LimitesDoEnvelope.ObservacaoBaseLegal, typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Observacao)),
        ("Fato", LimitesDoEnvelope.Fato, typeof(CondicaoGatilho), nameof(CondicaoGatilho.Fato)),
    ];

    private static readonly (string Nome, int PrecisaoNoCodec, int EscalaNoCodec, Type Entidade, string Propriedade)[] Precisoes =
    [
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(EtapaProcesso), nameof(EtapaProcesso.Peso)),
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(EtapaProcesso), nameof(EtapaProcesso.NotaMinima)),
        ("PrecisaoBonus", LimitesDoEnvelope.PrecisaoBonus, 4, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Fator)),
        ("PrecisaoBonus", LimitesDoEnvelope.PrecisaoBonus, 4, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Teto)),
        ("PrecisaoPr", LimitesDoEnvelope.PrecisaoPr, 4, typeof(ConfiguracaoDistribuicaoVagas), nameof(ConfiguracaoDistribuicaoVagas.Pr)),
    ];

    [Fact(DisplayName = "Todo limite de COMPRIMENTO do decoder é o da coluna que vai receber o valor")]
    public void Comprimentos_BatemComOSchema()
    {
        using SelecaoDbContext contexto = ContextoSoParaOModelo();

        foreach ((string nome, int valorNoCodec, Type entidade, string propriedade) in Comprimentos)
        {
            int? doSchema = contexto.Model
                .FindEntityType(entidade)!
                .FindProperty(propriedade)!
                .GetMaxLength();

            doSchema.Should().Be(valorNoCodec,
                $"LimitesDoEnvelope.{nome} tem de ser o HasMaxLength de {entidade.Name}.{propriedade}. " +
                "Se a coluna encolheu, o decoder passou a aceitar o que o banco recusa — e o descarte volta a morrer " +
                "com DbUpdateException (500) em vez de recusa nomeada. Se a coluna cresceu, o decoder passou a " +
                "recusar configuração legítima.");
        }
    }

    [Fact(DisplayName = "Todo limite de PRECISÃO do decoder é o da coluna numeric(p,s)")]
    public void Precisoes_BatemComOSchema()
    {
        using SelecaoDbContext contexto = ContextoSoParaOModelo();

        foreach ((string nome, int precisaoNoCodec, int escalaNoCodec, Type entidade, string propriedade) in Precisoes)
        {
            IProperty coluna = contexto.Model.FindEntityType(entidade)!.FindProperty(propriedade)!;

            coluna.GetPrecision().Should().Be(precisaoNoCodec,
                $"LimitesDoEnvelope.{nome} tem de ser a precisão de {entidade.Name}.{propriedade} — sem ela, um " +
                "decimal com escala impecável mas dígitos demais recanonicaliza nos MESMOS bytes, a prova de " +
                "round-trip aprova, e o numeric(p,s) só estoura no SaveChanges (22003).");
            coluna.GetScale().Should().Be(escalaNoCodec);
        }
    }

    /// <summary>
    /// Os limites dos <b>owned types</b> — <c>ReferenciaRegra</c> (6 usos no envelope) e o
    /// snapshot da referência demográfica. Eles não têm entidade própria no modelo: são
    /// colunas do dono, e é por elas que se chega ao <c>HasMaxLength</c>/<c>HasPrecision</c>.
    /// </summary>
    [Fact(DisplayName = "Os limites dos owned types (ReferenciaRegra, referência demográfica) também são os do schema")]
    public void OwnedTypes_BatemComOSchema()
    {
        using SelecaoDbContext contexto = ContextoSoParaOModelo();

        IEntityType regra = contexto.Model
            .FindEntityType(typeof(ConfiguracaoBonusRegional))!
            .GetNavigations()
            .Single(n => n.Name == nameof(ConfiguracaoBonusRegional.Regra))
            .TargetEntityType;

        regra.FindProperty(nameof(ReferenciaRegra.Codigo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.RegraCodigo, "LimitesDoEnvelope.RegraCodigo espelha a coluna do código da regra");
        regra.FindProperty(nameof(ReferenciaRegra.Versao))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.RegraVersao, "LimitesDoEnvelope.RegraVersao espelha a coluna da versão da regra");

        IEntityType demografica = contexto.Model
            .FindEntityType(typeof(ConfiguracaoDistribuicaoVagas))!
            .GetNavigations()
            .Single(n => n.Name == nameof(ConfiguracaoDistribuicaoVagas.ReferenciaDemografica))
            .TargetEntityType;

        demografica.FindProperty(nameof(ReferenciaReservaDemograficaSnapshot.CensoReferencia))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.CensoReferencia);

        foreach (string percentual in new[]
        {
            nameof(ReferenciaReservaDemograficaSnapshot.PpiPercentual),
            nameof(ReferenciaReservaDemograficaSnapshot.QuilombolaPercentual),
            nameof(ReferenciaReservaDemograficaSnapshot.PcdPercentual),
        })
        {
            demografica.FindProperty(percentual)!.GetPrecision()
                .Should().Be(LimitesDoEnvelope.PrecisaoPercentual, $"LimitesDoEnvelope.PrecisaoPercentual espelha a coluna de {percentual}");
        }

        // Story #851 — ArgsRegraPrazoRecurso.AtoAncoraCodigo e as precisões do prazo/das
        // duas suspensividades.
        IEntityType argsPrazoRecurso = contexto.Model
            .FindEntityType(typeof(RegraRecursoFase))!
            .GetNavigations()
            .Single(n => n.Name == nameof(RegraRecursoFase.Args))
            .TargetEntityType;

        argsPrazoRecurso.FindProperty(nameof(ArgsRegraPrazoRecurso.AtoAncoraCodigo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.TipoAtoCodigo, "LimitesDoEnvelope.TipoAtoCodigo espelha a coluna do código do ato âncora");

        foreach (string campoPrazo in new[]
        {
            nameof(ArgsRegraPrazoRecurso.PrazoValor),
            nameof(ArgsRegraPrazoRecurso.SuspensividadePrimeiraInstanciaValor),
            nameof(ArgsRegraPrazoRecurso.SuspensividadeSegundaInstanciaValor),
        })
        {
            argsPrazoRecurso.FindProperty(campoPrazo)!.GetPrecision()
                .Should().Be(LimitesDoEnvelope.PrecisaoPrazo, $"LimitesDoEnvelope.PrecisaoPrazo espelha a coluna de {campoPrazo}");
        }
    }

    /// <summary>
    /// E o inverso: <b>toda</b> constante de <c>LimitesDoEnvelope</c> é exercida por algum
    /// caso acima. Uma constante que ninguém confronta com o schema é uma que pode divergir
    /// dele em silêncio — e o fitness passaria a proteger menos do que diz proteger.
    /// </summary>
    [Fact(DisplayName = "Nenhuma constante de LimitesDoEnvelope fica sem confronto com o schema")]
    public void TodaConstante_EConfrontada()
    {
        IEnumerable<string> declaradas = typeof(LimitesDoEnvelope)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.IsLiteral)
            .Select(static f => f.Name);

        HashSet<string> confrontadas =
        [
            .. Comprimentos.Select(static c => c.Nome),
            .. Precisoes.Select(static p => p.Nome),
            // Exercidas em OwnedTypes_BatemComOSchema.
            "RegraCodigo", "RegraVersao", "CensoReferencia", "PrecisaoPercentual", "PrecisaoPrazo",

            // NumeroDoAto não é coluna do agregado — os DadosEdital são do ato, não da
            // configuração. O limite vem dos validators de publicar e de retificar (60), e é
            // deles que o decoder tem de ser tão estrito quanto.
            "NumeroDoAto",
        ];

        declaradas.Except(confrontadas).Should().BeEmpty(
            "toda constante de LimitesDoEnvelope tem de ser confrontada com a coluna que ela espelha — senão ela " +
            "pode divergir do schema sem que nada acuse, e o decoder volta a aceitar o que o banco recusa");
    }

    /// <summary>
    /// O modelo do EF é construído em memória — nenhuma conexão é aberta. A connection string
    /// existe só porque o provider a exige para montar o modelo.
    /// </summary>
    private static SelecaoDbContext ContextoSoParaOModelo() => new(
        new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql("Host=localhost;Database=nao-conecta;Username=x;Password=x")
            .UseSnakeCaseNamingConvention()
            .Options);
}
