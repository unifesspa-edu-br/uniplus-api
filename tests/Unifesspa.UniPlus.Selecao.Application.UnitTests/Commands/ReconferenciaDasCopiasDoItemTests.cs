namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

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
        [.. CatalogoDoConjuntoBasico.Fatos.Select(f => f.Codigo == codigo ? altera(f) : f)];

    private static FatoColetado Texto(string codigo, int ordem, string formato) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, "PESSOAL", formato: formato).Value!;

    private static HashSet<string> Coletados(params string[] fatos) => new(fatos, StringComparer.Ordinal);

    [Fact(DisplayName = "O fato que nenhuma versão publicada coletou é recopiado; o já coletado fica como está")]
    public void Aplicar_FatoJaColetadoNaoERecopiado()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        processo.DefinirItens([Texto("CPF", 0, "CPF"), Texto("RG_NUMERO", 1, "LIVRE")]).IsSuccess.Should().BeTrue();
        FatoColetado novo = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF");
        FatoColetado coletado = processo.FatosColetados.Single(static f => f.FatoCodigo == "RG_NUMERO");

        ReconferenciaDasCopiasDoItem.Aplicar(processo, CatalogoDoConjuntoBasico.Fatos, Coletados("RG_NUMERO")).IsSuccess.Should().BeTrue();

        novo.ClassificacaoProtecao.Should().Be("IDENTIFICADOR");
        coletado.ClassificacaoProtecao.Should().Be("PESSOAL", "o fato já coletado segue a guarda da retificação contra troca de eixo");
    }

    [Fact(DisplayName = "O fato já coletado cujo catálogo mudou de forma incompatível é recusado, orientando desfazer a correção")]
    public void Aplicar_FatoJaColetadoIncoerente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoCom("DESEJA_NOME_SOCIAL", static f => f with { Dominio = "CATEGORICO" });

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, Coletados("DESEJA_NOME_SOCIAL"));

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
        resposta.Error.Message.Should().Contain("desfaça a correção no catálogo").And.Contain("processos novos e rascunhos");
    }

    [Fact(DisplayName = "A recusa de coerência traz a causa real do campo, inclusive a fonte de valores")]
    public void Aplicar_IncoerenciaPorFonte_NomeiaAFonte()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoCom("RG_UF", static f => f with { FonteValores = "GEO_MUNICIPIO" });

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Message.Should().Contain("fonte de valores 'GEO_MUNICIPIO'").And.Contain("Regrave o item");
    }

    [Fact(DisplayName = "A restrição de opções que cita valor que o fato deixou de ter é recusada")]
    public void Aplicar_RestricaoDeOpcoesForaDoDominio_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        FatoColetado corRaca = FatoColetado.Criar(
            "COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null, "PESSOAL",
            restricoes: [new OpcoesPermitidas([new OpcoesCondicionadas(null, ["BRANCA", "PRETA"])])]).Value!;
        processo.DefinirItens([corRaca]).IsSuccess.Should().BeTrue();
        ReconferenciaDasCopiasDoItem.Aplicar(processo, CatalogoDoConjuntoBasico.Fatos, null).IsSuccess.Should().BeTrue("o domínio atual tem os dois valores");

        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoCom("COR_RACA", static f => f with
        {
            ValoresDominio = [.. f.ValoresDominio!.Where(static v => v != "PRETA")],
            ValoresDominioDeclarados = [.. f.ValoresDominioDeclarados!.Where(static v => v.Codigo != "PRETA")],
        });

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Message.Should().Contain("Cor ou raça");
    }

    [Fact(DisplayName = "A recusa junta todos os itens incoerentes no mesmo lote, cada um no caminho do próprio item")]
    public void Aplicar_VariosItensIncoerentes_AcumulaTodasAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        IReadOnlyList<FatoCandidatoView> catalogo = [
            .. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo switch
            {
                "DESEJA_NOME_SOCIAL" => f with { Dominio = "CATEGORICO" },
                "RG_UF" => f with { FonteValores = "GEO_MUNICIPIO" },
                _ => f,
            }),
        ];

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        resposta.Errors.Select(static e => e.Field).Should().OnlyContain(static campo => campo.StartsWith("formularios[INSCRICAO].itens[", StringComparison.Ordinal))
            .And.OnlyHaveUniqueItems();
        resposta.Errors.Select(static e => e.Error.Message).Should().Contain(m => m.Contains("fonte de valores 'GEO_MUNICIPIO'"));
    }

    [Fact(DisplayName = "O predicado de obrigatoriedade que cita valor que o fato deixou de ter é recusado, como na gravação do item")]
    public void Aplicar_PredicadoDeObrigatoriedadeComValorRemovido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        FatoCandidatoView temRenda = Fato("DESEJA_NOME_SOCIAL") with { Id = Guid.CreateVersion7(), Codigo = "TEM_RENDA", Nome = "Tem renda?", Binding = "CAMPO_FORMULARIO:TEM_RENDA" };
        PredicadoDnf quandoPreta = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!;
        FatoColetado item = FatoColetado.Criar(
            "TEM_RENDA", 100, "Tem renda?", TipoRenderizacao.Booleano, Obrigatoriedade.Quando(quandoPreta), null, "PESSOAL").Value!;
        Result definido = processo.DefinirItens([item]);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        IReadOnlyList<FatoCandidatoView> vigente = CatalogoDoConjuntoBasico.Com([temRenda]);
        ReconferenciaDasCopiasDoItem.Aplicar(processo, vigente, null).IsSuccess.Should().BeTrue("o domínio atual tem PRETA");

        IReadOnlyList<FatoCandidatoView> catalogo = [.. vigente.Select(static f => f.Codigo != "COR_RACA" ? f : f with
        {
            ValoresDominio = [.. f.ValoresDominio!.Where(static v => v != "PRETA")],
            ValoresDominioDeclarados = [.. f.ValoresDominioDeclarados!.Where(static v => v.Codigo != "PRETA")],
        })];

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().Contain(static e => e.Field != null && e.Field.EndsWith("predicadoObrigatoriedade", StringComparison.Ordinal));
        ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, Coletados("TEM_RENDA")).IsSuccess
            .Should().BeTrue("o fato já coletado só tem o campo conferido");
    }

    [Fact(DisplayName = "A exibição do grupo que cita valor que o fato deixou de ter é recusada, como na gravação do grupo")]
    public void Aplicar_ExibicaoDoGrupoComValorRemovido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        FatoCandidatoView membro = Fato("DESEJA_NOME_SOCIAL") with
        {
            Id = Guid.CreateVersion7(),
            Codigo = "MENOR_SOB_GUARDA",
            Nome = "Menor sob guarda",
            Binding = "CAMPO_FORMULARIO:MENOR_SOB_GUARDA",
            Escopo = "MEMBRO_GRUPO",
        };
        IReadOnlyList<FatoCandidatoView> vigente = CatalogoDoConjuntoBasico.Com([membro]);
        PredicadoDnf quandoPreta = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!;
        FatoColetado[] itens = [.. processo.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)];
        GrupoColetado grupo = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", itens.Max(static f => f.Ordem) + 1, itens.First().EtapaCodigo, "Composição familiar", 0, 10, quandoPreta, Obrigatoriedade.Nunca,
            [FatoColetado.Criar("MENOR_SOB_GUARDA", 0, "Menor sob guarda", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, "PESSOAL").Value!],
            FinalidadeFormulario.Inscricao).Value!;
        Result definido = processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, itens, PrecondicaoIfMatch.Ausente, [grupo]);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        ReconferenciaDasCopiasDoItem.Aplicar(processo, vigente, null).IsSuccess.Should().BeTrue("o domínio atual tem PRETA");

        IReadOnlyList<FatoCandidatoView> catalogo = [.. vigente.Select(static f => f.Codigo != "COR_RACA" ? f : f with
        {
            ValoresDominio = [.. f.ValoresDominio!.Where(static v => v != "PRETA")],
            ValoresDominioDeclarados = [.. f.ValoresDominioDeclarados!.Where(static v => v.Codigo != "PRETA")],
        })];

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("formularios[INSCRICAO].grupos[0].exibicao");
        resposta.Error!.Message.Should().Contain("Composição familiar").And.Contain("Regrave o grupo");
    }

    [Fact(DisplayName = "As recusas de dois formulários saem no caminho do formulário de cada um")]
    public void Aplicar_ItensIncoerentesEmDoisFormularios_CaminhosDistintos()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        FatoCandidatoView extra = Fato("DESEJA_NOME_SOCIAL") with
        {
            Id = Guid.CreateVersion7(),
            Codigo = "SITUACAO_EXTRA",
            Nome = "Situação extra",
            Binding = "CAMPO_FORMULARIO:SITUACAO_EXTRA",
        };
        FatoColetado doOutroFormulario = FatoColetado.Criar("SITUACAO_EXTRA", 0, "Situação extra", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, "PESSOAL").Value!;
        Result definido = processo.DefinirItens([doOutroFormulario], finalidade: FinalidadeFormulario.Habilitacao);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        IReadOnlyList<FatoCandidatoView> catalogo = [
            .. CatalogoDoConjuntoBasico.Com([extra]).Select(static f => f.Codigo is "DESEJA_NOME_SOCIAL" or "SITUACAO_EXTRA" ? f with { Dominio = "CATEGORICO" } : f),
        ];

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Select(static e => e.Field).Should().Contain([
            "formularios[HABILITACAO].itens[0].tipoRenderizacao",
        ]).And.Contain(static campo => campo.StartsWith("formularios[INSCRICAO].itens[", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A recusa da semântica não muda o agregado: a cópia do item conforme só é refeita sem nenhuma recusa")]
    public void Aplicar_RecusaDaSemantica_NaoMudaOAgregado()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Cópias do item", out _);
        string antes = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF").ClassificacaoProtecao;
        IReadOnlyList<FatoCandidatoView> catalogo = [
            .. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo switch
            {
                "CPF" => f with { ClassificacaoProtecao = "PUBLICO" },
                "RG_UF" => f with { FonteValores = "GEO_MUNICIPIO" },
                _ => f,
            }),
        ];
        antes.Should().NotBe("PUBLICO", "pré-condição: há cópia a refazer");

        Result resposta = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogo, null);

        resposta.IsFailure.Should().BeTrue();
        processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF").ClassificacaoProtecao.Should().Be(antes);
    }
}
