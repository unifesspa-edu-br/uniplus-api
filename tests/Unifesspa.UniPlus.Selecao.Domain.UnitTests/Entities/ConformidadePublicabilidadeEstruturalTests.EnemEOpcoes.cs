namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

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
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class ConformidadePublicabilidadeEstruturalTests
{
    [Theory(DisplayName = "ENEM com cálculo local: resolução ausente ou em branco, ou quadro vazio, estado que só chega sem passar pela fábrica — só o item da resolução fica vermelho, e Publicar recusa")]
    [InlineData(FaltaNaResolucao.ResolucaoAusente)]
    [InlineData(FaltaNaResolucao.ResolucaoEmBranco)]
    [InlineData(FaltaNaResolucao.QuadroVazio)]
    public void Enem_ResolucaoOuQuadroAusente(FaltaNaResolucao falta)
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        PropertyInfo resolucao = typeof(ConfiguracaoClassificacao).GetProperty(nameof(ConfiguracaoClassificacao.ResolucaoPesoAreaEnem))!;

        switch (falta)
        {
            case FaltaNaResolucao.ResolucaoAusente:
                resolucao.SetValue(processo.Classificacao, null);
                break;
            case FaltaNaResolucao.ResolucaoEmBranco:
                resolucao.SetValue(processo.Classificacao, "   ");
                break;
            default:
                EsvaziarQuadro(processo.Classificacao!);
                break;
        }

        SoEstesItensVermelhos(processo, "classificacao_resolucao_peso_area_enem_ausente");
        Item(processo, "classificacao_resolucao_peso_area_enem_ausente").Dimensao.Should().Be(DimensaoConformidade.Classificacao);
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Any(static i => i.Codigo == "desempate_area_enem_sem_quadro")
            .Should().Be(falta != FaltaNaResolucao.QuadroVazio, "o item do desempate sem quadro só sai do checklist quando é o quadro que falta");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain("Resolução de Pesos por Área");
    }

    [Fact(DisplayName = "Oferta sem nome congelado — só o item do nome fica vermelho, nomeando a oferta, e Publicar recusa")]
    public void OfertaSemNomeCongelado()
    {
        ConfiguracaoDistribuicaoVagas nomeada = OfertaAmpla(10, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas semNome = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 5, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 5, null)]).Value!;
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

        SoEstesItensVermelhos(processo, "distribuicao_vagas_oferta_sem_grupo_area_enem");
        ItemConformidade item = Item(processo, "distribuicao_vagas_oferta_sem_grupo_area_enem");
        item.Dimensao.Should().Be(DimensaoConformidade.DistribuicaoVagas);
        item.Mensagem.Should().Contain(semGrupo.OfertaCursoOrigemId.ToString())
            .And.Contain(outraSemGrupo.OfertaCursoOrigemId.ToString())
            .And.NotContain(comGrupo.OfertaCursoOrigemId.ToString());

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain(outraSemGrupo.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: com a retificação aberta, a distribuição regravada com oferta sem grupo faz o fechamento recusar pelo mesmo item")]
    public void Enem_FecharRetificacaoComOfertaSemGrupo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        Result<VersaoConfiguracao> publicada = Publicar(processo);
        publicada.IsSuccess.Should().BeTrue(publicada.Error?.Message);

        // A retificação aberta libera a gravação da distribuição; regravá-la depois de o curso
        // perder o grupo no cadastro congela a oferta sem grupo.
        processo.AbrirRetificacao("Ajustar as vagas", publicada.Value!, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "teste", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        ConfiguracaoDistribuicaoVagas semGrupo = OfertaAmpla(12, grupoAreaEnem: null);
        processo.DefinirDistribuicaoVagas([semGrupo], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> fechar = processo.FecharRetificacao(
            Dados(), publicada.Value!, "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste",
            PrecondicaoIfMatch.Curinga, TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        fechar.IsFailure.Should().BeTrue();
        fechar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        fechar.Error.Message.Should().Contain(semGrupo.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: a recusa de Publicar fica bem pontuada com um item e com vários")]
    public void Enem_MensagemAgregadaBemPontuada()
    {
        ConfiguracaoDistribuicaoVagas[] semGrupo = [.. new[] { OfertaAmpla(5, grupoAreaEnem: null), OfertaAmpla(4, grupoAreaEnem: null) }
            .OrderBy(static o => o.OfertaCursoOrigemId)];
        string umItem = Publicar(ProcessoEnemLocal(semGrupo)).Error!.Message;
        DeveEstarBemPontuada(umItem);
        umItem.Should().Contain($"(ofertas: {semGrupo[0].OfertaCursoOrigemId}; {semGrupo[1].OfertaCursoOrigemId})",
            "os Ids se separam por ponto e vírgula, e o agregador separa os itens por vírgula");

        ConfiguracaoDistribuicaoVagas outraSemGrupo = OfertaAmpla(5, grupoAreaEnem: null);
        ConfiguracaoDistribuicaoVagas foraDoQuadro = OfertaAmpla(10, GrupoTecnologica);
        string doisItens = Publicar(ProcessoEnemLocal(
            [outraSemGrupo, foraDoQuadro], [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I")])).Error!.Message;
        DeveEstarBemPontuada(doisItens);
        doisItens.Should().Contain(outraSemGrupo.OfertaCursoOrigemId.ToString())
            .And.Contain($"{foraDoQuadro.OfertaCursoOrigemId} (grupo TECNOLOGICA — Tecnológica))");
    }

    /// <summary>A redação pode mudar; a pontuação da mensagem agregada, não.</summary>
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

        SoEstesItensVermelhos(processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro");
        ItemConformidade item = Item(processo, "classificacao_grupo_area_enem_da_oferta_fora_do_quadro");
        item.Dimensao.Should().Be(DimensaoConformidade.Classificacao);
        item.Mensagem.Should()
            .Contain($"{foraDoQuadro.OfertaCursoOrigemId} (grupo TECNOLOGICA — Tecnológica)")
            .And.NotContain(noQuadro.OfertaCursoOrigemId.ToString());

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain(foraDoQuadro.OfertaCursoOrigemId.ToString());
    }

    [Fact(DisplayName = "ENEM com cálculo local: classificação sem quadro gravada sobre desempate por área recusa no campo da resolução, nunca em silêncio")]
    public void Enem_DefinirClassificacaoSemQuadroComDesempatePorArea_RecusaNaResolucao()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao semQuadro = ClassificacaoEnemLocal(QuadroPesoAreaEnemDeTeste.Completo());
        EsvaziarQuadro(semQuadro);

        Result resultado = processo.DefinirClassificacao(semQuadro, PrecondicaoIfMatch.Curinga);

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
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        EsvaziarQuadro(processo.Classificacao!);

        SoEstesItensVermelhos(processo, "classificacao_resolucao_peso_area_enem_ausente");
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Select(static i => i.Codigo).Should().NotContain(
            "desempate_area_enem_sem_quadro", "sairia verde com a descrição de que há quadro, justamente quando ele falta");
        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull(
            "o gate diz o mesmo que o checklist: a falta do quadro é só pendência da resolução");

        // A deduplicação é da conformidade: a gravação do desempate continua recusando.
        Result gravar = processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"])).Value!],
            PrecondicaoIfMatch.Curinga);
        gravar.IsFailure.Should().BeTrue();
        gravar.Error!.Code.Should().Be("ProcessoSeletivo.DesempateAreaEnemSemQuadro");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName = "Contraprova: cálculo local que não é baseado em ENEM não traz os itens da média pelos pesos por área, dispensa grupo de área na oferta, e Publicar conclui")]
    public void Enem_CalculoLocalSemEnemDispensaGrupo()
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas([OfertaAmpla(10, grupoAreaEnem: null)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false,
                resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Select(static i => i.Codigo).Should().NotContain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro"]);
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Contraprova: classificação baseada em ENEM importada não traz os itens da média local, dispensa grupo de área na oferta, e Publicar conclui")]
    public void Enem_ClassificacaoImportadaDispensaGrupo()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, grupoAreaEnem: null)]);
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
                resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Select(static i => i.Codigo).Should().NotContain(
            ["classificacao_resolucao_peso_area_enem_ausente", "distribuicao_vagas_oferta_sem_grupo_area_enem", "classificacao_grupo_area_enem_da_oferta_fora_do_quadro"]);
        SoEstesItensVermelhos(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Opções declaradas pelo processo (issue #1619)
    // ══════════════════════════════════════════════════════════════════════════════════════

    private static FatoColetado FatoDeOpcaoDeTeste() => FatoColetado.Criar(
        "FATO_OPCAO_DE_TESTE", 0, "Fato de opção do processo", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
        origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static OpcaoDeclaradaFato Opcao(string codigo, int ordem = 0) =>
        OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", codigo, $"Opção {codigo}", ordem).Value!;

    [Fact(DisplayName = "Fato coletado de fonte do processo sem opção declarada: item vermelho e publicação recusada; com opção, liberado")]
    public void FatoDeFonteDoProcessoSemOpcao_RecusaAtePrimeiraOpcao()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItensComFaseDeInscricao([FatoDeOpcaoDeTeste()]).IsSuccess.Should().BeTrue();

        VermelhosForaDoFormulario(processo).Should().ContainSingle()
            .Which.Codigo.Should().Be("fato_coletavel_sem_valores_ofertados");

        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();
        processo.OpcoesDoProcesso("FATO_OPCAO_DE_TESTE").Should().ContainSingle().Which.Codigo.Should().Be("2025");
    }

    [Theory(DisplayName = "Definir opções declaradas recusa fato da oferta de atendimento, lista vazia e código repetido")]
    [InlineData("CONDICAO_ATENDIMENTO", 1, "OpcaoDeclaradaFato.GeridasPelaOfertaDeAtendimento")]
    [InlineData("FATO_OPCAO_DE_TESTE", 0, "OpcaoDeclaradaFato.ListaVazia")]
    [InlineData("FATO_OPCAO_DE_TESTE", 2, "OpcaoDeclaradaFato.CodigoRepetido")]
    public void DefinirOpcoesDeclaradas_Recusa(string fato, int quantidade, string codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoConforme();
        List<OpcaoDeclaradaFato> opcoes = [.. Enumerable.Range(0, quantidade).Select(static i => Opcao("2025", i))];

        processo.DefinirOpcoesDeclaradas(fato, opcoes, PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Definir opções declaradas recusa deixar de fora uma opção citada por exigência viva")]
    public void DefinirOpcoesDeclaradas_OpcaoCitadaPorExigencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2024"), Opcao("2025", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "COMPROVANTE_2024", "Comprovante 2024", "ACADEMICO", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [CondicaoGatilho.Criar(0, "FATO_OPCAO_DE_TESTE", Operador.Igual, JsonSerializer.SerializeToElement("2024")).Value!],
            basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [Opcao("2025")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("OpcaoDeclaradaFato.ReferenciadaPorExigenciaViva");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Termos exigidos pelo formulário (UNI-REQ-0086)
    // ══════════════════════════════════════════════════════════════════════════════════════
}
