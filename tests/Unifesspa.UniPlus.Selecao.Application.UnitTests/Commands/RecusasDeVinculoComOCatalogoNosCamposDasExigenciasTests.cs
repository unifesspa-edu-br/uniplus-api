namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// Na definição dos documentos exigidos, a recusa de vínculo novo com o catálogo aponta o campo do pedido que cita o dado ou o
/// valor, e sai junto das recusas que o domínio daria, sem gravar nada.
/// </summary>
public sealed class RecusasDeVinculoComOCatalogoNosCamposDasExigenciasTests
{
    private const string DadoDesativado = "VinculoCatalogo.FatoDesativado";
    private const string ValorDesativado = "VinculoCatalogo.ValorDesativado";
    private const string FaseFora = "DocumentoExigido.FaseNaoPertenceAoProcesso";

    private static FatoCandidatoView FaixaEtariaDesativada() => FatoTipoDeficiencia() with
    {
        Codigo = "FAIXA_ETARIA",
        Nome = "Faixa etária",
        Binding = "ATRIBUTO_CANDIDATO:FAIXA_ETARIA",
        Ativo = false,
        ValoresDominio = null,
        ValoresDominioDeclarados = null,
        FonteValores = null,
        Origem = CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado),
        Dominio = CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Numerico),
    };

    private static FatoCandidatoView TipoDeficienciaComTeaDesativado() => FatoTipoDeficiencia() with
    {
        ValoresDominioDeclarados =
        [
            new("DEFICIENCIA_VISUAL", "Deficiência visual", 0, Ativo: true),
            new("TEA", "Transtorno do espectro autista", 1, Ativo: false),
        ],
    };

    private static FatoCandidatoView OutroDadoAtivo() => FatoTipoDeficiencia() with
    {
        Codigo = "OUTRO_DADO",
        Nome = "Outro dado",
        Binding = "CAMPO_FORMULARIO:OUTRO_DADO",
    };

    private static FatoCandidatoView TipoDeficienciaDesativado() => FatoTipoDeficiencia() with { Ativo = false };

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(Result resultado) =>
        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code));

    private static DefinirDocumentosExigidosCommand Comando(CenarioDaExigencia cenario, params RegraDeComprovacaoInformada[] regras) =>
        new(cenario.Processo.Id, regras);

    private static RegraDeComprovacaoInformada Regra(ItemDocumentoExigidoDeTeste item) => RegraDeComprovacaoDeTeste.DeDocumento(item).Construir();

    private static CenarioDaExigencia CenarioComCatalogo(params FatoCandidatoView[] catalogo)
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        cenario.Mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(catalogo);
        return cenario;
    }

    [Fact(DisplayName = "O dado desativado na condição da segunda regra e a fase fora do cronograma da primeira saem juntos, e nada é gravado")]
    public async Task DadoDesativadoNaCondicaoEFaseFora_SaemJuntos()
    {
        CenarioDaExigencia cenario = CenarioComCatalogo(FaixaEtariaDesativada());
        ColetarFato(cenario.Processo, "DATA_NASCIMENTO", TipoDeCampo.Data);
        ItemDocumentoExigidoDeTeste foraDoCronograma = GeralNoFormularioDaExigencia(Guid.CreateVersion7(), cenario.TipoDocumentoId);

        Result resultado = await HandleAsync(cenario.Mocks, Comando(cenario,
            Regra(foraDoCronograma), Regra(cenario.ItemCondicional(new(0, "FAIXA_ETARIA", "MAIOR_IGUAL", "18")))));

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[1].documento.aplicabilidade.condicoes[0].fato", DadoDesativado),
            ("regrasDeComprovacao[0].documento.momento.faseId", FaseFora));
        cenario.Processo.DocumentosExigidos.Should().BeEmpty();
        await cenario.Mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O valor desativado aponta o campo valor da condição, pela posição dela no pedido, e não a ordem das cláusulas")]
    public async Task ValorDesativado_ApontaOValorDaCondicaoNaPosicaoDoPedido()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([TipoDeficienciaComTeaDesativado()]);
        ColetarFato(cenario.Processo, "TIPO_DEFICIENCIA");

        Result resultado = await HandleAsync(cenario.Mocks, Comando(cenario, Regra(cenario.ItemGeral().Condicional(
            new(1, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\""), new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"DEFICIENCIA_VISUAL\"")))));

        Recusas(resultado).Should().Equal(("regrasDeComprovacao[0].documento.aplicabilidade.condicoes[0].valor", ValorDesativado));
    }

    [Fact(DisplayName = "O dado desativado que o documento comprova aponta a posição dele na lista de dados comprovados")]
    public async Task DadoComprovadoDesativado_ApontaAPosicaoNaLista()
    {
        CenarioDaExigencia cenario = CenarioComCatalogo(OutroDadoAtivo(), TipoDeficienciaDesativado());

        Result resultado = await HandleAsync(cenario.Mocks, Comando(cenario,
            Regra(cenario.ItemGeral().ComFatosComprovados("OUTRO_DADO", "TIPO_DEFICIENCIA"))));

        Recusas(resultado).Should().Equal(("regrasDeComprovacao[0].documento.fatosComprovados[1]", DadoDesativado));
    }

    [Fact(DisplayName = "O mesmo dado desativado comprovado por dois documentos gera duas recusas, uma em cada documento")]
    public async Task MesmoDadoEmDoisDocumentos_DuasRecusas()
    {
        CenarioDaExigencia cenario = CenarioComCatalogo(TipoDeficienciaDesativado());

        Result resultado = await HandleAsync(cenario.Mocks, Comando(cenario,
            Regra(cenario.ItemGeral().ComFatosComprovados("TIPO_DEFICIENCIA")), Regra(cenario.ItemGeral().ComFatosComprovados("TIPO_DEFICIENCIA"))));

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[0].documento.fatosComprovados[0]", DadoDesativado),
            ("regrasDeComprovacao[1].documento.fatosComprovados[0]", DadoDesativado));
    }

    [Fact(DisplayName = "O dado desativado comprovado pelo documento de um item de grupo aponta o item dentro da regra do grupo")]
    public async Task DadoComprovadoNoItemDeGrupo_ApontaOItem()
    {
        CenarioDaExigencia cenario = CenarioComCatalogo(OutroDadoAtivo(), TipoDeficienciaDesativado());
        RegraDeComprovacaoInformada grupo = RegraDeComprovacaoDeTeste.Grupo(
            "TODOS_OS_ITENS",
            Regra(cenario.ItemGeral().ComFatosComprovados("OUTRO_DADO")),
            Regra(cenario.ItemGeral().ComFatosComprovados("TIPO_DEFICIENCIA"))).Construir();

        Result resultado = await HandleAsync(cenario.Mocks, Comando(cenario, grupo));

        Recusas(resultado).Should().Equal(("regrasDeComprovacao[0].itens[1].documento.fatosComprovados[0]", DadoDesativado));
    }
}
