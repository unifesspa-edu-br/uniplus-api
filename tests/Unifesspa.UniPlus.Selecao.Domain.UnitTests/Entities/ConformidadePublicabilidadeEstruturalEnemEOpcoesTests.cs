namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoEnemComCalculoLocal;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

public sealed class ConformidadePublicabilidadeEstruturalEnemEOpcoesTests
{
    private static void RetirarDaResolucao(ProcessoSeletivo processo, FaltaNaResolucao falta)
    {
        PropertyInfo resolucao = typeof(ConfiguracaoClassificacao).GetProperty(nameof(ConfiguracaoClassificacao.ResolucaoPesoAreaEnem))!;
        if (falta == FaltaNaResolucao.QuadroVazio)
        {
            EsvaziarQuadro(processo.Classificacao!);
            return;
        }

        resolucao.SetValue(processo.Classificacao, falta == FaltaNaResolucao.ResolucaoAusente ? null : "   ");
    }

    private static Result DefinirODesempatePorAreaEnem(ProcessoSeletivo processo) => processo.DefinirCriteriosDesempate(
        [CriterioDesempate.Criar(1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo(), 'e'), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!]);

    private static ConfiguracaoClassificacao ClassificacaoLocalSemEnem() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), 'a'),
        RegraArredondamento = Regra(RegraDeArredondamentoDaNota.Truncar.Codigo(), 'b'),
        CasasArredondamento = 2,
        RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
        EscalaNotaFinal = 100m,
    }).Value!;

    private static ConfiguracaoClassificacao ClassificacaoImportadaBaseadaEmEnem() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), 'b'),
        RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
        BaseadoEmEnem = true,
    }).Value!;

    private static void AfirmarSemItensDaMediaPorAreaEPublicacaoAceita(ProcessoSeletivo processo)
    {
        Checklist(processo).Select(static i => i.Codigo).Should().NotContain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro"]);
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "ENEM com cálculo local: resolução ausente ou em branco, ou quadro vazio, estado que só chega sem passar pela fábrica — só o item da resolução fica vermelho, e Publicar recusa")]
    [InlineData(FaltaNaResolucao.ResolucaoAusente)]
    [InlineData(FaltaNaResolucao.ResolucaoEmBranco)]
    [InlineData(FaltaNaResolucao.QuadroVazio)]
    public void Enem_ResolucaoOuQuadroAusente(FaltaNaResolucao falta)
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        RetirarDaResolucao(processo, falta);

        SoEstesItensVermelhos(processo, "classificacao_resolucao_peso_area_enem_ausente");
        Item(processo, "classificacao_resolucao_peso_area_enem_ausente").Dimensao.Should().Be(DimensaoConformidade.Classificacao);
        Checklist(processo).Any(static i => i.Codigo == "desempate_area_enem_sem_quadro")
            .Should().Be(falta != FaltaNaResolucao.QuadroVazio, "o item do desempate sem quadro só sai do checklist quando é o quadro que falta");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain("Resolução de Pesos por Área");
    }

    [Fact(DisplayName = "Oferta sem nome congelado — só o item do nome fica vermelho, nomeando a oferta, e Publicar recusa")]
    public void OfertaSemNomeCongelado()
    {
        ConfiguracaoDistribuicaoVagas nomeada = OfertaAmpla(10, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas semNome = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 5, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), 'a'), Modalidades = [Modalidade("AC", NaturezaLegalModalidade.Ampla, null, 5, null)] }).Value!;
        ProcessoSeletivo processo = ProcessoConforme([nomeada, semNome]);

        SoEstesItensVermelhos(processo, "distribuicao_vagas_oferta_sem_nome");
        Item(processo, "distribuicao_vagas_oferta_sem_nome").Mensagem.Should()
            .Contain(semNome.OfertaCursoOrigemId.ToString())
            .And.NotContain(nomeada.OfertaCursoOrigemId.ToString());

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName = "ENEM com cálculo local: ofertas sem grupo de área — só o item do grupo fica vermelho, nomeando as ofertas, e Publicar recusa")]
    public void Enem_OfertaSemGrupo()
    {
        ConfiguracaoDistribuicaoVagas comGrupo = OfertaAmpla(10, GrupoTecnologica);
        ConfiguracaoDistribuicaoVagas semGrupo = OfertaAmpla(5, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas outraSemGrupo = OfertaAmpla(3, grupoAreaEnem: null);
        ProcessoSeletivo processo = ProcessoEnemLocal([comGrupo, semGrupo, outraSemGrupo]);

        ConferirItemVermelhoQueNomeia(
            processo, "distribuicao_vagas_oferta_sem_grupo_area_enem", DimensaoConformidade.DistribuicaoVagas,
            nomeia: [semGrupo.OfertaCursoOrigemId.ToString(), outraSemGrupo.OfertaCursoOrigemId.ToString()], naoNomeia: [comGrupo.OfertaCursoOrigemId.ToString()]);
        ConferirQuePublicarRecusaNomeando(processo, outraSemGrupo.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: a recusa de Publicar traz uma causa por item, com a pontuação da mensagem do painel")]
    public void Enem_CausaPorItemBemPontuada()
    {
        ConfiguracaoDistribuicaoVagas[] semGrupo = [.. new[] { OfertaAmpla(5, grupoAreaEnem: null), OfertaAmpla(4, grupoAreaEnem: null) }
            .OrderBy(static o => o.OfertaCursoOrigemId)];
        string umItem = Publicar(ProcessoEnemLocal(semGrupo)).Errors.Should().ContainSingle().Subject.Error.Message;
        DeveEstarBemPontuada(umItem);
        umItem.Should().Contain($"(ofertas: {semGrupo[0].OfertaCursoOrigemId}; {semGrupo[1].OfertaCursoOrigemId})",
            "os Ids de um mesmo item se separam por ponto e vírgula");

        ConfiguracaoDistribuicaoVagas outraSemGrupo = OfertaAmpla(5, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas foraDoQuadro = OfertaAmpla(10, GrupoTecnologica);
        string[] doisItens = [.. Publicar(ProcessoEnemLocal(
            [outraSemGrupo, foraDoQuadro], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")])).Errors.Select(static causa => causa.Error.Message)];
        doisItens.Should().HaveCount(2).And.AllSatisfy(DeveEstarBemPontuada);
        doisItens.Should().Contain(mensagem => mensagem.Contains(outraSemGrupo.OfertaCursoOrigemId.ToString(), StringComparison.Ordinal))
            .And.Contain(mensagem => mensagem.Contains($"{foraDoQuadro.OfertaCursoOrigemId} (grupo TECNOLOGICA — Tecnológica)", StringComparison.Ordinal));
    }

    /// <summary>A redação pode mudar; a pontuação da mensagem de cada item, não.</summary>
    private static void DeveEstarBemPontuada(string mensagem)
    {
        mensagem.Should().NotContain("..").And.NotContain("., ").And.NotContain(";)").And.EndWith(").");
    }

    [Fact(DisplayName = "ENEM com cálculo local: as ofertas pendentes aparecem na mensagem em ordem estável, qualquer que seja a ordem da distribuição")]
    public void Enem_OfertasEmOrdemEstavel()
    {
        ConfiguracaoDistribuicaoVagas[] ofertas = [OfertaAmpla(5, grupoAreaEnem: null), OfertaAmpla(3, grupoAreaEnem: null)];
        ConfiguracaoDistribuicaoVagas[] emOrdem = [.. ofertas.OrderBy(static o => o.OfertaCursoOrigemId)];
        ProcessoSeletivo processo = ProcessoEnemLocal([emOrdem[1], emOrdem[0]]);

        string mensagem = Item(processo, "distribuicao_vagas_oferta_sem_grupo_area_enem").Mensagem;
        mensagem.IndexOf(emOrdem[0].OfertaCursoOrigemId.ToString(), StringComparison.Ordinal).Should()
            .BeLessThan(mensagem.IndexOf(emOrdem[1].OfertaCursoOrigemId.ToString(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ENEM com cálculo local: oferta com grupo fora do quadro — só o item do quadro fica vermelho, na classificação, nomeando a oferta e o grupo por código e rótulo, e Publicar recusa")]
    public void Enem_GrupoForaDoQuadro()
    {
        ConfiguracaoDistribuicaoVagas noQuadro = OfertaAmpla(5, GrupoHumanisticaI);
        ConfiguracaoDistribuicaoVagas foraDoQuadro = OfertaAmpla(10, GrupoTecnologica);
        ProcessoSeletivo processo = ProcessoEnemLocal(
            [noQuadro, foraDoQuadro], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")]);

        ConferirItemVermelhoQueNomeia(
            processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro", DimensaoConformidade.Classificacao,
            nomeia: [$"{foraDoQuadro.OfertaCursoOrigemId} (grupo TECNOLOGICA — Tecnológica)"], naoNomeia: [noQuadro.OfertaCursoOrigemId.ToString()]);
        ConferirQuePublicarRecusaNomeando(processo, foraDoQuadro.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: classificação sem quadro gravada sobre desempate por área recusa no campo da resolução, nunca em silêncio")]
    public void Enem_DefinirClassificacaoSemQuadroComDesempatePorArea_RecusaNaResolucao()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo(), 'e'), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!]).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao semQuadro = ClassificacaoEnemLocal(QuadroPesoAreaEnemDeTeste.Completo());
        EsvaziarQuadro(semQuadro);

        Result resultado = processo.DefinirClassificacao(semQuadro);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            (ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, "ProcessoSeletivo.DesempateAreaEnemSemQuadro"));
    }

    [Fact(DisplayName = "ENEM com cálculo local: o rótulo do grupo volta na mensagem limitado ao teto de eco")]
    public void Enem_GrupoForaDoQuadro_RotuloLimitadoAoTetoDeEco()
    {
        string rotuloLongo = new('x', GrupoAreaEnemSnapshot.RotuloMaxLength);
        ProcessoSeletivo processo = ProcessoEnemLocal(
            [OfertaAmpla(10, ("OUTRO", rotuloLongo))], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")]);

        Item(processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro").Mensagem.Should()
            .Contain($"(grupo OUTRO — {new string('x', 40)}…)",
                "o rótulo volta cortado no teto de eco das mensagens de recusa, de 40 caracteres");
    }

    [Fact(DisplayName = "ENEM com cálculo local: sem quadro, nem a oferta com grupo nem o desempate por área abrem um segundo vermelho pela mesma causa")]
    public void Enem_SemQuadroNaoDuplicaPendencia()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(5, GrupoHumanisticaI)]);
        DefinirODesempatePorAreaEnem(processo).IsSuccess.Should().BeTrue();
        EsvaziarQuadro(processo.Classificacao!);

        SoEstesItensVermelhos(processo, "classificacao_resolucao_peso_area_enem_ausente");
        Checklist(processo).Select(static i => i.Codigo).Should().NotContain(
            "desempate_area_enem_sem_quadro", "sairia verde com a descrição de que há quadro, justamente quando ele falta");
        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo).Should().BeNull(
            "a conferência diz o mesmo que o checklist: a falta do quadro é só pendência da resolução");

        // A deduplicação é da conformidade: a gravação do desempate continua recusando.
        Result gravar = DefinirODesempatePorAreaEnem(processo);
        gravar.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");
        ExigirPublicacaoRecusada(processo, "ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName = "Contraprova: cálculo local que não é baseado em ENEM não traz os itens da média pelos pesos por área, dispensa grupo de área na oferta, e Publicar conclui")]
    public void Enem_CalculoLocalSemEnemDispensaGrupo()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas([OfertaAmpla(10, grupoAreaEnem: null)], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoLocalSemEnem()).IsSuccess.Should().BeTrue();

        AfirmarSemItensDaMediaPorAreaEPublicacaoAceita(processo);
    }

    [Fact(DisplayName = "Contraprova: classificação baseada em ENEM importada não traz os itens da média local, dispensa grupo de área na oferta, e Publicar conclui")]
    public void Enem_ClassificacaoImportadaDispensaGrupo()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, grupoAreaEnem: null)]);
        processo.DefinirClassificacao(ClassificacaoImportadaBaseadaEmEnem()).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!])
            .IsSuccess.Should().BeTrue();

        AfirmarSemItensDaMediaPorAreaEPublicacaoAceita(processo);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Opções declaradas pelo processo
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Fato coletado de fonte do processo sem opção declarada: item vermelho e publicação recusada; com opção, liberado")]
    public void FatoDeFonteDoProcessoSemOpcao_RecusaAtePrimeiraOpcao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensNaFaseDaImportacao([FatoDeOpcaoDeTeste()]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")])
            .IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo).Should().BeNull();
        processo.OpcoesDoProcesso("FATO_OPCAO_DE_TESTE").Should().ContainSingle().Which.Codigo.Should().Be("2025");
    }

    [Theory(DisplayName = "Definir opções declaradas recusa fato do atendimento especializado, lista vazia e código repetido")]
    [InlineData("CONDICAO_ATENDIMENTO", 1, "OpcaoDeclaradaFato.GeridasPeloAtendimentoEspecializado")]
    [InlineData("FATO_OPCAO_DE_TESTE", 0, "OpcaoDeclaradaFato.ListaVazia")]
    [InlineData("FATO_OPCAO_DE_TESTE", 2, "OpcaoDeclaradaFato.CodigoRepetido")]
    public void DefinirOpcoesDeclaradas_Recusa(string fato, int quantidade, string codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoConforme();
        List<OpcaoDeclaradaFato> opcoes = [.. Enumerable.Range(0, quantidade).Select(static i => Opcao("2025", i))];

        processo.DefinirOpcoesDeclaradas(fato, opcoes)
            .Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Definir opções declaradas recusa deixar de fora uma opção citada por exigência viva")]
    public void DefinirOpcoesDeclaradas_OpcaoCitadaPorExigencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2024"), Opcao("2025", 1)])
            .IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        processo.DefinirDocumentosExigidosNosFormulariosMinimos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPelaOpcao(faseId, "2024"), 0).Value!])
            .IsSuccess.Should().BeTrue();

        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")])
            .Error!.Code.Should().Be("OpcaoDeclaradaFato.ReferenciadaPorExigenciaViva");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Termos exigidos pelo formulário (UNI-REQ-0086)
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static void ConferirItemVermelhoQueNomeia(
        ProcessoSeletivo processo, string codigoDoItem, string dimensao, string[] nomeia, string[] naoNomeia)
    {
        SoEstesItensVermelhos(processo, codigoDoItem);
        ItemConformidade item = Item(processo, codigoDoItem);
        item.Dimensao.Should().Be(dimensao);
        item.Mensagem.Should().ContainAll(nomeia).And.NotContainAny(naoNomeia);
    }

    private static void ConferirQuePublicarRecusaNomeando(ProcessoSeletivo processo, string trechoDaMensagem)
    {
        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain(trechoDaMensagem);
    }

    private static DocumentoExigido ExigenciaCondicionalPelaOpcao(Guid faseId, string opcao) => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COMPROVANTE_2024", "Comprovante 2024", "ACADEMICO", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, "FATO_OPCAO_DE_TESTE", Operador.Igual, JsonSerializer.SerializeToElement(opcao)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [BaseLegalResolvida()], false))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;
}
