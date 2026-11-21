namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Os limites dos tipos embutidos (<c>ReferenciaRegra</c>, referência demográfica, grupo de área do ENEM, unidade
/// administradora e tipo da etapa) também são os das colunas. Eles não têm entidade própria no modelo: são colunas do dono, e é
/// por elas que se chega ao <c>HasMaxLength</c> e ao <c>HasPrecision</c>.
/// </summary>
public sealed class LimitesDosTiposEmbutidosBatemComOSchemaTests
{
    /// <summary>
    /// Um SHA-256 em hexadecimal minúsculo tem 64 caracteres: é o que o value object valida ao construir a referência, e é o que
    /// a coluna tem de guardar.
    /// </summary>
    private const int HashSha256Length = 64;

    private static readonly string[] PercentuaisDaReferenciaDemografica =
    [
        nameof(ReferenciaReservaDemograficaSnapshot.PpiPercentual),
        nameof(ReferenciaReservaDemograficaSnapshot.QuilombolaPercentual),
        nameof(ReferenciaReservaDemograficaSnapshot.PcdPercentual),
    ];

    /// <summary>
    /// Toda referência de regra embutida no modelo, descoberta e não listada, tem as larguras que o decodificador do envelope
    /// pressupõe.
    /// </summary>
    /// <remarks>
    /// As navegações são descobertas pelo modelo, não nomeadas dono por dono: a referência adicionada a qualquer dono depois
    /// já entra na conferência. A configuração compartilhada torna a divergência inexprimível para quem usa a extensão; este
    /// teste cobre quem configurar a navegação à mão.
    /// </remarks>
    [Fact(DisplayName = "Toda ReferenciaRegra embutida no modelo tem as larguras do decodificador, descobertas e não listadas")]
    public void Toda_ReferenciaRegra_Embutida_TemAsLargurasDoDecodificador()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();

        (string Dono, string Navegacao, IEntityType Alvo)[] referencias = ReferenciasDeRegraEmbutidas(contexto);

        referencias.Should().NotBeEmpty(
            "se a descoberta parar de achar navegações, o teste passa a aprovar por vacuidade, e é justamente esse silêncio que ele existe para não ter");

        foreach ((string dono, string navegacao, IEntityType alvo) in referencias)
        {
            ConferirAReferenciaDeRegra(dono, navegacao, alvo);
        }
    }

    [Fact(DisplayName = "Os limites dos tipos embutidos também são os do schema")]
    public void TiposEmbutidos_BatemComOSchema()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();

        ConferirAReferenciaDemografica(contexto);
        ConferirOGrupoDeAreaEnem(contexto);
        ConferirAUnidadeAdministradora(contexto);
        ConferirOTipoDaEtapa(contexto);
    }

    private static (string Dono, string Navegacao, IEntityType Alvo)[] ReferenciasDeRegraEmbutidas(UniPlusDbContext contexto) => contexto.Model
        .GetEntityTypes()
        .SelectMany(e => e.GetNavigations()
            .Where(n => n.TargetEntityType.ClrType == typeof(ReferenciaRegra))
            .Select(n => (Dono: e.ClrType.Name, Navegacao: n.Name, Alvo: n.TargetEntityType)))
        .ToArray();

    private static void ConferirAReferenciaDeRegra(string dono, string navegacao, IEntityType alvo)
    {
        alvo.FindProperty(nameof(ReferenciaRegra.Codigo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.RegraCodigo,
                $"o código da regra em {dono}.{navegacao} tem de caber o que o decodificador aceita");

        alvo.FindProperty(nameof(ReferenciaRegra.Versao))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.RegraVersao,
                $"a versão da regra em {dono}.{navegacao} tem de caber o que o decodificador aceita");

        IProperty hash = alvo.FindProperty(nameof(ReferenciaRegra.Hash))!;
        hash.GetMaxLength().Should().Be(HashSha256Length,
            $"o hash em {dono}.{navegacao} é um SHA-256 hexadecimal minúsculo");
        hash.IsFixedLength().Should().BeTrue(
            $"o hash em {dono}.{navegacao} tem sempre 64 caracteres; coluna de comprimento variável aceitaria um hash truncado, "
            + "que é o que prova que a definição da regra não mudou");
    }

    private static void ConferirAReferenciaDemografica(UniPlusDbContext contexto)
    {
        IEntityType demografica = ModeloEmMemoria.EmbutidoDe<ConfiguracaoDistribuicaoVagas>(contexto, nameof(ConfiguracaoDistribuicaoVagas.ReferenciaDemografica));

        demografica.FindProperty(nameof(ReferenciaReservaDemograficaSnapshot.CensoReferencia))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.CensoReferencia);

        foreach (string percentual in PercentuaisDaReferenciaDemografica)
        {
            demografica.FindProperty(percentual)!.GetPrecision()
                .Should().Be(LimitesDoEnvelope.PrecisaoPercentual, $"LimitesDoEnvelope.PrecisaoPercentual espelha a coluna de {percentual}");
        }
    }

    private static void ConferirOGrupoDeAreaEnem(UniPlusDbContext contexto)
    {
        IEntityType grupoDaOferta = ModeloEmMemoria.EmbutidoDe<ConfiguracaoDistribuicaoVagas>(contexto, nameof(ConfiguracaoDistribuicaoVagas.GrupoAreaEnem));
        // O mesmo grupo, no quadro de pesos por área congelado na classificação.
        IEntityType grupoDoQuadro = ModeloEmMemoria.EmbutidoDe<GrupoPesoAreaEnemCongelado>(contexto, nameof(GrupoPesoAreaEnemCongelado.GrupoAreaEnem));

        foreach (IEntityType grupo in new[] { grupoDaOferta, grupoDoQuadro })
        {
            grupo.FindProperty(nameof(GrupoAreaEnemSnapshot.Codigo))!.GetMaxLength().Should().Be(LimitesDoEnvelope.GrupoAreaEnemCodigo);
            grupo.FindProperty(nameof(GrupoAreaEnemSnapshot.Rotulo))!.GetMaxLength().Should().Be(LimitesDoEnvelope.GrupoAreaEnemRotulo);
        }
    }

    // Unidade administradora (identidadesUnidade) e cidade, esta opcional e de tudo ou nada.
    private static void ConferirAUnidadeAdministradora(UniPlusDbContext contexto)
    {
        IEntityType unidade = ModeloEmMemoria.EmbutidoDe<ProcessoSeletivo>(contexto, nameof(ProcessoSeletivo.UnidadeAdministradora));

        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.Sigla), LimitesDoEnvelope.UnidadeAdministradoraSigla, "UnidadeAdministradoraSigla");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.Codigo), LimitesDoEnvelope.UnidadeAdministradoraCodigo, "UnidadeAdministradoraCodigo");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.Slug), LimitesDoEnvelope.UnidadeAdministradoraSlug, "UnidadeAdministradoraSlug");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.Nome), LimitesDoEnvelope.UnidadeAdministradoraNome, "UnidadeAdministradoraNome");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.Tipo), LimitesDoEnvelope.UnidadeAdministradoraTipo, "UnidadeAdministradoraTipo");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.CidadeCodigoIbge), LimitesDoEnvelope.UnidadeAdministradoraCidadeCodigoIbge, "UnidadeAdministradoraCidadeCodigoIbge");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.CidadeNome), LimitesDoEnvelope.UnidadeAdministradoraCidadeNome, "UnidadeAdministradoraCidadeNome");
        ConferirOLimiteDaColuna(unidade, nameof(UnidadeAdministradoraSnapshot.CidadeUf), LimitesDoEnvelope.UnidadeAdministradoraCidadeUf, "UnidadeAdministradoraCidadeUf");
    }

    // O tipo da etapa aninhado em cada item de etapas é persistido ao restaurar uma versão, por isso precisa da mesma disciplina de
    // limite (diferente do bloco de topo tipoProcesso, só de forma).
    private static void ConferirOTipoDaEtapa(UniPlusDbContext contexto)
    {
        IEntityType tipoEtapa = ModeloEmMemoria.EmbutidoDe<EtapaProcesso>(contexto, nameof(EtapaProcesso.TipoEtapa));

        tipoEtapa.FindProperty(nameof(TipoEtapaSnapshot.Codigo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.TipoEtapaCodigo, "LimitesDoEnvelope.TipoEtapaCodigo espelha a coluna tipo_etapa_codigo");
        tipoEtapa.FindProperty(nameof(TipoEtapaSnapshot.Nome))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.TipoEtapaNome, "LimitesDoEnvelope.TipoEtapaNome espelha a coluna tipo_etapa_nome");
    }

    private static void ConferirOLimiteDaColuna(IEntityType entidade, string propriedade, int limite, string constante) =>
        entidade.FindProperty(propriedade)!.GetMaxLength()
            .Should().Be(limite, $"LimitesDoEnvelope.{constante} espelha a coluna de {propriedade}");
}
