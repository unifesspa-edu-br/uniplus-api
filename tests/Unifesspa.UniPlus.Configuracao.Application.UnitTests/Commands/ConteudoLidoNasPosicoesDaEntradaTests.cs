namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>
/// A recusa de domínio conta as partes lidas; a parte que a leitura deixou de fora desloca as seguintes, e a recusa volta
/// à posição que veio na entrada.
/// </summary>
public sealed class ConteudoLidoNasPosicoesDaEntradaTests
{
    private static readonly DomainError Recusa = new("Teste.Recusa", "Recusa de teste.");

    private static readonly IReadOnlyDictionary<string, string> SemFormatos = new Dictionary<string, string>(StringComparer.Ordinal);

    [Fact(DisplayName = "A posição 1 não se confunde com a 10: cada uma volta à sua posição de entrada")]
    public void Itens_PosicaoUmENaoDez_VoltamCadaUmaASuaPosicao()
    {
        FatoColetadoInput[] itens = [ItemNaoLido("CERTIFICADO", 0), .. Enumerable.Range(1, 11).Select(static i => Item($"FATO_{i}", i))];

        ConteudoLido lido = Ler(Conteudo(itens));

        Posicoes(lido, "conteudo.itens[1]", "conteudo.itens[10].formato", "conteudo.itens[10]x", "conteudo.itens[11]")
            .Should().Equal("conteudo.itens[2]", "conteudo.itens[11].formato", "conteudo.itens[10]x", "conteudo.itens[11]");
    }

    [Fact(DisplayName = "Página e termo que a leitura deixou de fora deslocam os seguintes")]
    public void PaginasETermos_ParteNaoLida_DeslocaOsSeguintes()
    {
        ConteudoDoModeloInput entrada = ComPrimeiraPaginaEPrimeiroTermoNaoLidos();

        Posicoes(Ler(entrada), "conteudo.paginas[0].codigo", "conteudo.termos[0].codigo")
            .Should().Equal("conteudo.paginas[1].codigo", "conteudo.termos[1].codigo");
    }

    [Fact(DisplayName = "O campo de um grupo lido muda só o índice do grupo; a posição dentro dele não muda")]
    public void Grupos_CampoDeGrupoLido_MudaSoOIndiceDoGrupo()
    {
        ConteudoDoModeloInput entrada = Conteudo(Item("CERTIFICADO", 0)) with
        {
            Grupos = [Grupo("COMPOSICAO_FAMILIAR", 1).SemOcorrencias().Construir(), Grupo("OUTRA_COMPOSICAO", 2).Construir()],
        };

        Posicoes(Ler(entrada), "conteudo.grupos[0].subitens[1].formato", "conteudo.grupos[0].rotulo")
            .Should().Equal("conteudo.grupos[1].subitens[1].formato", "conteudo.grupos[1].rotulo");
    }

    [Fact(DisplayName = "O documento avulso, o grupo de documentos e o documento do grupo voltam à posição que veio")]
    public void Documentos_ParteNaoLida_DeslocaOsSeguintes()
    {
        ConteudoDoModeloInput entrada = ComPrimeiroDocumentoEPrimeiroGrupoNaoLidos();
        string avulso = "conteudo.conjuntoDeDocumentos.documentosExigidos[0].indeferimento";
        string doGrupo = "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[1].quando";

        Posicoes(Ler(entrada), avulso, doGrupo, "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0]")
            .Should().Equal(
                "conteudo.conjuntoDeDocumentos.documentosExigidos[1].indeferimento",
                "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[1].quando",
                "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[1]");
    }

    [Fact(DisplayName = "O campo sem posição e a recusa sem campo ficam como vieram")]
    public void CampoSemPosicao_FicaComoVeio()
    {
        ConteudoLido lido = Ler(Conteudo(ItemNaoLido("CERTIFICADO", 0), Item("DATA_NASCIMENTO", 1, "DATA")));

        Posicoes(lido, "conteudo.titulo", "conteudo.pressupostos[0]", "conteudo", "conteudo.itens", "descritivo.nome", "conteudo.itens[x]")
            .Should().Equal("conteudo.titulo", "conteudo.pressupostos[0]", "conteudo", "conteudo.itens", "descritivo.nome", "conteudo.itens[x]");
        lido.NasPosicoesDaEntrada([new FieldError(null, Recusa)]).Should().ContainSingle().Which.Field.Should().BeNull();
    }

    [Fact(DisplayName = "O conteúdo lido por inteiro sabe que nada ficou de fora; com recusa de leitura, não")]
    public void PorInteiro_SoQuandoALeituraNaoRecusouNada()
    {
        Ler(Conteudo(Item("CERTIFICADO", 0))).PorInteiro.Should().BeTrue();
        Ler(Conteudo(ItemNaoLido("CERTIFICADO", 0))).PorInteiro.Should().BeFalse();
    }

    private static ConteudoDoModeloInput ComPrimeiraPaginaEPrimeiroTermoNaoLidos() => Conteudo(Item("CERTIFICADO", 0)) with
    {
        Paginas =
        [
            EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("DADOS", 0, "BLOCO_INEXISTENTE").Construir(),
            EntradaDaPaginaDoFormularioDeTeste.Secao("OUTRA", 1).Construir(),
        ],
        Termos =
        [
            EntradaDoTermoExigidoDeTeste.Exigindo("LGPD", 0).ComObrigatoriedade("INEXISTENTE").Construir(),
            EntradaDoTermoExigidoDeTeste.Exigindo("ACEITE", 1).Construir(),
        ],
    };

    private static ConteudoDoModeloInput ComPrimeiroDocumentoEPrimeiroGrupoNaoLidos() => Conteudo(Item("CERTIFICADO", 0)) with
    {
        ConjuntoDeDocumentos = ConjuntoDeDocumentosDoModeloBuilder.Vazio()
            .ComDocumentos(DocumentoNaoLido(), DocumentoLido())
            .ComGrupos(ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, DocumentoNaoLido()), ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, DocumentoLido(), DocumentoLido()))
            .Construir(),
    };

    private static ConteudoLido Ler(ConteudoDoModeloInput entrada) => new LeituraDoConteudo(SemFormatos).Ler(entrada);

    private static string?[] Posicoes(ConteudoLido lido, params string[] campos) =>
        [.. lido.NasPosicoesDaEntrada(campos.Select(static c => new FieldError(c, Recusa))).Select(static e => e.Field)];

    private static EntradaDoGrupoColetadoDeTeste Grupo(string codigo, int ordem) =>
        EntradaDoGrupoColetadoDeTeste.Compondo(codigo, ordem).ComRotulo("Composição").NaSecao("DADOS")
            .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(Campo("MAIOR_IDADE", 0), Campo("SEM_RENDA", 1));

    private static DocumentoExigidoDoModeloInput DocumentoLido() =>
        new("INSCRICAO", "LAUDO_MEDICO", null, new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));

    private static DocumentoExigidoDoModeloInput DocumentoNaoLido() =>
        DocumentoLido() with { Quando = [[new CondicaoPrecondicaoInput("CERTIFICADO", "OPERADOR_INEXISTENTE", JsonSerializer.SerializeToElement(true))]] };
}
