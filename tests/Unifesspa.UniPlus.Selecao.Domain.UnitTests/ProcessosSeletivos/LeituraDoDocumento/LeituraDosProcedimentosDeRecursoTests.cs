namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

public sealed class LeituraDosProcedimentosDeRecursoTests
{
    private static readonly ReferenciaDeRegraNoDocumento Ancorada = new(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1");

    private static FaseNoDocumento ComRegra(ReferenciaDeRegraNoDocumento regra, params ProcedimentoNoDocumento[] procedimentos) =>
        Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, AtoDoResultado)) with
        {
            RegraDeRecurso = new(regra, AtoDoResultado, ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis), procedimentos),
        };

    [Fact(DisplayName = "O procedimento com regra desconhecida e item de avaliação como alvo acumula as duas recusas")]
    public void Procedimento_RegraEAlvoInvalidos_AcumulaRecusas()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento("item-digitado", null), new("REGRA-DIGITADA", "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimentos: procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().BeEmpty();
        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/alvo", "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo");
    }

    [Fact(DisplayName = "O procedimento com tipo fora do vocabulário, alvo com item e regra desconhecida acumula as três recusas na ordem tipo, alvo e regra")]
    public void Procedimento_TipoAlvoERegraInvalidos_AcumulaNaOrdemDosCampos()
    {
        ProcedimentoNoDocumento procedimento = new(0, null, new AlvoNoDocumento("item-digitado", null), new("REGRA-DIGITADA", "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimentos: procedimento)));

        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/tipoDeDecisao", "MotivoDeDecisao.TipoInvalido"),
            ("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/alvo", "RegraRecursoFase.AlvoDoProcedimentoDeRecursoInexistente"),
            ("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo", "RegraRecursoFase.ProcedimentoDeRecursoDesconhecido"));
    }

    [Fact(DisplayName = "A regra que o catálogo não conhece é recusada com a mesma mensagem da gravação, sem repetir o que foi escrito")]
    public void Procedimento_RegraDesconhecida_RecusaComAMensagemDaGravacao()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new("REGRA-DIGITADA", "versao-digitada"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimentos: procedimento)));

        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo", "RegraRecursoFase.ProcedimentoDeRecursoDesconhecido"));
        Mensagens(lidos).Should().ContainSingle().Which.Should()
            .Be($"O procedimento de recurso informado, ou a versão dele, não consta do catálogo. Procedimentos aceitos: {string.Join(", ", CatalogoDe<RegraDeProcedimentoDoRecurso>.Todos().Select(static item => item.Codigo))}.")
            .And.NotContain("REGRA-DIGITADA")
            .And.NotContain("versao-digitada");
    }

    [Fact(DisplayName = "A versão inexistente da regra do procedimento aponta o código citado")]
    public void Procedimento_ComVersaoInexistente_ApontaOCodigo()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new(CodigoDoProcedimento, "versao-inexistente"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimento)));

        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra/codigo");
    }

    [Fact(DisplayName = "O procedimento sem tipo de decisão do vocabulário é recusado no campo do tipo, sem exceção")]
    public void Procedimento_SemTipoDeDecisaoDoVocabulario_RecusaNoCampoDoTipo()
    {
        ProcedimentoNoDocumento procedimento = new(0, null, null, new(CodigoDoProcedimento, "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().BeEmpty();
        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/tipoDeDecisao", "MotivoDeDecisao.TipoInvalido"));
    }

    [Fact(DisplayName = "A recusa do tipo de decisão na leitura lista os tipos aceitos, como a gravação, e acompanha a posição do procedimento")]
    public void Procedimento_SemTipoDeDecisaoDoVocabulario_RecusaComAMensagemDoVocabulario()
    {
        ProcedimentoNoDocumento anterior = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new(CodigoDoProcedimento, "v1"));
        ProcedimentoNoDocumento procedimento = new(1, null, null, new(CodigoDoProcedimento, "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, anterior, procedimento)));

        PartesRecusadas(lidos).Should().Equal(
            ("/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/1/tipoDeDecisao", "MotivoDeDecisao.TipoInvalido"));
        Mensagens(lidos).Should().ContainSingle().Which.Should()
            .Be($"O tipo informado não pertence ao vocabulário de decisões. Tipos aceitos: {TipoDoMotivoInformado.RotulosAceitosEmTexto}.");
        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().ContainSingle();
    }

    [Theory(DisplayName = "A leitura aceita área do ENEM e recusa regra ou alvo inexistente")]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Procedimento_ReferenciaEAlvo_DefinemAdocao(bool regraInexistente, bool itemInexistente, bool aceito)
    {
        AlvoNoDocumento alvo = itemInexistente ? new("item-digitado", null) : new(null, "LINGUAGENS");
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, alvo, new(regraInexistente ? "REGRA-DIGITADA" : CodigoDoProcedimento, "v1"));
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComRegra(Ancorada, procedimento)));

        lidos.Cronograma.CronogramaFases.Single().RegraRecurso!.Procedimentos.Itens.Should().HaveCount(aceito ? 1 : 0);
        lidos.Origem.PartesRecusadas.Should().HaveCount(aceito ? 0 : 1);
    }

    private static ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> LerDaEtapa(
        IReadOnlyDictionary<string, Guid> itensPelaChave, params ProcedimentoNoDocumento[] procedimentos) =>
        LeituraDosProcedimentosDeRecurso.Ler(procedimentos, "recursos[0].procedimentos", 3, CodigosDoProcedimentoDeRecurso.DaEtapa, itensPelaChave, Cadastros());

    [Fact(DisplayName = "O alvo que é item de avaliação da etapa, pela chave declarada, vira o alvo do item")]
    public void Etapa_AlvoComChaveDeItemExistente_AdotaOItem()
    {
        Guid item = Guid.CreateVersion7();
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento("criterio_um", null), new(CodigoDoProcedimento, "v1"));

        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> lidos =
            LerDaEtapa(new Dictionary<string, Guid> { ["criterio_um"] = item }, procedimento);

        lidos.Recusas.Should().BeEmpty();
        lidos.Valor.Should().ContainSingle().Which.Item.Alvo.Should().Be(AlvoDoProcedimentoDeRecurso.DoItem(item));
    }

    [Fact(DisplayName = "O alvo com chave de item que a etapa não declarou é recusado no campo do alvo, com o código da etapa e a posição enviada, sem repetir a chave")]
    public void Etapa_AlvoComChaveDeItemInexistente_RecusaNoCampoDoAlvo()
    {
        ProcedimentoNoDocumento procedimento = new(1, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento("chave-digitada", null), new(CodigoDoProcedimento, "v1"));

        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> lidos =
            LerDaEtapa(new Dictionary<string, Guid> { ["criterio_um"] = Guid.CreateVersion7() }, procedimento);

        lidos.Valor.Should().BeEmpty();
        RecusaDaParte recusa = lidos.Recusas.Should().ContainSingle().Subject;
        recusa.Parte.Should().Be("recursos[0].procedimentos");
        recusa.Campo.Should().Be("recursos[0].procedimentos[1].alvo");
        recusa.PosicaoNoDocumento.Should().Be(3);
        recusa.Erro.Code.Should().Be(CodigosDoProcedimentoDeRecurso.DaEtapa.AlvoInexistente);
        recusa.Erro.Message.Should().NotContain("chave-digitada");
    }

    [Fact(DisplayName = "A área do ENEM vale como alvo na etapa, com ou sem item declarado")]
    public void Etapa_AlvoComAreaDoEnem_AdotaAArea()
    {
        ProcedimentoNoDocumento procedimento = new(0, TipoMotivoDeDecisao.NotaDaEtapa, new AlvoNoDocumento(null, "LINGUAGENS"), new(CodigoDoProcedimento, "v1"));

        LerDaEtapa(new Dictionary<string, Guid>(), procedimento).Valor.Should().ContainSingle().Which.Item.Alvo.Should().Be(AlvoDoProcedimentoDeRecurso.DaArea("LINGUAGENS"));
    }

    [Fact(DisplayName = "A regra fora do catálogo na etapa é recusada com o código da etapa, e o procedimento seguinte continua adotado")]
    public void Etapa_RegraForaDoCatalogo_OmiteSoOProcedimento()
    {
        ProcedimentoNoDocumento recusado = new(0, TipoMotivoDeDecisao.NotaDaEtapa, null, new("REGRA-DIGITADA", "v1"));
        ProcedimentoNoDocumento adotado = new(1, TipoMotivoDeDecisao.NotaDaEtapa, null, new(CodigoDoProcedimento, "v1"));

        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> lidos = LerDaEtapa(new Dictionary<string, Guid>(), recusado, adotado);

        lidos.Valor.Select(static par => par.PosicaoNoDocumento).Should().Equal(1);
        lidos.Recusas.Should().ContainSingle().Which.Erro.Code.Should().Be(CodigosDoProcedimentoDeRecurso.DaEtapa.Desconhecido);
    }
}
