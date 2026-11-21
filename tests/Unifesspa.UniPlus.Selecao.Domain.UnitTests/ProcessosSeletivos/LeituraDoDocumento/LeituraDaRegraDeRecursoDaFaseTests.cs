namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>A regra de recurso da fase entra quando o catálogo a tem como regra de prazo ancorado em ato; senão a fase fica sem ela e a parte recusada suspende o que depende dela. O procedimento recusado é omitido e suspende só a cobertura da fase.</summary>
public sealed class LeituraDaRegraDeRecursoDaFaseTests
{
    private static readonly ParametrosDeclaradosDoRecurso PrazoEmDiasUteis = ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis);

    private static FaseNoDocumento ComRegra(ReferenciaDeRegraNoDocumento regra, ParametrosDeclaradosDoRecurso? prazo = null, params ProcedimentoNoDocumento[] procedimentos) =>
        Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, AtoDoResultado)) with
        {
            RegraDeRecurso = new(regra, AtoDoResultado, prazo ?? PrazoEmDiasUteis, procedimentos),
        };

    private static FaseNoDocumento ComTipos(FaseNoDocumento fase, params TipoMotivoDeDecisao[] tipos) =>
        fase with { Publicacoes = fase.Publicacoes with { TiposDeDecisao = [.. tipos.Select(static tipo => tipo.Codigo())] } };

    private static string[] CodigosDasCausas(IdentificacaoECronogramaLidos lidos) =>
        [.. CausasQueImpedemCongelar.De(Configuracao(lidos), CadastrosDeTeste.Padrao).Select(static causa => causa.Error.Code)];

    private static FieldError[] CausasComOCodigo(IdentificacaoECronogramaLidos lidos, string codigo) =>
        [.. CausasQueImpedemCongelar.De(Configuracao(lidos), CadastrosDeTeste.Padrao).Where(causa => causa.Error.Code == codigo)];

    private static bool ItemDoProcedimentoOk(IdentificacaoECronogramaLidos lidos) =>
        ChecklistDeConformidade.AvaliarConformidade(Configuracao(lidos), CadastrosDeTeste.Padrao)
            .Single(static item => item.Codigo == "cronograma_procedimento_de_recurso").Ok;

    private static readonly (string? Ponteiro, string Codigo)[] CausasDosParametros =
    [
        ("/cronograma/fases/0/regraRecurso/prazoValor", "RegraRecursoFase.PrazoNaoPositivo"),
        ("/cronograma/fases/0/regraRecurso/efeitoSuspensivo", "RegraRecursoFase.EfeitoSuspensivoNaoDeclarado"),
        ("/cronograma/fases/0/regraRecurso/guardaDaVaga", "RegraRecursoFase.GuardaDaVagaNaoDeclaravel"),
        ("/cronograma/fases/0/regraRecurso/prazoSegundaInstanciaValor", "RegraRecursoFase.PrazoSegundaInstanciaSemInstanciaSuperior"),
    ];

    private static ReferenciaDeRegraNoDocumento Ancorada => new(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1");

    [Fact(DisplayName = "A regra de prazo ancorado em ato entra na fase com o produto preliminar do ato citado como âncora")]
    public void Regra_Ancorada_EntraComAAncoraDoPreliminar()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada)));

        FaseCronograma fase = lidos.Cronograma.CronogramaFases.Single();
        fase.RegraRecurso!.ProdutoAncoraId.Should().Be(fase.Produtos.Single().Id);
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra que o catálogo não tem recusa a regra de recurso inteira e aponta o campo da regra")]
    public void Regra_Inexistente_ApontaOCampoDaRegra()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(new("REGRA-DIGITADA", "v1"))));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/regraRecurso/regraCodigo", "RegraRecursoFase.RegraCatalogoInvalida"));
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("DIGITADA", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A regra que o catálogo não tem não esconde o prazo que o domínio recusa")]
    public void RegraInexistente_PrazoInvalido_AcumulaRecusas()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(new("REGRA-DIGITADA", "v1"), PrazoEmDiasUteis with { PrazoDeInterposicao = new PrazoDeclarado(0m, "diasUteis") })));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/regraRecurso/regraCodigo", "RegraRecursoFase.RegraCatalogoInvalida"),
            ("/cronograma/fases/0/regraRecurso/prazoValor", "RegraRecursoFase.PrazoNaoPositivo"));
    }

    [Fact(DisplayName = "A regra do catálogo que não é de prazo de recurso é recusada como regra inválida")]
    public void Regra_DeOutroTipo_EhRecusada()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(new(CodigoDaConvencao, "v1"))));

        PartesRecusadas(lidos).Select(static parte => parte.Codigo).Should().Equal("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "O procedimento cuja regra o catálogo não tem é omitido: a regra é adotada com os procedimentos que ficaram")]
    public void Procedimento_SemRegraNoCatalogo_CortaSoOProcedimento()
    {
        ProcedimentoNoDocumento recusado = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new("PROCEDIMENTO-DIGITADO", "v1"));
        ProcedimentoNoDocumento aceito = new(1, TipoMotivoDeDecisao.Classificacao, null, new(CodigoDoProcedimento, "v1"));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, null, recusado, aceito)));

        RegraRecursoFase regra = lidos.Cronograma.CronogramaFases.Single().RegraRecurso!;
        regra.Procedimentos.Itens.Should().ContainSingle().Which.TipoDeDecisao.Should().Be(TipoMotivoDeDecisao.Classificacao);
        regra.Parametros.Should().Be(PrazoEmDiasUteis.Conferir().Conferidos);
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo", "RegraRecursoFase.ProcedimentoDeRecursoDesconhecido"));
        lidos.Origem.TemParteRecusadaEm("fases[0].regraRecurso.procedimentos").Should().BeTrue();
        lidos.Origem.TemParteRecusadaEm("fases[0].regraRecurso").Should().BeFalse();
    }

    [Fact(DisplayName = "Com procedimento recusado, o prazo e a contagem em dias úteis seguem avaliados")]
    public void ProcedimentoRecusado_NaoSuspendeAContagem()
    {
        ProcedimentoNoDocumento recusado = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new("PROCEDIMENTO-DIGITADO", "v1"));

        IdentificacaoECronogramaLidos lidos = Ler(Identificacao() with { AlgoritmoContagemPrazo = null }, ComRegra(Ancorada, null, recusado));

        ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(Configuracao(lidos)).Should().NotBeNull();
        ContagemDePrazos.ContagemIndeterminadaPelaLeitura(Configuracao(lidos)).Should().BeFalse();
    }

    [Fact(DisplayName = "O procedimento recusado suspende a cobertura da fase, sem causa de ausência falsa, e deixa o item pendente")]
    public void ProcedimentoRecusado_SuspendeACobertura_EDeixaOItemPendente()
    {
        ProcedimentoNoDocumento recusado = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new("PROCEDIMENTO-DIGITADO", "v1"));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComTipos(ComRegra(Ancorada, null, recusado), TipoMotivoDeDecisao.NotaDaEtapa)));

        CodigosDasCausas(lidos).Should().Contain(CodigosDoProcedimentoDeRecurso.DaFase.Desconhecido)
            .And.NotContain(CodigosDoProcedimentoDeRecurso.DaFase.Ausente);
        ItemDoProcedimentoOk(lidos).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem procedimento recusado, o tipo de decisão da fase sem procedimento é causa de ausência e o item fica pendente")]
    public void SemProcedimento_ETipoDeclarado_EAusencia()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComTipos(ComRegra(Ancorada), TipoMotivoDeDecisao.NotaDaEtapa)));

        CodigosDasCausas(lidos).Should().Contain(CodigosDoProcedimentoDeRecurso.DaFase.Ausente);
        ItemDoProcedimentoOk(lidos).Should().BeFalse();
    }

    [Fact(DisplayName = "A incoerência dos procedimentos que vêm do documento é recusada pela mesma conferência, na posição de origem")]
    public void ProcedimentosDuplicados_ApontamAPosicaoDeOrigem()
    {
        ProcedimentoNoDocumento recusado = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new("PROCEDIMENTO-DIGITADO", "v1"));
        ProcedimentoNoDocumento primeiro = new(1, TipoMotivoDeDecisao.Classificacao, null, new(CodigoDoProcedimento, "v1"));
        ProcedimentoNoDocumento repetido = primeiro with { PosicaoNoDocumento = 2 };

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComTipos(ComRegra(Ancorada, null, recusado, primeiro, repetido), TipoMotivoDeDecisao.Classificacao)));

        PonteiroNoDocumento.Converter(CausasComOCodigo(lidos, CodigosDoProcedimentoDeRecurso.DaFase.Duplicado), lidos.Origem)
            .Select(static causa => causa.Field).Should().Equal("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/2");
    }

    [Fact(DisplayName = "O procedimento de tipo que a fase não declara é recusado pela mesma conferência")]
    public void Procedimento_DeTipoNaoDeclarado_ERecusado()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new(CodigoDoProcedimento, "v1"));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, null, procedimento)));

        PonteiroNoDocumento.Converter(CausasComOCodigo(lidos, CodigosDoProcedimentoDeRecurso.DaFase.ParaDecisaoNaoDeclarada), lidos.Origem)
            .Select(static causa => causa.Field).Should().Equal("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/tipoDeDecisao");
    }

    [Fact(DisplayName = "O procedimento com regra do catálogo e área do ENEM como alvo entra na regra de recurso da fase")]
    public void Procedimento_ComAreaDoEnem_EntraNaRegra()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento(null, "LINGUAGENS"), new(CodigoDoProcedimento, "v1"));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimentos: procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().ContainSingle();
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O alvo por item de avaliação não existe na fase: omite só o procedimento, com o texto único da conferência")]
    public void Procedimento_ComItemDeAvaliacaoComoAlvo_CortaSoOProcedimento()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento("item-digitado", null), new(CodigoDoProcedimento, "v1"));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, null, procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().BeEmpty();
        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/alvo");
        Mensagens(lidos).Should().Equal(ConferenciaDosProcedimentosDeRecurso.AlvoInexistente(CodigosDoProcedimentoDeRecurso.DaFase).Message);
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("digitado", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O prazo que o domínio recusa deixa a fase sem regra de recurso e aponta o campo da regra de recurso")]
    public void Prazo_Recusado_ApontaARegraDeRecurso()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, PrazoEmDiasUteis with { PrazoDeInterposicao = new PrazoDeclarado(0m, "diasUteis") })));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/regraRecurso/prazoValor", "RegraRecursoFase.PrazoNaoPositivo"));
    }

    [Theory(DisplayName = "As causas independentes dos parâmetros são acumuladas em ordem, cada uma no campo dela, com a regra existente ou não")]
    [InlineData(true)]
    [InlineData(false)]
    public void ParametrosInvalidos_AcumulamEmOrdem(bool regraExiste)
    {
        ParametrosDeclaradosDoRecurso parametros = PrazoEmDiasUteis with
        {
            PrazoDeInterposicao = new PrazoDeclarado(0m, "horas"),
            EfeitoSuspensivo = null,
            GuardaDaVaga = "talvez",
            PrazoDaSegundaInstancia = new PrazoDeclarado(1m, "horas"),
        };
        ReferenciaDeRegraNoDocumento regra = regraExiste ? Ancorada : new("INEXISTENTE", "v1");
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(regra, parametros)));

        PartesRecusadas(lidos).Should().Equal(regraExiste
            ? CausasDosParametros
            : [("/cronograma/fases/0/regraRecurso/regraCodigo", "RegraRecursoFase.RegraCatalogoInvalida"), .. CausasDosParametros]);
    }

    [Fact(DisplayName = "A fase sem regra de recurso não tem parte recusada")]
    public void SemRegra_NaoTemParteRecusada()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "resultado", "RESULTADO")));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso.Should().BeNull();
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }
}
