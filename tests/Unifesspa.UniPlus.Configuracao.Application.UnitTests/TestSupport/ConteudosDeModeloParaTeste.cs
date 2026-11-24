namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O catálogo de fatos e o conteúdo de modelo de formulário com que os testes da escrita do
/// modelo montam a entrada, já com os padrões válidos.
/// </summary>
internal static class ConteudosDeModeloParaTeste
{
    public const string Finalidade = "Verificação dos requisitos do processo seletivo.";
    public const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    public static readonly EtapaDoModelo Secao = new("DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", null, null, null);
    public static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    public static void Aprovar(FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        fato.SolicitarAprovacaoDaProtecao("isabella.figueiredo", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
        fato.AprovarProtecao("caroline.lima", DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();
    }

    public static FatoCandidato Declarado(
        string codigo, TipoDoDado dominio = TipoDoDado.SimOuNao, FormatoTexto? formato = null, EscopoFato escopo = EscopoFato.Candidato) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, null, formato, "INSCRICAO", escopo, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;

    public static ConteudoDoModeloInput Conteudo(params FatoColetadoInput[] itens) => new(
        "Habilitação",
        [
            EntradaDaEtapaFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
            EntradaDaEtapaFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão").Construir(),
        ],
        itens,
        [],
        []);

    public static FatoColetadoInput Item(string fato, int ordem, string tipo = "SIM_OU_NAO") =>
        EntradaDoFatoColetadoDeTeste.Coletando(fato, ordem).DoTipo(tipo).NaSecao("DADOS").Construir();

    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    /// <summary>O modelo gravado com o certificado como item, antes de qualquer mudança no catálogo.</summary>
    public static ModeloFormulario Existente(string? tipo)
    {
        ItemDoModelo item = new(
            "CERTIFICADO", 0, "DADOS", new ApresentacaoDoCampo("Certificado", TipoRenderizacao.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));
        return ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, tipo,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }
}
