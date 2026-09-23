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
        ("TipoDeficienciaCodigo", LimitesDoEnvelope.TipoDeficienciaCodigo, typeof(OfertaTipoDeficiencia), nameof(OfertaTipoDeficiencia.TipoDeficienciaCodigo)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(OfertaCondicao), nameof(OfertaCondicao.CondicaoNome)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(OfertaRecurso), nameof(OfertaRecurso.RecursoNome)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(OfertaTipoDeficiencia), nameof(OfertaTipoDeficiencia.TipoDeficienciaNome)),
        // Story #1466 — bônus regional referencia Base Legal tipada (snapshot congelado).
        ("TipoInstrumentoNormativo", LimitesDoEnvelope.TipoInstrumentoNormativo, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.TipoInstrumento)),
        ("IdentificacaoBaseLegalBonusRegional", LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Identificacao)),
        ("DescricaoBaseLegalBonusRegional", LimitesDoEnvelope.DescricaoBaseLegalBonusRegional, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Descricao)),
        ("MunicipioBonusRegionalCodigoIbge", LimitesDoEnvelope.MunicipioBonusRegionalCodigoIbge, typeof(ConfiguracaoBonusRegionalMunicipio), nameof(ConfiguracaoBonusRegionalMunicipio.CodigoIbge)),
        ("MunicipioBonusRegionalNome", LimitesDoEnvelope.MunicipioBonusRegionalNome, typeof(ConfiguracaoBonusRegionalMunicipio), nameof(ConfiguracaoBonusRegionalMunicipio.Nome)),
        ("MunicipioBonusRegionalUf", LimitesDoEnvelope.MunicipioBonusRegionalUf, typeof(ConfiguracaoBonusRegionalMunicipio), nameof(ConfiguracaoBonusRegionalMunicipio.Uf)),

        // Story #851 — cronograma de fases.
        ("FaseCodigo", LimitesDoEnvelope.FaseCodigo, typeof(FaseCronograma), nameof(FaseCronograma.Codigo)),
        ("DonoInstitucional", LimitesDoEnvelope.DonoInstitucional, typeof(FaseCronograma), nameof(FaseCronograma.DonoInstitucional)),
        ("FaseCodigo", LimitesDoEnvelope.FaseCodigo, typeof(FaseCronograma), nameof(FaseCronograma.FaseConcluinteCodigo)),
        ("TipoAtoCodigo", LimitesDoEnvelope.TipoAtoCodigo, typeof(ProdutoDaFase), nameof(ProdutoDaFase.AtoCodigo)),
        ("TipoBancaCodigo", LimitesDoEnvelope.TipoBancaCodigo, typeof(BancaRequerida), nameof(BancaRequerida.Codigo)),
        ("CategoriaDocumentoCodigo", LimitesDoEnvelope.CategoriaDocumentoCodigo, typeof(CategoriaJulgada), nameof(CategoriaJulgada.Codigo)),

        // As mesmas grandezas, um nível abaixo: a etapa repete fase, ato e banca, e as colunas
        // dela têm a mesma largura. Sem estas linhas a tabela cobria só o lado da fase, e
        // estreitar uma coluna da etapa não acusaria nada.
        //
        // O que estas linhas NÃO alcançam, e convém não confiar que alcancem: o decodificador
        // media faseCodigo contra o teto do NOME da etapa, e isso passou porque aquele teto
        // batia com a coluna do nome — a tabela confere constante contra coluna, nunca que o
        // call site pegou a constante da coluna que vai receber o valor. Quem prende esse
        // segundo erro é o teste de recusa por campo, em EnvelopeCodecRecusaTests.
        ("FaseCodigo", LimitesDoEnvelope.FaseCodigo, typeof(EtapaProcesso), nameof(EtapaProcesso.FaseCodigo)),
        ("TipoAtoCodigo", LimitesDoEnvelope.TipoAtoCodigo, typeof(ProdutoDaEtapa), nameof(ProdutoDaEtapa.AtoCodigo)),
        ("TipoBancaCodigo", LimitesDoEnvelope.TipoBancaCodigo, typeof(BancaDaEtapa), nameof(BancaDaEtapa.Codigo)),

        // Story #554 (PR #903) — exigencias[] real.
        ("TipoDocumentoCodigo", LimitesDoEnvelope.TipoDocumentoCodigo, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCodigo)),
        ("TipoDocumentoNome", LimitesDoEnvelope.TipoDocumentoNome, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoNome)),
        ("TipoDocumentoCategoria", LimitesDoEnvelope.TipoDocumentoCategoria, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCategoria)),
        ("Token", LimitesDoEnvelope.Token, typeof(DocumentoExigido), nameof(DocumentoExigido.ConsequenciaIndeferimento)),
        ("BaseLegal", LimitesDoEnvelope.BaseLegal, typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Referencia)),
        ("ObservacaoBaseLegal", LimitesDoEnvelope.ObservacaoBaseLegal, typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Observacao)),
        ("Fato", LimitesDoEnvelope.Fato, typeof(CondicaoGatilho), nameof(CondicaoGatilho.Fato)),

        // Story #575 — cascata de remanejamento. Códigos de modalidade — mesmo limite de
        // ModalidadeSelecionada.Codigo, reusado porque são a mesma grandeza.
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(ConfiguracaoCascataRemanejamento), nameof(ConfiguracaoCascataRemanejamento.FallbackCodigo)),
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(DestinoRemanejamento), nameof(DestinoRemanejamento.ModalidadeOrigemCodigo)),
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(DestinoRemanejamento), nameof(DestinoRemanejamento.ModalidadeDestinoCodigo)),

        // Story #559 — formulário de inscrição. Rotulo/FormularioTitulo reusam
        // NomeDeCadastro (mesma grandeza: rótulo curto legível).
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(FatoColetado), nameof(FatoColetado.Rotulo)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(ProcessoSeletivo), nameof(ProcessoSeletivo.FormularioTitulo)),
        ("TermoDeAceite", LimitesDoEnvelope.TermoDeAceite, typeof(ProcessoSeletivo), nameof(ProcessoSeletivo.FormularioTermoAceiteTexto)),

        // Issue #563 — divulgação pública.
        ("Justificativa", LimitesDoEnvelope.Justificativa, typeof(ConfiguracaoDivulgacao), nameof(ConfiguracaoDivulgacao.Justificativa)),
    ];

    /// <summary>
    /// A escala com que o decodificador lê todo prazo de recurso (o <c>EscalaPadrao</c> do
    /// codec). A precisão tem constante própria em <c>LimitesDoEnvelope</c>; a escala não —
    /// e sem confrontá-la uma coluna <c>numeric(18,2)</c> passaria batido, arredondando o
    /// prazo que o edital prometeu.
    /// </summary>
    private const int EscalaDoPrazo = 4;

    private static readonly (string Nome, int PrecisaoNoCodec, int EscalaNoCodec, Type Entidade, string Propriedade)[] Precisoes =
    [
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(EtapaProcesso), nameof(EtapaProcesso.Peso)),
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(EtapaProcesso), nameof(EtapaProcesso.NotaMinima)),
        ("PrecisaoBonus", LimitesDoEnvelope.PrecisaoBonus, 4, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Fator)),
        ("PrecisaoBonus", LimitesDoEnvelope.PrecisaoBonus, 4, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Teto)),
        ("PrecisaoPr", LimitesDoEnvelope.PrecisaoPr, 4, typeof(ConfiguracaoDistribuicaoVagas), nameof(ConfiguracaoDistribuicaoVagas.Pr)),
        ("PrecisaoTaxaInscricao", LimitesDoEnvelope.PrecisaoTaxaInscricao, ConfiguracaoTaxaInscricao.ValorEscala, typeof(ConfiguracaoTaxaInscricao), nameof(ConfiguracaoTaxaInscricao.Valor)),
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
    /// Um SHA-256 em hexadecimal minúsculo tem 64 caracteres — é o que o value object valida
    /// ao construir a referência, e é o que a coluna tem de guardar.
    /// </summary>
    private const int HashSha256Length = 64;

    /// <summary>
    /// Toda referência de regra embutida no modelo — <b>descobertas</b>, não listadas — tem as
    /// larguras que o decodificador do envelope pressupõe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A lista à mão era o problema. Quando esta conferência nomeava dono por dono, quatro dos
    /// nove ficavam de fora sem que nada dissesse isso, e foi assim que uma divergência real
    /// passou: um dono ficou com larguras próprias e nenhuma linha o confrontava. Descobrir as
    /// navegações pelo modelo faz o dono novo nascer coberto, em vez de depender de alguém
    /// lembrar de acrescentá-lo aqui.
    /// </para>
    /// <para>
    /// A configuração compartilhada já torna a divergência inexprimível para quem usa a
    /// extensão; este gate é a rede para quem não usar — configurar a navegação à mão continua
    /// sendo possível, e é exatamente o caminho que produziria o defeito de novo.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "Toda ReferenciaRegra embutida no modelo tem as larguras do decodificador — descobertas, não listadas")]
    public void Toda_ReferenciaRegra_Embutida_TemAsLargurasDoDecodificador()
    {
        using SelecaoDbContext contexto = ContextoSoParaOModelo();

        (string Dono, string Navegacao, IEntityType Alvo)[] referencias = contexto.Model
            .GetEntityTypes()
            .SelectMany(e => e.GetNavigations()
                .Where(n => n.TargetEntityType.ClrType == typeof(ReferenciaRegra))
                .Select(n => (Dono: e.ClrType.Name, Navegacao: n.Name, Alvo: n.TargetEntityType)))
            .ToArray();

        referencias.Should().NotBeEmpty(
            "se a descoberta parar de achar navegações, o gate passa a aprovar por vacuidade — e é "
            + "justamente esse silêncio que ele existe para não ter");

        foreach ((string dono, string navegacao, IEntityType alvo) in referencias)
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
                $"o hash em {dono}.{navegacao} tem sempre 64 caracteres — coluna de comprimento variável "
                + "aceitaria um hash truncado, que é o que prova que a definição da regra não mudou");
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

        IEntityType demografica = contexto.Model
            .FindEntityType(typeof(ConfiguracaoDistribuicaoVagas))!
            .GetNavigations()
            .Single(n => n.Name == nameof(ConfiguracaoDistribuicaoVagas.ReferenciaDemografica))
            .TargetEntityType;

        demografica.FindProperty(nameof(ReferenciaReservaDemograficaSnapshot.CensoReferencia))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.CensoReferencia);

        IEntityType grupoAreaEnem = contexto.Model
            .FindEntityType(typeof(ConfiguracaoDistribuicaoVagas))!
            .GetNavigations()
            .Single(n => n.Name == nameof(ConfiguracaoDistribuicaoVagas.GrupoAreaEnem))
            .TargetEntityType;

        grupoAreaEnem.FindProperty(nameof(GrupoAreaEnemSnapshot.Codigo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.GrupoAreaEnemCodigo);
        grupoAreaEnem.FindProperty(nameof(GrupoAreaEnemSnapshot.Rotulo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.GrupoAreaEnemRotulo);

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

        // Story #851 — as precisões do prazo de interposição e das duas suspensividades.
        IEntityType argsPrazoRecurso = contexto.Model
            .FindEntityType(typeof(RegraRecursoFase))!
            .GetNavigations()
            .Single(n => n.Name == nameof(RegraRecursoFase.Args))
            .TargetEntityType;

        foreach (string campoPrazo in new[]
        {
            nameof(ArgsRegraPrazoRecurso.PrazoValor),
            nameof(ArgsRegraPrazoRecurso.SuspensividadePrimeiraInstanciaValor),
            nameof(ArgsRegraPrazoRecurso.SuspensividadeSegundaInstanciaValor),
        })
        {
            IProperty coluna = argsPrazoRecurso.FindProperty(campoPrazo)!;
            coluna.GetPrecision()
                .Should().Be(LimitesDoEnvelope.PrecisaoPrazo, $"LimitesDoEnvelope.PrecisaoPrazo espelha a coluna de {campoPrazo}");
            coluna.GetScale().Should().Be(EscalaDoPrazo);
        }

        // Os prazos da janela recursal da ETAPA precisam das mesmas precisões que os da fase: o
        // decodificador do envelope usa uma constante só para os dois lados, então uma coluna
        // mais estreita aqui produz envelope que ele aprova e o INSERT recusa.
        IEntityType recursoDaEtapa = contexto.Model.FindEntityType(typeof(RecursoDaEtapa))!;

        IEntityType argsDoRecursoDaEtapa = recursoDaEtapa
            .GetNavigations()
            .Single(n => n.Name == nameof(RecursoDaEtapa.Args))
            .TargetEntityType;

        foreach (string campoPrazo in new[]
        {
            nameof(ArgsRegraPrazoRecurso.PrazoValor),
            nameof(ArgsRegraPrazoRecurso.SuspensividadePrimeiraInstanciaValor),
            nameof(ArgsRegraPrazoRecurso.SuspensividadeSegundaInstanciaValor),
        })
        {
            IProperty coluna = argsDoRecursoDaEtapa.FindProperty(campoPrazo)!;
            coluna.GetPrecision()
                .Should().Be(LimitesDoEnvelope.PrecisaoPrazo, $"LimitesDoEnvelope.PrecisaoPrazo espelha a coluna de {campoPrazo} em recursos_da_etapa");
            coluna.GetScale().Should().Be(EscalaDoPrazo,
                $"a escala de {campoPrazo} é o que o decodificador aceita; uma coluna com menos casas ARREDONDA o prazo " +
                "em silêncio, e a prova de round-trip não enxerga arredondamento — só o valor restaurado sai diferente do congelado");
        }

        // Issue #849 — Unidade administradora (identidadesUnidade).
        IEntityType unidadeAdministradora = contexto.Model
            .FindEntityType(typeof(ProcessoSeletivo))!
            .GetNavigations()
            .Single(n => n.Name == nameof(ProcessoSeletivo.UnidadeAdministradora))
            .TargetEntityType;

        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.Sigla))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraSigla, "LimitesDoEnvelope.UnidadeAdministradoraSigla espelha a coluna de Sigla");
        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.Slug))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraSlug, "LimitesDoEnvelope.UnidadeAdministradoraSlug espelha a coluna de Slug");
        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.Nome))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraNome, "LimitesDoEnvelope.UnidadeAdministradoraNome espelha a coluna de Nome");
        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.Tipo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraTipo, "LimitesDoEnvelope.UnidadeAdministradoraTipo espelha a coluna de Tipo");

        // Issue #1114 — cidade da Unidade administradora (opcional all-or-nothing).
        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.CidadeCodigoIbge))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraCidadeCodigoIbge, "LimitesDoEnvelope.UnidadeAdministradoraCidadeCodigoIbge espelha a coluna de CidadeCodigoIbge");
        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.CidadeNome))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraCidadeNome, "LimitesDoEnvelope.UnidadeAdministradoraCidadeNome espelha a coluna de CidadeNome");
        unidadeAdministradora.FindProperty(nameof(UnidadeAdministradoraSnapshot.CidadeUf))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.UnidadeAdministradoraCidadeUf, "LimitesDoEnvelope.UnidadeAdministradoraCidadeUf espelha a coluna de CidadeUf");

        // Issue #1071 — snapshot de tipo de etapa (tipoEtapa aninhado em cada item de etapas).
        // Diferente do bloco de topo tipoProcesso (só forma, nunca reidratado), este valor É
        // persistido ao restaurar uma versão — por isso precisa da mesma disciplina de limite.
        IEntityType tipoEtapa = contexto.Model
            .FindEntityType(typeof(EtapaProcesso))!
            .GetNavigations()
            .Single(n => n.Name == nameof(EtapaProcesso.TipoEtapa))
            .TargetEntityType;

        tipoEtapa.FindProperty(nameof(TipoEtapaSnapshot.Codigo))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.TipoEtapaCodigo, "LimitesDoEnvelope.TipoEtapaCodigo espelha a coluna tipo_etapa_codigo");
        tipoEtapa.FindProperty(nameof(TipoEtapaSnapshot.Nome))!.GetMaxLength()
            .Should().Be(LimitesDoEnvelope.TipoEtapaNome, "LimitesDoEnvelope.TipoEtapaNome espelha a coluna tipo_etapa_nome");
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
            "UnidadeAdministradoraSigla", "UnidadeAdministradoraSlug", "UnidadeAdministradoraNome", "UnidadeAdministradoraTipo",
            "UnidadeAdministradoraCidadeCodigoIbge", "UnidadeAdministradoraCidadeNome", "UnidadeAdministradoraCidadeUf",
            "TipoEtapaCodigo", "TipoEtapaNome",
            "GrupoAreaEnemCodigo", "GrupoAreaEnemRotulo",

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
