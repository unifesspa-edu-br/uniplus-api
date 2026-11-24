namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A publicação e a retificação recopiam do catálogo só o fato que nenhuma versão publicada coletou;
/// o já coletado não é recopiado, mas o campo dele segue coerente com o fato do catálogo.
/// </summary>
public sealed class ReconferenciaDasCopiasDoItemTests
{
    private static FatoCandidatoView Fato(string codigo) => CatalogoDoConjuntoBasico.Fatos.Single(f => f.Codigo == codigo);

    private static IReadOnlyList<FatoCandidatoView> CatalogoCom(string codigo, Func<FatoCandidatoView, FatoCandidatoView> altera) =>
        CatalogoComAlteracoes((codigo, altera));

    private static IReadOnlyList<FatoCandidatoView> CatalogoComAlteracoes(
        params (string Codigo, Func<FatoCandidatoView, FatoCandidatoView> Altera)[] alteracoes) =>
        [.. CatalogoDoConjuntoBasico.Fatos.Select(f => alteracoes.FirstOrDefault(a => a.Codigo == f.Codigo).Altera?.Invoke(f) ?? f)];

    private static FatoColetado Texto(string codigo, int ordem, string formato) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem).DeTexto(formato).Construir();

    private static HashSet<string> Coletados(params string[] fatos) => new(fatos, StringComparer.Ordinal);

    private static ProcessoSeletivo ProcessoConforme() => ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);

    private static ProcessoSeletivo ProcessoComCpfERgNumero()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirItens([Texto("CPF", 0, "CPF"), Texto("RG_NUMERO", 1, "LIVRE")]).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static FatoColetado ItemDe(ProcessoSeletivo processo, string codigo) =>
        processo.FatosColetados.Single(f => f.FatoCodigo == codigo);

    private static IReadOnlyList<FatoCandidatoView> SemAPretaEmCorRaca(IReadOnlyList<FatoCandidatoView> catalogo) =>
        [.. catalogo.Select(static f => f.Codigo != "COR_RACA" ? f : f with
        {
            ValoresDominio = [.. f.ValoresDominio!.Where(static v => v != "PRETA")],
            ValoresDominioDeclarados = [.. f.ValoresDominioDeclarados!.Where(static v => v.Codigo != "PRETA")],
        })];

    private static PredicadoDnf QuandoCorRacaEPreta() => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!;

    private static FatoCandidatoView FatoDeFormularioNovo(string codigo, string nome, string? escopo = null) => Fato("DESEJA_NOME_SOCIAL") with
    {
        Id = Guid.CreateVersion7(),
        Codigo = codigo,
        Nome = nome,
        Binding = $"CAMPO_FORMULARIO:{codigo}",
        Escopo = escopo ?? Fato("DESEJA_NOME_SOCIAL").Escopo,
    };

    [Fact(DisplayName = "O fato que nenhuma versão publicada coletou é recopiado; o já coletado fica como está")]
    public void Aplicar_FatoJaColetadoNaoERecopiado()
    {
        ProcessoSeletivo processo = ProcessoComCpfERgNumero();

        ReconferenciaDeTeste.Aplicar(processo, CatalogoDoConjuntoBasico.Fatos, Coletados("RG_NUMERO")).IsSuccess.Should().BeTrue();

        ItemDe(processo, "CPF").ClassificacaoProtecao.Should().Be("IDENTIFICADOR");
        ItemDe(processo, "RG_NUMERO").ClassificacaoProtecao.Should().Be("PESSOAL", "o fato já coletado segue a guarda da retificação contra troca de eixo");
    }

    [Fact(DisplayName = "A hipótese legal e a finalidade são recopiadas do catálogo com a classificação, enquanto nenhuma versão publicada coletou o fato")]
    public void Aplicar_RecopiaAHipoteseLegalEAFinalidade()
    {
        ProcessoSeletivo processo = ProcessoComCpfERgNumero();
        IReadOnlyList<FatoCandidatoView> catalogo = [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo is "CPF" or "RG_NUMERO"
            ? f with { HipoteseLegal = "EXECUCAO_POLITICAS_PUBLICAS", FinalidadeTratamento = "Finalidade nova do catálogo." }
            : f)];

        ReconferenciaDeTeste.Aplicar(processo, catalogo, Coletados("RG_NUMERO")).IsSuccess.Should().BeTrue();

        ItemDe(processo, "CPF").HipoteseLegal.Should().Be("EXECUCAO_POLITICAS_PUBLICAS");
        ItemDe(processo, "CPF").FinalidadeTratamento.Should().Be("Finalidade nova do catálogo.");
        ItemDe(processo, "RG_NUMERO").HipoteseLegal.Should().Be(ProtecaoDoFatoDeTeste.HipoteseLegal, "o fato já coletado mantém o que a versão publicada congelou");
        ItemDe(processo, "RG_NUMERO").FinalidadeTratamento.Should().Be(ProtecaoDoFatoDeTeste.Finalidade, ProtecaoDoFatoDeTeste.Descricao);
    }

    [Fact(DisplayName = "O fato já coletado cujo catálogo mudou de forma incompatível é recusado, orientando desfazer a correção")]
    public void Aplicar_FatoJaColetadoIncoerente_Recusa()
    {
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoCom("DESEJA_NOME_SOCIAL", static f => f with { Dominio = CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao) });

        Result resposta = ReconferenciaDeTeste.Aplicar(ProcessoConforme(), catalogo, Coletados("DESEJA_NOME_SOCIAL"));

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
        resposta.Error.Message.Should().Contain("desfaça a correção no catálogo").And.Contain("processos novos e rascunhos");
    }

    [Fact(DisplayName = "A recusa de coerência traz a causa real do campo, inclusive a fonte de valores")]
    public void Aplicar_IncoerenciaPorFonte_NomeiaAFonte()
    {
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoCom("RG_UF", static f => f with { FonteValores = CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Municipios) });

        Result resposta = ReconferenciaDeTeste.Aplicar(ProcessoConforme(), catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Message.Should().Contain("fonte de valores 'MUNICIPIOS'").And.Contain("Regrave o item");
    }

    [Fact(DisplayName = "A restrição de opções que cita valor que o fato deixou de ter é recusada")]
    public void Aplicar_RestricaoDeOpcoesForaDoDominio_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FatoColetado corRaca = FatoColetadoDeTeste.Coletando("COR_RACA", 0)
            .ComRotulo("Cor ou raça")
            .DoTipo(TipoRenderizacao.EscolhaUnica)
            .ComRestricoes(new OpcoesPermitidas([new OpcoesCondicionadas(null, ["BRANCA", "PRETA"])]))
            .Construir();
        processo.DefinirItens([corRaca]).IsSuccess.Should().BeTrue();
        ReconferenciaDeTeste.Aplicar(processo, CatalogoDoConjuntoBasico.Fatos, null).IsSuccess.Should().BeTrue("o domínio atual tem os dois valores");

        Result resposta = ReconferenciaDeTeste.Aplicar(processo, SemAPretaEmCorRaca(CatalogoDoConjuntoBasico.Fatos), null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Message.Should().Contain("Cor ou raça");
    }

    [Fact(DisplayName = "A recusa junta todos os itens incoerentes no mesmo lote, cada um no caminho do próprio item, pela posição dele no documento")]
    public void Aplicar_VariosItensIncoerentes_AcumulaTodasAsRecusas()
    {
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoComAlteracoes(
            ("DESEJA_NOME_SOCIAL", static f => f with { Dominio = CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao) }),
            ("RG_UF", static f => f with { FonteValores = CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Municipios) }));

        Result resposta = ReconferenciaDeTeste.Aplicar(ProcessoConforme(), catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        resposta.Errors.Select(static e => e.Field).Should().OnlyContain(static campo => campo.StartsWith("formularios[0].itens[", StringComparison.Ordinal))
            .And.OnlyHaveUniqueItems();
        resposta.Errors.Select(static e => e.Error.Message).Should().Contain(m => m.Contains("fonte de valores 'MUNICIPIOS'"));
    }

    [Fact(DisplayName = "O predicado de obrigatoriedade que cita valor que o fato deixou de ter é recusado, como na gravação do item")]
    public void Aplicar_PredicadoDeObrigatoriedadeComValorRemovido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComItemObrigatorioQuandoCorRacaEPreta();
        IReadOnlyList<FatoCandidatoView> vigente = CatalogoDoConjuntoBasico.Com([FatoDeFormularioNovo("TEM_RENDA", "Tem renda?")]);
        ReconferenciaDeTeste.Aplicar(processo, vigente, null).IsSuccess.Should().BeTrue("o domínio atual tem PRETA");

        Result resposta = ReconferenciaDeTeste.Aplicar(processo, SemAPretaEmCorRaca(vigente), null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().Contain(static e => e.Field != null && e.Field.EndsWith("predicadoObrigatoriedade", StringComparison.Ordinal));
        ReconferenciaDeTeste.Aplicar(processo, SemAPretaEmCorRaca(vigente), Coletados("TEM_RENDA")).IsSuccess
            .Should().BeTrue("o fato já coletado só tem o campo conferido");
    }

    [Fact(DisplayName = "A exibição do grupo que cita valor que o fato deixou de ter é recusada, como na gravação do grupo")]
    public void Aplicar_ExibicaoDoGrupoComValorRemovido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        IReadOnlyList<FatoCandidatoView> vigente = CatalogoDoConjuntoBasico.Com([FatoDeFormularioNovo("MENOR_SOB_GUARDA", "Menor sob guarda", "MEMBRO_GRUPO")]);
        DefinirGrupoExibidoQuandoCorRacaEPreta(processo);
        ReconferenciaDeTeste.Aplicar(processo, vigente, null).IsSuccess.Should().BeTrue("o domínio atual tem PRETA");

        Result resposta = ReconferenciaDeTeste.Aplicar(processo, SemAPretaEmCorRaca(vigente), null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("formularios[0].grupos[0].exibicao");
        resposta.Error!.Message.Should().Contain("Composição familiar").And.Contain("Regrave o grupo");
    }

    private static ProcessoSeletivo ProcessoComItemObrigatorioQuandoCorRacaEPreta()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        FatoColetado item = FatoColetadoDeTeste.Coletando("TEM_RENDA", 100)
            .ComRotulo("Tem renda?")
            .ComObrigatoriedade(Obrigatoriedade.Quando(QuandoCorRacaEPreta()))
            .Construir();
        Result definido = processo.DefinirItens([item]);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);

        return processo;
    }

    private static void DefinirGrupoExibidoQuandoCorRacaEPreta(ProcessoSeletivo processo)
    {
        FatoColetado[] itens = [.. processo.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)];
        GrupoColetado grupo = GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", itens.Max(static f => f.Ordem) + 1)
            .NaSecao(itens.First().EtapaCodigo)
            .ComOcorrencias(0, 10)
            .ExibidoQuando(QuandoCorRacaEPreta())
            .ComCampos(FatoColetadoDeTeste.Coletando("MENOR_SOB_GUARDA", 0).ComRotulo("Menor sob guarda").Construir())
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .Construir();
        Result definido = processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, itens, [grupo]);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
    }

    private static void DefinirItemNaHabilitacao(ProcessoSeletivo processo, FatoColetado item)
    {
        Result definido = processo.DefinirItens([item], finalidade: FinalidadeFormulario.Habilitacao);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
    }

    [Fact(DisplayName = "As recusas de dois formulários saem no caminho do formulário de cada um, pela posição dele no documento")]
    public void Aplicar_ItensIncoerentesEmDoisFormularios_CaminhosDistintos()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        DefinirItemNaHabilitacao(processo, FatoColetadoDeTeste.Coletando("SITUACAO_EXTRA", 0).ComRotulo("Situação extra").Construir());
        IReadOnlyList<FatoCandidatoView> catalogo = [
            .. CatalogoDoConjuntoBasico.Com([FatoDeFormularioNovo("SITUACAO_EXTRA", "Situação extra")])
                .Select(static f => f.Codigo is "DESEJA_NOME_SOCIAL" or "SITUACAO_EXTRA" ? f with { Dominio = CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao) } : f),
        ];

        Result resposta = ReconferenciaDeTeste.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Select(static e => e.Field).Should().Contain([
            "formularios[1].itens[0].tipoRenderizacao",
        ]).And.Contain(static campo => campo.StartsWith("formularios[0].itens[", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A recusa da semântica não muda o agregado: a cópia do item conforme só é refeita sem nenhuma recusa")]
    public void Aplicar_RecusaDaSemantica_NaoMudaOAgregado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        string antes = ItemDe(processo, "CPF").ClassificacaoProtecao;
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoComAlteracoes(
            ("CPF", static f => f with { ClassificacaoProtecao = "PUBLICO" }),
            ("RG_UF", static f => f with { FonteValores = CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Municipios) }));
        antes.Should().NotBe("PUBLICO", "pré-condição: há cópia a refazer");

        Result resposta = ReconferenciaDeTeste.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        ItemDe(processo, "CPF").ClassificacaoProtecao.Should().Be(antes);
    }
}
